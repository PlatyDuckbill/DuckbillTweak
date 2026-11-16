using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using DuckbillTweak.Plugin.Emotes;
using DuckbillTweak.Plugin.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;

namespace DuckbillTweak.Plugin.Native;

/// <summary>Owns native IconText nodes inside the game's status HUDs. Never writes the game's status lists.</summary>
internal sealed unsafe class NativeEmoteStatus : IDisposable
{
    private readonly IFramework framework;
    private readonly IGameGui gui;
    private readonly EmoteStatusObserver observer;
    private readonly Configuration config;
    private readonly IPluginLog log;
    private readonly ITextureProvider textures;
    private readonly List<Binding> bindings = [];
    private bool disposed;
    public bool Available { get; }
    public bool HasFailure => bindings.Any(b => b.Failed);
    public bool WaitingSlot => bindings.Any(b => b.Full);
    public string Diagnostic => bindings.FirstOrDefault(b => b.Failed)?.Code ??
        (!NativeEmoteReader.Available ? "EmoteActivityUnresolved" : Available ? "" : "IconTextCtorUnresolved");

    // KR status/statuscustom/targetinfo/targetinfobuffdebuff ULD contracts, checked before each use.
    private sealed record Shape(string Name, bool Target, uint Parent, uint First, uint Count);
    private sealed class Binding(Shape shape)
    {
        public Shape Shape { get; } = shape;
        public AddonController Controller { get; set; } = null!;
        public nint Bound, Parent;
        public EmoteStatusIconNode? Icon;
        public bool Failed, Full;
        public string Code = "";
        public EmoteStatusEntry? LastEntry;
        public string Language = "";
    }

    public NativeEmoteStatus(IFramework framework, IGameGui gui, EmoteStatusObserver observer, Configuration config, IPluginLog log, ITextureProvider textures)
    {
        this.framework = framework; this.gui = gui; this.observer = observer; this.config = config; this.log = log;
        this.textures = textures;
        Available = NativeEmoteReader.Available && AtkComponentIconText.Addresses.Ctor.Value != 0;
        if (!Available) return;
        try
        {
            foreach (var shape in new[] { new Shape("_StatusCustom2", false, 1, 2, 20), new Shape("_Status", false, 1, 2, 30),
                new Shape("_TargetInfoBuffDebuff", true, 2, 3, 30), new Shape("_TargetInfo", true, 23, 24, 30) })
            {
                var binding = new Binding(shape);
                binding.Controller = new() { AddonName = shape.Name, OnSetup = addon => Update(binding, addon),
                    OnUpdate = addon => Update(binding, addon), OnRefresh = addon => Update(binding, addon),
                    OnFinalize = addon => Finalize(binding, addon) };
                bindings.Add(binding);
                binding.Controller.Enable();
            }
            framework.Update += Poll;
        }
        catch { Dispose(); throw; }
    }

    private void Poll(IFramework _)
    {
        if (disposed) return;
        foreach (var binding in bindings)
        {
            var addon = gui.GetAddonByName(binding.Shape.Name);
            if (!addon.IsNull) Update(binding, (AtkUnitBase*)addon.Address);
        }
    }

    private void Fail(Binding binding, string code)
    {
        binding.Failed = true; binding.Code = binding.Shape.Name + ":" + code;
        if (binding.Icon is { } icon) icon.HideStatus();
        log.Warning("Emote status HUD disabled until reopened: {Code}", binding.Code);
    }

    private void Update(Binding binding, AtkUnitBase* addon)
    {
        if (disposed || addon is null || binding.Failed) return;
        if (binding.Bound != 0 && binding.Bound != (nint)addon)
        {
            // Finalize owns detachment. Do not dereference an old parent after an unexpected lifetime change.
            binding.Failed = true; binding.Code = binding.Shape.Name + ":LifetimeChanged"; return;
        }
        var view = observer.View;
        var entry = binding.Shape.Target ? view.Target : view.Self;
        binding.Full = false;
        if (!config.EmoteStatusIcons || entry is null || Environment.TickCount64 - view.Tick > 1000 || !addon->IsVisible)
        {
            if (binding.Icon is { } hidden)
            {
                hidden.HideStatus();
                if (binding.LastEntry is not null) hidden.TextTooltip = "";
            }
            binding.LastEntry = null; binding.Language = "";
            return;
        }
        var parent = addon->GetNodeById(binding.Shape.Parent);
        if (parent is null || parent->Type != NodeType.Res || parent->Width == 0 || parent->Height == 0)
        { Fail(binding, "ParentShape"); return; }
        if (!parent->IsVisible()) { binding.Icon?.HideStatus(); return; }
        if (binding.Parent != 0 && binding.Parent != (nint)parent) { Fail(binding, "ParentChanged"); return; }
        AtkComponentNode* free = null;
        for (var id = binding.Shape.First; id < binding.Shape.First + binding.Shape.Count; id++)
        {
            var slot = addon->GetComponentNodeById(id);
            if (slot is null || slot->Component is null || slot->Component->OwnerNode != slot || slot->ParentNode != parent ||
                slot->Component->GetComponentType() != ComponentType.IconText || slot->Width is not (24 or 25) || slot->Height != 41)
            { Fail(binding, "SlotShape"); return; }
            var image = slot->Component->GetImageNodeById(3);
            var text = slot->Component->GetTextNodeById(2);
            if (image is null || text is null || image->Width != 24 || image->Height != 32 || text->Height != 18 ||
                !float.IsFinite(slot->X) || !float.IsFinite(slot->Y) || !float.IsFinite(slot->ScaleX) || !float.IsFinite(slot->ScaleY) ||
                slot->ScaleX is < .1f or > 10 || slot->ScaleY is < .1f or > 10)
            { Fail(binding, "IconShape"); return; }
            if (!slot->IsVisible() && free is null) free = slot;
        }
        if (free is null)
        { binding.Full = true; if (binding.Icon is { } hidden) hidden.HideStatus(); return; }
        binding.Bound = (nint)addon; binding.Parent = (nint)parent;
        if (binding.Icon is null)
        {
            binding.Icon = new EmoteStatusIconNode();
            IDalamudTextureWrap? texture = null;
            try
            {
                texture = textures.CreateFromRaw(RawImageSpecification.Rgba32(EmoteStatusFrame.Width, EmoteStatusFrame.Height),
                    EmoteStatusFrame.Rgba, "DuckbillTweak.EmoteStatusFrame");
                binding.Icon.LoadFrame(texture); // The native image node takes ownership on success.
                texture = null;
            }
            catch (Exception exception)
            {
                texture?.Dispose();
                binding.Icon.Dispose(); binding.Icon = null;
                log.Error(exception, "Emote status frame texture initialization failed");
                Fail(binding, "FrameTextureUnavailable"); return;
            }
            binding.Icon.AttachNode(parent);
        }
        var node = binding.Icon;
        node.IconId = entry.IconId;
        node.Position = new Vector2(free->X, free->Y);
        node.Scale = new Vector2(free->ScaleX, free->ScaleY);
        if (binding.LastEntry != entry || binding.Language != config.UiLanguage)
        {
            node.TextTooltip = UiText.GetForLanguage(entry.Ownership switch
            {
                EmoteOwnership.Owned => TextKey.EmoteTooltipOwned,
                EmoteOwnership.Missing => TextKey.EmoteTooltipMissing,
                _ => TextKey.EmoteTooltipUnknown
            }, config.UiLanguage, entry.Name);
            binding.LastEntry = entry; binding.Language = config.UiLanguage;
            node.RefreshHoveredTooltip();
        }
        if (node.TextureReady) node.ShowStatus(addon);
        else node.HideStatus();
    }

    private static void Finalize(Binding binding, AtkUnitBase* addon)
    {
        if (binding.Bound != 0 && binding.Bound != (nint)addon) return;
        binding.Icon?.HideStatus(); binding.Icon?.Dispose(); binding.Icon = null; binding.Bound = binding.Parent = 0;
        binding.LastEntry = null; binding.Language = "";
        binding.Failed = binding.Full = false; binding.Code = "";
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; framework.Update -= Poll;
        foreach (var binding in bindings) binding.Controller.Dispose();
        bindings.Clear();
    }
}
