#if DEBUG
using System.Numerics;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace DuckbillTweak.Plugin.Combat;

/// <summary>
/// Research-only probe for the positional FlyText plan (docs/PLANS.md). Toggled by "/dbtw probe" and never saved.
/// Logs the raw ActionEffect bytes of the local player's own actions and every FlyText argument so the server's
/// positional result can be located. It changes nothing in game; the original Receive is always called.
/// The hook address comes from the installed ClientStructs resolver, not from a fixed executable offset.
/// </summary>
internal sealed unsafe class PositionalProbe : IDisposable
{
    private const uint TrueNorthStatus = 1250;
    private delegate void ReceiveDelegate(uint casterEntityId, Character* caster, Vector3* targetPosition,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds);

    private readonly IGameInteropProvider interop;
    private readonly IFlyTextGui flyText;
    private readonly IObjectTable objects;
    private readonly IDataManager data;
    private readonly IPluginLog log;
    private readonly IFramework framework;
    private readonly Dictionary<uint, string> actionNames = [];
    private Hook<ReceiveDelegate>? hook;

    public PositionalProbe(IGameInteropProvider interop, IFlyTextGui flyText, IObjectTable objects, IDataManager data, IPluginLog log, IFramework framework)
    {
        this.interop = interop; this.flyText = flyText; this.objects = objects; this.data = data; this.log = log; this.framework = framework;
    }

    public bool Enabled => hook is not null;

    /// <summary>Returns the new state. Must run on the framework thread.</summary>
    public bool Toggle()
    {
        if (Enabled) Disable();
        else Enable();
        return Enabled;
    }

    private void Enable()
    {
        var address = ActionEffectHandler.Addresses.Receive.Value;
        if (address == 0)
        {
            log.Warning("[PositionalProbe] ActionEffectHandler.Receive was not resolved by ClientStructs; probe not enabled.");
            return;
        }
        hook = interop.HookFromAddress<ReceiveDelegate>(address, Detour);
        hook.Enable();
        flyText.FlyTextCreated += OnFlyText;
        framework.Update += OnFrameworkUpdate;
        log.Information("[PositionalProbe] enabled; Receive=0x{Address:X}", address);
    }

    private void Disable()
    {
        flyText.FlyTextCreated -= OnFlyText;
        framework.Update -= OnFrameworkUpdate;
        seenNodes.Clear();
        hook?.Dispose();
        hook = null;
        actionNames.Clear();
        log.Information("[PositionalProbe] disabled");
    }

    private void Detour(uint casterEntityId, Character* caster, Vector3* targetPosition,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds)
    {
        try { Record(casterEntityId, header, effects, targetIds); }
        catch (Exception exception) { log.Error(exception, "[PositionalProbe] record failed"); }
        hook!.Original(casterEntityId, caster, targetPosition, header, effects, targetIds);
    }

    private void Record(uint casterEntityId, ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds)
    {
        var player = objects.LocalPlayer;
        // ActionType 1 = Action. Mounts, items and other kinds are not positional skills.
        if (player is null || casterEntityId != player.EntityId || header->ActionType != 1) return;
        var combo = ActionManager.Instance()->Combo;
        var trueNorth = player.StatusList.Any(status => status.StatusId == TrueNorthStatus);
        // Own statuses label potency modifiers (e.g. enhanced/honed buffs); level shows level sync.
        var statuses = string.Join(',', player.StatusList.Where(status => status.StatusId != 0)
            .Select(status => $"{status.StatusId}x{status.Param}"));
        var prefix = $"[PositionalProbe] effect t={Environment.TickCount64} action={header->ActionId}/{ActionName(header->ActionId)} " +
            $"spell={header->SpellId} variation={header->AnimationVariation} flags=0x{header->Flags:X2} targets={header->NumTargets} " +
            $"job={player.ClassJob.RowId} level={player.Level} comboAtReceive={combo.Action}/{combo.Timer:F2} trueNorth={trueNorth} statuses=[{statuses}]" +
            // Monk fury stacks live in the job gauge, not in statuses. Read before the original handler updates them.
            (player.ClassJob.RowId == 20 ? $" monkFury=opo{JobGaugeManager.Instance()->Monk.OpoOpoStacks}/raptor{JobGaugeManager.Instance()->Monk.RaptorStacks}/coeurl{JobGaugeManager.Instance()->Monk.CoeurlStacks}" : "") +
            (player.ClassJob.RowId == 30 ? $" kazematoi={JobGaugeManager.Instance()->Ninja.Kazematoi}" : "");
        for (var index = 0; index < header->NumTargets; index++)
        {
            var targetId = targetIds[index].Id;
            var target = objects.SearchById(targetId);
            var geometry = target is null ? "geometry=unknown"
                : Geometry(target.Position, target.Rotation, player.Position, target.HitboxRadius);
            var entries = (ActionEffectHandler.Effect*)(effects + index);
            var parts = new List<string>(8);
            for (var slot = 0; slot < 8; slot++)
            {
                var e = entries[slot];
                if (e.Type == 0) continue;
                parts.Add($"{slot}:type={e.Type} p=[{e.Param0:X2} {e.Param1:X2} {e.Param2:X2} {e.Param3:X2} {e.Param4:X2}] value={e.Value}");
            }
            log.Information("{Prefix} target=0x{Target:X}/{Name} {Geometry} effects=[{Effects}]",
                prefix, targetId, target?.Name.TextValue ?? "?", geometry, string.Join("; ", parts));
            // Full raw bytes so success/failure can be compared bit by bit, including fields ClientStructs does not name.
            log.Information("[PositionalProbe] raw t={Tick} action={Action} header={Header} effects={Effects}",
                Environment.TickCount64, header->ActionId,
                Convert.ToHexString(new ReadOnlySpan<byte>(header, sizeof(ActionEffectHandler.Header))),
                Convert.ToHexString(new ReadOnlySpan<byte>(effects + index, sizeof(ActionEffectHandler.TargetEffects))));        }
    }

    /// <summary>Client-side estimate used only to label probe rows. The server result remains the source of truth.</summary>
    private static string Geometry(Vector3 target, float rotation, Vector3 source, float hitbox)
    {
        var angle = MathF.Atan2(source.X - target.X, source.Z - target.Z) - rotation;
        while (angle > MathF.PI) angle -= MathF.Tau;
        while (angle <= -MathF.PI) angle += MathF.Tau;
        var degrees = angle * 180 / MathF.PI;
        var zone = MathF.Abs(degrees) <= 45 ? "Front" : MathF.Abs(degrees) >= 135 ? "Rear" : "Flank";
        var distance = Vector2.Distance(new(source.X, source.Z), new(target.X, target.Z));
        return $"relative={degrees:F0}deg zone={zone} distance={distance:F1} hitbox={hitbox:F1}";
    }

    private string ActionName(uint actionId)
    {
        if (actionNames.TryGetValue(actionId, out var name)) return name;
        name = data.GetExcelSheet<LuminaAction>().GetRowOrDefault(actionId)?.Name.ExtractText() ?? "?";
        actionNames[actionId] = name;
        return name;
    }

    /// <summary>
    /// Research: logs every visible text node of the _FlyText addon once per (node, text), with fill and edge colors,
    /// to learn which color the FlyText "color" argument drives and what the native fill is.
    /// </summary>
    private void DumpFlyTextNodes()
    {
        var manager = FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkUnitManager.Instance();
        if (manager is null) return;
        var addon = manager->GetAddonByName("_FlyText");
        if (addon is null || !addon->IsVisible) return;
        var seen = new HashSet<string>();
        Walk(&addon->UldManager, 0, seen);
        foreach (var key in seenNodes.Keys.Where(key => !seen.Contains(key)).ToList()) seenNodes.Remove(key);
    }

    private readonly Dictionary<string, bool> seenNodes = [];

    private void OnFrameworkUpdate(IFramework _)
    {
        try { DumpFlyTextNodes(); }
        catch (Exception exception) { log.Error(exception, "[PositionalProbe] flytext node dump failed"); }
    }

    private void Walk(FFXIVClientStructs.FFXIV.Component.GUI.AtkUldManager* uld, int depth, HashSet<string> seen)
    {
        if (depth > 4) return;
        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node is null || !node->IsVisible()) continue;
            if (node->Type == FFXIVClientStructs.FFXIV.Component.GUI.NodeType.Text)
            {
                var text = (FFXIVClientStructs.FFXIV.Component.GUI.AtkTextNode*)node;
                var value = text->NodeText.ToString();
                if (string.IsNullOrEmpty(value)) continue;
                var key = $"{(nint)node:X}|{value}";
                seen.Add(key);
                if (!seenNodes.TryAdd(key, true)) continue;
                log.Information("[PositionalProbe] flynode t={Tick} depth={Depth} id={Id} text={Text} fill=0x{Fill:X8} edge=0x{Edge:X8} font={Font} size={Size} flags=0x{Flags:X}",
                    Environment.TickCount64, depth, node->NodeId, value, text->TextColor.RGBA, text->EdgeColor.RGBA, text->FontType, text->FontSize, (int)text->TextFlags);
            }
            else if ((int)node->Type >= 1000)
            {
                var component = ((FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentNode*)node)->Component;
                if (component is not null) Walk(&component->UldManager, depth + 1, seen);
            }
        }
    }

    private void OnFlyText(ref FlyTextKind kind, ref int value1, ref int value2, ref SeString text1, ref SeString text2,
        ref uint color, ref uint icon, ref uint damageTypeIcon, ref float yOffset, ref bool handled)
    {
        log.Information("[PositionalProbe] flytext t={Tick} kind={Kind} value1={Value1} value2={Value2} text1={Text1} text2={Text2} color=0x{Color:X8} icon={Icon} damageIcon={DamageIcon} y={Y:F1}",
            Environment.TickCount64, kind, value1, value2, text1?.TextValue ?? "", text2?.TextValue ?? "", color, icon, damageTypeIcon, yOffset);
    }

    public void Dispose()
    {
        if (Enabled) Disable();
    }
}
#endif
