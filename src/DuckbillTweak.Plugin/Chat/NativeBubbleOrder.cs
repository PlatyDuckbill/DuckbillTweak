using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Plugin.Services;

namespace DuckbillTweak.Plugin.Chat;

// Uses only the installed ClientStructs functions and lists; no module/executable hash gate.
// Runtime contract: KR ULD shape (MiniTalk and NamePlate each have root 1 and custom component 2/1001),
// unique non-null layer membership, and after a cross-layer move the old layer no longer lists the addon.
internal sealed unsafe class NativeBubbleOrder : IDisposable
{
    private sealed record Saved(nint Addon, ushort Id, uint Layer, ushort Previous, nint BeforeAddress,
        ushort Next, nint AfterAddress, uint AppliedLayer);
    private Saved? saved;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IPluginLog log;
    private uint territory;
    private bool contractFailed;

    public NativeBubbleOrder(IFramework framework, IClientState client, IPluginLog log)
    {
        this.framework = framework; this.client = client; this.log = log;
        framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework _)
    {
        if (territory != client.TerritoryType) { Restore(); territory = client.TerritoryType; }
        if (contractFailed || !client.IsLoggedIn) { Restore(); return; }
        Update();
    }

    private void Update()
    {
        var manager = RaptureAtkUnitManager.Instance();
        if (manager is null) return;
        var bubble = manager->GetAddonByName("MiniTalk");
        var plate = manager->GetAddonByName("NamePlate");
        if (!Shape(bubble) || !Shape(plate)) { Restore(); return; }
        if (saved is not null && (saved.Addon != (nint)bubble || saved.Id != bubble->Id)) saved = null;
        var bubbleIndex = Index(manager, bubble);
        var plateIndex = Index(manager, plate);
        if (bubbleIndex < 0 || plateIndex < 0) return;
        // Layer lists run from front to back. Lower layers are drawn in front.
        if (bubble->DepthLayer < plate->DepthLayer ||
            bubble->DepthLayer == plate->DepthLayer && bubbleIndex < plateIndex) return;
        if (saved is null)
        {
            ref var layer = ref manager->DepthLayers[(int)bubble->DepthLayer];
            var before = bubbleIndex > 0 ? layer.Entries[bubbleIndex - 1].Value : null;
            var after = bubbleIndex + 1 < layer.Count ? layer.Entries[bubbleIndex + 1].Value : null;
            var previous = before is not null ? before->Id : (ushort)0;
            var next = after is not null ? after->Id : (ushort)0;
            saved = new((nint)bubble, bubble->Id, bubble->DepthLayer, previous, (nint)before, next, (nint)after, bubble->DepthLayer);
        }
        // KR RVA 6816A0 inserts the moving addon immediately before the reference
        // in the front-to-back list. Do not change either screen's nodes/settings.
        // KR SetAddonOrderAfter writes the destination layer before searching
        // its list. Cross-layer moves must remove the old membership first.
        if (!MoveLayer(manager, bubble, plate->DepthLayer)) return;
        saved = saved with { AppliedLayer = bubble->DepthLayer };
        manager->SetAddonOrderAfter(plate, bubble, true);
    }

    public void Restore()
    {
        var previous = saved;
        saved = null;
        if (previous is null) return;
        var manager = RaptureAtkUnitManager.Instance();
        if (manager is null) return;
        var bubble = manager->GetAddonByName("MiniTalk");
        if (!Shape(bubble) || (nint)bubble != previous.Addon || bubble->Id != previous.Id ||
            bubble->DepthLayer != previous.AppliedLayer || Index(manager, bubble) < 0) return;
        // SetAddonDepthLayerAndOrder skips ordering when the layer is unchanged.
        // Resolve surviving neighbors afresh and use the native ordering operation.
        var next = previous.Next == 0 ? null : manager->GetAddonById(previous.Next);
        var before = previous.Previous == 0 ? null : manager->GetAddonById(previous.Previous);
        if (!MoveLayer(manager, bubble, previous.Layer)) return;
        if (next is not null && (nint)next == previous.AfterAddress && next->DepthLayer == previous.Layer && Index(manager, next) >= 0)
            manager->SetAddonOrderAfter(next, bubble, true);
        else if (before is not null && (nint)before == previous.BeforeAddress && before->DepthLayer == previous.Layer && Index(manager, before) >= 0)
        {
            // Restore the final entry without the native non-immediate fallback,
            // which wraps to the beginning when the reference has no successor.
            manager->SetAddonOrderAfter(before, bubble, true);
            manager->SetAddonOrderAfter(bubble, before, true);
        }
        // If both original neighbors have disappeared, only the original layer
        // can be restored. Never use a stale neighbor or invent an exact slot.
    }

    private bool MoveLayer(RaptureAtkUnitManager* manager, AtkUnitBase* addon, uint layer)
    {
        if (contractFailed || layer >= manager->DepthLayers.Length || Index(manager, addon) < 0) return false;
        if (addon->DepthLayer == layer) return true;
        ref var destination = ref manager->DepthLayers[(int)layer];
        if (destination.Count >= 256) return false;
        for (var i = 0; i < destination.Count; i++) if (destination.Entries[i].Value is null) return false;
        var source = addon->DepthLayer;
        if (!manager->SetAddonDepthLayer(addon->Id, layer)) return false;
        // The inspected KR build removes the old membership first; verify that at runtime instead of a build hash.
        if (addon->DepthLayer == layer && Index(manager, addon) >= 0 && !Lists(manager, source, addon)) return true;
        contractFailed = true;
        log.Error("Chat bubble order: depth layer move contract failed (layer {From} -> {To}); disabled until reload.", source, layer);
        return false;
    }

    private static bool Lists(RaptureAtkUnitManager* manager, uint layerIndex, AtkUnitBase* addon)
    {
        if (layerIndex >= manager->DepthLayers.Length) return false;
        ref var layer = ref manager->DepthLayers[(int)layerIndex];
        for (var i = 0; i < layer.Count && i < 256; i++) if (layer.Entries[i].Value == addon) return true;
        return false;
    }

    private static bool Shape(AtkUnitBase* addon)
    {
        if (addon is null || addon->RootNode is null || addon->RootNode->NodeId != 1) return false;
        var node = addon->GetNodeById(2);
        return node is not null && node->Type == (NodeType)1001;
    }

    private static int Index(RaptureAtkUnitManager* manager, AtkUnitBase* addon)
    {
        if (addon->DepthLayer >= manager->DepthLayers.Length) return -1;
        ref var layer = ref manager->DepthLayers[(int)addon->DepthLayer];
        if (layer.Count > 256) return -1;
        var index = -1;
        for (var i = 0; i < layer.Count; i++)
        {
            var entry = layer.Entries[i].Value;
            if (entry is null) return -1;
            if (entry != addon) continue;
            if (index >= 0) return -1;
            index = i;
        }
        return index;
    }

    public void Dispose() { framework.Update -= OnUpdate; Restore(); }
}
