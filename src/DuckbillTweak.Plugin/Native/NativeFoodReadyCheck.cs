using System.Numerics;
using Dalamud.Plugin.Services;
using DuckbillTweak.Plugin.Food;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;

namespace DuckbillTweak.Plugin.Native;

/// <summary>Read-only food labels beside names in the native KR ReadyCheck, with native name widths restored.</summary>
internal sealed unsafe class NativeFoodReadyCheck : IDisposable
{
    private sealed record Label(nint Name, ushort Width, TextNode Food);
    private readonly FoodReminder food;
    private readonly IPluginLog log;
    private readonly IFramework framework;
    private readonly IGameGui gui;
    private readonly AddonController controller;
    private readonly Dictionary<uint, Label> labels = [];
    private nint bound;
    private bool failed;
    public bool ObservationFailed => failed;
    private long lastUpdate;
    private long lastPoll;
#if DEBUG
    private string? lastDiagnostic;
#endif

    public NativeFoodReadyCheck(FoodReminder food, IPluginLog log, IFramework framework, IGameGui gui)
    {
        this.food = food; this.log = log; this.framework = framework; this.gui = gui;
        controller = new() { AddonName = "ReadyCheck", OnSetup = Update, OnRefresh = Update, OnUpdate = Update,
            OnFinalize = FinalizeAddon };
        controller.Enable();
        framework.Update += Poll;
    }

    // ReadyCheck also needs to be observed when no native update/refresh callback is received.
    // Native reads and writes still happen only on Framework; finalization remains lifecycle-bound.
    private void Poll(IFramework _)
    {
        if (Environment.TickCount64 - lastPoll < 200) return;
        lastPoll = Environment.TickCount64;
        var addon = gui.GetAddonByName("ReadyCheck");
        if (!addon.IsNull) Update((AtkUnitBase*)addon.Address);
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void Trace(string message)
    {
#if DEBUG
        if (lastDiagnostic == message) return;
        lastDiagnostic = message;
        log.Information("Food ReadyCheck: {State}", message);
#endif
    }

    private void FinalizeAddon(AtkUnitBase* addon)
    {
        Detach(addon);
#if DEBUG
        lastDiagnostic = null;
#endif
    }

    private void Update(AtkUnitBase* addon)
    {
        if (!addon->IsVisible) { Detach(addon); return; }
        if (!food.Ready)
        {
            Trace($"observation unavailable: {food.ObservationState}");
            Detach(addon); return;
        }
        if (failed) return;
        if (Environment.TickCount64 - lastUpdate < 200) return;
        lastUpdate = Environment.TickCount64;
        try
        {
            // KR ULD: 24 custom member components (11..34), text 5 at x52, 160x28.
            // No fixed addon offsets or ready-check/party-list ordering assumptions.
            var agent = AgentReadyCheck.Instance();
            // Window width changes with party/alliance mode; only the member component contract is fixed.
            // Observed KR runtime: ReadyCheck is visible while AgentReadyCheck.AddonId remains zero.
            // This agent provides entries but does not prove ownership of the native ReadyCheck window.
            // Validate that window's member components below; absent agent entries mean unknown food labels.
            if (addon->RootNode is null)
            {
                Trace($"root pending: Addon={addon->Id}");
                Detach(addon); return;
            }
            if (bound != 0 && bound != (nint)addon) return;
            bound = (nint)addon;
            var readyIds = new HashSet<ulong>();
            if (agent is not null)
                foreach (var entry in agent->ReadyCheckEntries) if (entry.ContentId != 0) readyIds.Add(entry.ContentId);
            var populated = 0;
            var linked = 0;
            var known = 0;
            for (uint id = 11; id <= 34; id++)
            {
                var owner = addon->GetComponentNodeById(id);
                var component = owner is not null ? owner->Component : null;
                var name = component is not null ? component->GetTextNodeById(5) : null;
                if (owner is null || owner->Width != 212 || owner->Height != 28 || name is null ||
                    name->ParentNode is null || name->Type != NodeType.Text || name->X != 52 || name->Height != 28 ||
                    component->GetImageNodeById(4) is null ||
                    (name->Width != 160 && !(labels.ContainsKey(id) && name->Width == 100)))
                {
                    Detach(addon); failed = true;
                    log.Warning("Ready-check member component {Node} did not match food label contract: Owner={Width}x{Height}; Name={NameWidth}x{NameHeight}; NameX={NameX}; NameType={NameType}",
                        id, owner is null ? 0 : owner->Width, owner is null ? 0 : owner->Height,
                        name is null ? 0 : name->Width, name is null ? 0 : name->Height,
                        name is null ? -1 : name->X, name is null ? 0 : (int)name->Type);
                    return;
                }
                if (!labels.TryGetValue(id, out var label))
                {
                    var node = new TextNode { Position = new(152, 0), Size = new(60, 28), FontSize = 12,
                        AlignmentType = AlignmentType.Right, String = "미확인" };
                    label = new((nint)name, name->Width, node);
                    labels.Add(id, label);
                    node.AttachNode(name->ParentNode);
                }
                if (label.Name != (nint)name) { Detach(addon); failed = true; return; }
                var nativeName = name->NodeText.ToString().Trim();
                var matches = food.Members.Where(m => m.Name == nativeName && readyIds.Contains(m.ContentId)).ToArray();
                // In alliance ready checks a same-named player in another party cannot be disambiguated by text.
                var member = readyIds.Count <= 8 && matches.Length == 1 ? matches[0] : null;
                if (nativeName.Length > 0) populated++;
                if (member is not null) linked++;
                if (member?.Seconds is not null) known++;
                var text = member?.Display ?? "미확인";
                if (label.Food.String != text) label.Food.String = text;
                label.Food.IsVisible = nativeName.Length > 0;
                label.Food.TextColor = member?.Seconds is null ? new Vector4(0.65f, 0.65f, 0.65f, 1) :
                    member.IsLow(food.MinimumMinutes ?? 1) ? new Vector4(1, 0.4f, 0.3f, 1) : Vector4.One;
                name->SetWidth(100);
            }
            Trace($"labels attached: Addon={addon->Id}; AgentAddon={(agent is null ? 0 : agent->AddonId)}; Labels={labels.Count}; ReadyIds={readyIds.Count}; Members={food.Members.Count}; Populated={populated}; Linked={linked}; Known={known}");
        }
        catch (Exception exception)
        {
            Detach(addon); failed = true;
            log.Error(exception, "Ready-check food labels disabled until the window is reopened");
        }
    }

    private void Detach(AtkUnitBase* addon)
    {
        if (bound != 0 && bound != (nint)addon) return;
        foreach (var (id, label) in labels)
        {
            var component = addon->GetComponentByNodeId(id);
            var name = component is not null ? component->GetTextNodeById(5) : null;
            if ((nint)name == label.Name) name->SetWidth(label.Width);
            label.Food.Dispose();
        }
        labels.Clear(); bound = 0; failed = false; lastUpdate = 0;
    }

    public void Dispose() { framework.Update -= Poll; controller.Dispose(); }
}
