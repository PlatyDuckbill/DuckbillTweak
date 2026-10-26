using System.Diagnostics;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Events.EventDataTypes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using DuckbillTweak.Plugin.Shops;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using NativeEventType = FFXIVClientStructs.FFXIV.Component.GUI.AtkEventType;

namespace DuckbillTweak.Plugin.Native;

internal sealed unsafe class NativeLootInteraction : IDisposable
{
    private sealed record OpenRequest(ushort AddonId, int Index, uint ItemId, long Received);
    private readonly IAddonEventManager events;
    private readonly IAddonLifecycle lifecycle;
    private readonly Configuration config;
    private readonly NativeLootAddon popup;
    private readonly IPartyList party;
    private readonly IGameGui gui;
    private readonly MerchantService merchants;
    private readonly IPluginLog log;
    private readonly AddonController<AddonNeedGreed> controller;
    private readonly Dictionary<nint, IAddonEventHandle> handles = [];
    private readonly HashSet<(AddonEventType Type, byte Button)> observedEvents = [];
    private OpenRequest? pending;
    private OpenRequest? lastAccepted;
    private (ushort AddonId, int Items, int Rows, int Nodes)? lastShape;
    private bool disposed;

    public NativeLootInteraction(IAddonEventManager events, IAddonLifecycle lifecycle, IGameGui gui, IPartyList party,
        Configuration config, NativeLootAddon popup, MerchantService merchants, IPluginLog log)
    {
        this.events = events; this.lifecycle = lifecycle; this.gui = gui; this.party = party;
        this.config = config; this.popup = popup; this.merchants = merchants; this.log = log;
        controller = new AddonController<AddonNeedGreed> { AddonName = "NeedGreed", OnUpdate = Update, OnFinalize = _ => Reset() };
        lifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "NeedGreed", ReceiveListEvent);
        try { controller.Enable(); }
        catch { lifecycle.UnregisterListener(ReceiveListEvent); throw; }
    }

    private static AtkComponentList* FindList(AddonNeedGreed* addon)
    {
        if (addon is null || addon->RootNode is null || !addon->IsVisible || addon->NumItems < 0 || addon->NumItems > addon->Items.Length)
            return null;
        var list = addon->GetComponentListById(6);
        return list is not null && list->ListLength >= 0 && list->ListLength <= addon->Items.Length ? list : null;
    }

    private void Update(AddonNeedGreed* addon)
    {
        if (!config.LootRolls) { Reset(); return; }
        var list = FindList(addon);
        if (list is null) { Reset(); return; }
        var current = new HashSet<nint>();
        for (var i = 0; i < list->ListLength; i++)
        {
            var renderer = list->GetItemRenderer(i);
            if (renderer is null) continue;
            // Mouse events are dispatched to the row's hit nodes, not necessarily its component owner.
            if (renderer->EventNode is not null) current.Add((nint)renderer->EventNode);
            if (renderer->CollisionNodeList is null || renderer->CollisionNodeListCount > 64) continue;
            for (var node = 0; node < renderer->CollisionNodeListCount; node++)
                if (renderer->CollisionNodeList[node] is not null) current.Add((nint)renderer->CollisionNodeList[node]);
        }
        foreach (var node in current)
        {
            if (handles.ContainsKey(node)) continue;
            var handle = events.AddEvent((nint)addon, node, AddonEventType.MouseDown, ReceiveCollisionEvent);
            if (handle is not null) handles[node] = handle;
        }
        foreach (var node in handles.Keys.Where(k => !current.Contains(k)).ToArray())
        { events.RemoveEvent(handles[node]); handles.Remove(node); }
        var shape = (AddonId: addon->Id, Items: addon->NumItems, Rows: list->ListLength, Nodes: handles.Count);
        if (lastShape != shape)
        {
            log.Information("Loot click binding: Addon={Addon}; Items={Items}; Rows={Rows}; HitNodes={Nodes}",
                shape.AddonId, shape.Items, shape.Rows, shape.Nodes);
            lastShape = shape;
        }
        var request = pending;
        if (request is null) return;
        pending = null;
        if (Stopwatch.GetElapsedTime(request.Received).TotalSeconds > 1 || request.AddonId != addon->Id ||
            request.Index < 0 || request.Index >= addon->NumItems || request.Index >= list->ListLength ||
            addon->Items[request.Index].ItemId != request.ItemId) return;
        try
        {
            // Open outside the input callback, after revalidating the copied row identity.
            popup.ShowItem(request.ItemId, merchants.ItemDisplayName(request.ItemId), party.IsAlliance);
            log.Information("Loot roll popup requested: ItemId={Item}; Index={Index}", request.ItemId, request.Index);
        }
        catch (Exception ex) { log.Error(ex, "Loot roll popup could not open for item {Item}", request.ItemId); }
    }

    private void ReceiveListEvent(AddonEvent type, AddonArgs args)
    {
        if (disposed || !config.LootRolls || args is not AddonReceiveEventArgs received || received.AtkEventData == 0) return;
        var eventType = (NativeEventType)received.AtkEventType;
        if (eventType is not (NativeEventType.ListItemClick or NativeEventType.ListItemDoubleClick)) return;
        var current = gui.GetAddonByName("NeedGreed");
        if (current.IsNull || current.Address != args.Addon.Address) return;
        var addon = (AddonNeedGreed*)current.Address;
        var list = FindList(addon);
        if (list is null) return;
        var data = ((AtkEventData*)received.AtkEventData)->ListItemData;
        if (observedEvents.Add((received.AtkEventType, data.MouseButtonId)))
            log.Debug("Loot list input: Type={Type}; Button={Button}; Index={Index}", received.AtkEventType, data.MouseButtonId, data.SelectedIndex);
        if (data.MouseButtonId != 1 || data.SelectedIndex < 0 || data.SelectedIndex >= list->ListLength) return;
        var renderer = list->GetItemRenderer(data.SelectedIndex);
        if (renderer is null || renderer != data.ListItemRenderer) return;
        if (Queue(addon, list, data.SelectedIndex)) args.PreventOriginal();
    }

    private void ReceiveCollisionEvent(AddonEventType type, AddonEventData received)
    {
        if (disposed || !config.LootRolls || received is not AddonMouseEventData { IsRightClick: true, IsNoModifier: true }) return;
        var current = gui.GetAddonByName("NeedGreed");
        if (current.IsNull || current.Address != received.AddonPointer) return;
        var addon = (AddonNeedGreed*)current.Address;
        var list = FindList(addon);
        if (list is null) return;
        for (var i = 0; i < list->ListLength; i++)
        {
            var renderer = list->GetItemRenderer(i);
            if (renderer is null) continue;
            var matches = (nint)renderer->EventNode == received.NodeTargetPointer;
            if (!matches && renderer->CollisionNodeList is not null && renderer->CollisionNodeListCount <= 64)
                for (var node = 0; node < renderer->CollisionNodeListCount && !matches; node++)
                    matches = (nint)renderer->CollisionNodeList[node] == received.NodeTargetPointer;
            if (!matches) continue;
            if (Queue(addon, list, i) && received.AtkEventPointer != 0)
                ((AtkEvent*)received.AtkEventPointer)->SetEventIsHandled();
            return;
        }
    }

    private bool Queue(AddonNeedGreed* addon, AtkComponentList* list, int index)
    {
        if (index < 0 || index >= addon->NumItems || index >= list->ListLength || index >= addon->Items.Length) return false;
        var item = addon->Items[index];
        if (item.ItemId == 0) return false;
        // The same physical click can reach both the hit node and the list callback.
        if (lastAccepted is { } previous && previous.AddonId == addon->Id && previous.Index == index &&
            previous.ItemId == item.ItemId && Stopwatch.GetElapsedTime(previous.Received).TotalMilliseconds < 300) return true;
        log.Information("Loot right-click received: ItemId={Item}; Index={Index}", item.ItemId, index);
        pending = lastAccepted = new(addon->Id, index, item.ItemId, Stopwatch.GetTimestamp());
        return true;
    }

    private void Reset()
    {
        pending = null; lastAccepted = null; lastShape = null; observedEvents.Clear();
        foreach (var handle in handles.Values) events.RemoveEvent(handle);
        handles.Clear();
    }
    public void Dispose()
    {
        disposed = true; lifecycle.UnregisterListener(ReceiveListEvent); controller.Dispose(); Reset();
    }
}
