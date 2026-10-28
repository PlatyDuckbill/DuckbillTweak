using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DuckbillTweak.Plugin.Native;

// Collision collection reads applied draw visibility, which can still describe
// the previous frame immediately after ToggleVisibility/Relayout.
internal sealed unsafe class NativeCollisionRefresh : IDisposable
{
    private readonly IAddonLifecycle lifecycle;
    private nint pending;
    private ushort pendingId;

    public NativeCollisionRefresh(IAddonLifecycle lifecycle, string name)
    {
        this.lifecycle = lifecycle;
        lifecycle.RegisterListener(AddonEvent.PostDraw, name, AfterDraw);
    }

    public void Request(AtkUnitBase* addon)
    {
        pending = (nint)addon;
        pendingId = addon->Id;
        addon->UpdateCollisionNodeList(false);
    }

    private void AfterDraw(AddonEvent _, AddonArgs args)
    {
        if (pending == 0 || args.Addon.Address != pending) return;
        var addon = (AtkUnitBase*)pending;
        pending = 0;
        if (addon->Id == pendingId) addon->UpdateCollisionNodeList(false);
    }

    public void Clear() { pending = 0; pendingId = 0; }
    public void Dispose() { Clear(); lifecycle.UnregisterListener(AfterDraw); }
}
