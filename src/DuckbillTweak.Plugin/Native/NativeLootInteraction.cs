using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Events.EventDataTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using KamiToolKit.Controllers;

namespace DuckbillTweak.Plugin.Native;

internal sealed unsafe class NativeLootInteraction : IDisposable
{
    private readonly IAddonEventManager events;
    private readonly Configuration config;
    private readonly NativeLootAddon popup;
    private readonly IPartyList party;
    private readonly IGameGui gui;
    private readonly AddonController<AddonNeedGreed> controller;
    private readonly Dictionary<nint, IAddonEventHandle> handles = [];

    public NativeLootInteraction(IAddonEventManager events, IGameGui gui, IPartyList party,
        Configuration config, NativeLootAddon popup)
    {
        this.events = events; this.gui = gui; this.party = party; this.config = config; this.popup = popup;
        controller = new AddonController<AddonNeedGreed> { AddonName = "NeedGreed", OnUpdate = Update,
            OnFinalize = _ => RemoveEvents() };
        controller.Enable();
    }

    private void Update(AddonNeedGreed* addon)
    {
        if (!config.LootRolls) { RemoveEvents(); return; }
        if (addon->RootNode is null || addon->NumItems is < 0 or > 16) return;
        var list = addon->GetComponentListById(6);
        if (list is null || list->ListLength is < 0 or > 16) return;
        var current = new HashSet<nint>();
        for (var i = 0; i < list->ListLength; i++)
        {
            var renderer = list->GetItemRenderer(i);
            if (renderer is null || renderer->OwnerNode is null) continue;
            var node = (nint)renderer->OwnerNode;
            current.Add(node);
            if (handles.ContainsKey(node)) continue;
            var handle = events.AddEvent((nint)addon, node, AddonEventType.MouseUp, Click);
            if (handle is not null) handles[node] = handle;
        }
        foreach (var node in handles.Keys.Where(k => !current.Contains(k)).ToArray())
        { events.RemoveEvent(handles[node]); handles.Remove(node); }
    }

    private void Click(AddonEventType type, AddonEventData data)
    {
        if (!config.LootRolls || data is not AddonMouseEventData { IsRightClick: true, IsNoModifier: true, IsDragging: false }) return;
        var current = gui.GetAddonByName("NeedGreed");
        if (current.IsNull || current.Address != data.AddonPointer) return;
        var addon = (AddonNeedGreed*)current.Address;
        var list = addon->GetComponentListById(6);
        if (list is null || addon->NumItems is < 1 or > 16) return;
        var index = list->HoveredItemIndex;
        if (index < 0 || index >= addon->NumItems || index >= addon->Items.Length) return;
        var item = addon->Items[index];
        if (item.ItemId == 0) return;
        var hovered = (uint)gui.HoveredItem;
        if (hovered != 0 && hovered != item.ItemId) return;
        // Values are copied while the event's addon is alive. No pointer is passed into the popup.
        popup.ShowItem(item.ItemId, item.ItemName.ToString(), party.IsAlliance);
    }

    private void RemoveEvents() { foreach (var handle in handles.Values) events.RemoveEvent(handle); handles.Clear(); }
    public void Dispose() { controller.Dispose(); RemoveEvents(); }
}
