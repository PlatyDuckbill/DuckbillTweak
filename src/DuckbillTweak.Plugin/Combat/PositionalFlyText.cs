using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Combat;
using DuckbillTweak.Plugin.Native;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace DuckbillTweak.Plugin.Combat;

    /// <summary>
/// Colors the native damage FlyText of the player's own combo/positional weaponskills by the server's result:
/// a missed combo first, otherwise positional hit/miss. The server sends no positional/combo flag (raw packet compared,
/// docs/PLANS.md); its damage bonus byte is floor((applied - unbonused) * 100 / applied) and is decoded against every
/// combo/positional state. Potencies come from the KR tooltips evaluated for the current job and level (Core
/// BonusTooltip, same code as the offline coverage check). Tooltips that do not fit the verified model leave that
/// action native. Scope: exact verified KR build, outside PvP and special fields.
/// </summary>
internal sealed unsafe class PositionalFlyText : IDisposable
{
    /// <summary>FlyText colors are ABGR (native damage is 0xFF0061E3, orange); settings store RGB 0xRRGGBB.</summary>
    private static uint ToFlyTextColor(uint rgb) => 0xFF000000 | ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF);
    private const long MatchWindowMs = 2000;
    // TerritoryIntendedUse ids from the KR sheet (GameDataProbe territory-uses): PvP, Diadem, deep dungeons,
    // Eureka, Bozja/Delubrum, variant/criterion, cosmic exploration, Occult Crescent, monster trials.
    private static readonly HashSet<uint> ExcludedUses = [18, 26, 27, 28, 31, 37, 38, 39, 41, 44, 45, 47, 48, 52, 53, 57, 58, 60, 61, 62];

    private delegate void ReceiveDelegate(uint casterEntityId, Character* caster, Vector3* targetPosition,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds);
    internal enum Outcome { PositionalHit, PositionalMiss, ComboMissed }
    private sealed record Pending(string Name, int Damage, int Bonus, Outcome Outcome, long Tick);

    private readonly IFramework framework;
    private readonly IFlyTextGui flyText;
    private readonly IObjectTable objects;
    private readonly IClientState client;
    private readonly IDataManager data;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly Dictionary<uint, string> names = [];
    private readonly Dictionary<uint, string> macros = [];
    private readonly Dictionary<uint, BonusAction> table = [];
    private readonly List<Pending> pending = [];
    private readonly Hook<ReceiveDelegate>? hook;
    private (uint Job, int Level) tableKey;
    private uint territory;
    private bool territoryAllowed;
    private bool active;

    public PositionalFlyText(IGameInteropProvider interop, IFramework framework, IFlyTextGui flyText, IObjectTable objects,
        IClientState client, IDataManager data, IPluginLog log, Configuration config, NativeExecutable executable)
    {
        this.framework = framework; this.flyText = flyText; this.objects = objects; this.client = client;
        this.data = data; this.log = log; this.config = config;
        var actions = data.GetExcelSheet<LuminaAction>();
        var transients = data.GetExcelSheet<ActionTransient>();
        foreach (var id in BonusTooltip.Candidates)
        {
            if (actions.GetRowOrDefault(id) is not { } row || transients.GetRowOrDefault(id) is not { } transient) continue;
            names[id] = row.Name.ExtractText();
            macros[id] = transient.Description.ToMacroString();
        }
        // Potencies and the verified model are tied to the exact KR build.
        var address = ActionEffectHandler.Addresses.Receive.Value;
        if (!executable.Matches) log.Warning("Positional FlyText disabled: {Reason}", executable.Reason);
        else if (address == 0) log.Warning("Positional FlyText disabled: ActionEffectHandler.Receive was not resolved.");
        else if (macros.Count != BonusTooltip.Candidates.Count) log.Warning("Positional FlyText disabled: action rows missing.");
        else hook = interop.HookFromAddress<ReceiveDelegate>(address, Detour);
        framework.Update += Update;
        flyText.FlyTextCreated += OnFlyText;
    }

    private void Update(IFramework _)
    {
        if (client.TerritoryType != territory)
        {
            territory = client.TerritoryType;
            pending.Clear();
            var use = data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territory)?.TerritoryIntendedUse.RowId;
            territoryAllowed = use is { } value && !ExcludedUses.Contains(value);
        }
        var player = objects.LocalPlayer;
        var enabled = config.PositionalFlyText;
        if (hook is not null && enabled && player is not null) RebuildTable(player);
        var wanted = hook is not null && enabled && client.IsLoggedIn && !client.IsPvP
            && territoryAllowed && player is not null && table.Count > 0;
        if (wanted != active)
        {
            active = wanted;
            if (wanted) hook!.Enable();
            else { hook?.Disable(); pending.Clear(); }
        }
        if (pending.Count != 0)
        {
            var now = Environment.TickCount64;
            pending.RemoveAll(entry => now - entry.Tick > MatchWindowMs);
        }
    }

    /// <summary>Evaluates the candidate tooltips for the current job and (synced) level, only when either changes.</summary>
    private void RebuildTable(IPlayerCharacter player)
    {
        var key = (player.ClassJob.RowId, (int)player.Level);
        if (key == tableKey) return;
        tableKey = key;
        table.Clear();
        pending.Clear();
        var rejected = new List<string>();
        foreach (var (id, macro) in macros)
        {
            var action = BonusTooltip.Build(id, macro, (int)key.Item1, key.Item2, out var reason);
            if (action is not null) table[id] = action;
            else if (TooltipMentionsJob(macro, key.Item1)) rejected.Add($"{id}:{reason}");
        }
        log.Information("Bonus FlyText table: Job={Job}; Level={Level}; Supported={Supported}; Rejected={Rejected}",
            key.Item1, key.Item2, string.Join(',', table.Keys), rejected.Count == 0 ? "none" : string.Join(" | ", rejected));
    }

    // Keeps the log readable: report rejections only for tooltips that branch on the current job.
    private static bool TooltipMentionsJob(string macro, uint job) => macro.Contains($"gnum68=={job}", StringComparison.Ordinal);

    private void Detour(uint casterEntityId, Character* caster, Vector3* targetPosition,
        ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targetIds)
    {
        if (active)
        {
            try { Record(casterEntityId, header, effects); }
            catch (Exception exception) { log.Error(exception, "Positional FlyText: result read failed."); }
        }
        hook!.Original(casterEntityId, caster, targetPosition, header, effects, targetIds);
    }

    /// <summary>Runs before the original handler, so statuses, gauges and combo still describe the moment of use.</summary>
    private void Record(uint casterEntityId, ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects)
    {
        // ActionType 1 = Action. Positional weaponskills are single-target, so only the first target is read.
        if (header->ActionType != 1 || header->NumTargets < 1) return;
        if (!table.TryGetValue(header->ActionId, out var action)) return;
        var player = objects.LocalPlayer;
        if (player is null || casterEntityId != player.EntityId) return;
        if ((player.ClassJob.RowId, (int)player.Level) != tableKey) return; // table not rebuilt for this state yet
        var entries = (ActionEffectHandler.Effect*)effects;
        for (var slot = 0; slot < 8; slot++)
        {
            var effect = entries[slot];
            if (effect.Type != 3) continue; // 3 = damage
            // Param4 0x40 marks a damage value above 65535 with Param3 as the high byte.
            var damage = effect.Value | ((effect.Param4 & 0x40) != 0 ? effect.Param3 << 16 : 0);
            var context = ReadContext(player);
            // Combo and positional are both decoded from the server byte; only buffs/gauges come from the client.
            if (BonusJudge.Decode(action, context, effect.Param2) is not { } result)
            {
                var combo = ActionManager.Instance()->Combo;
                log.Warning("Bonus FlyText: unmatched bonus; Action={Action}; Job={Job}; Level={Level}; Bonus={Bonus}; States={States}; PreviousAction={Combo}/{Timer:F2}; Coeurl={Coeurl}; Kazematoi={Kaze}; Statuses={Statuses}",
                    action.ActionId, tableKey.Job, tableKey.Level, effect.Param2, BonusJudge.Describe(action, context), combo.Action, combo.Timer,
                    context.MonkCoeurlStacks, context.NinjaKazematoi, string.Join(',', context.Statuses));
                return;
            }
            // Priority chosen by the user: a missed combo overrides the positional outcome.
            Outcome? outcome = null;
            if (config.ComboFailFlyText && result.ComboApplied == false) outcome = Outcome.ComboMissed;
            else if (result.PositionalHit is { } hit && (hit ? config.PositionalHitEnabled : config.PositionalMissEnabled))
                outcome = hit ? Outcome.PositionalHit : Outcome.PositionalMiss;
            if (outcome is { } value) pending.Add(new(names[action.ActionId], damage, effect.Param2, value, Environment.TickCount64));
            return;
        }
    }

    private static BonusContext ReadContext(IPlayerCharacter player)
    {
        var statuses = player.StatusList.Where(status => status.StatusId != 0).Select(status => status.StatusId).ToHashSet();
        var gauges = JobGaugeManager.Instance();
        var job = player.ClassJob.RowId;
        return new(statuses, job is 2 or 20 ? gauges->Monk.CoeurlStacks : 0, job is 29 or 30 ? gauges->Ninja.Kazematoi : 0);
    }

    private void OnFlyText(ref FlyTextKind kind, ref int value1, ref int value2, ref SeString text1, ref SeString text2,
        ref uint color, ref uint icon, ref uint damageTypeIcon, ref float yOffset, ref bool handled)
    {
        if (!active || pending.Count == 0) return;
        if (kind is not (FlyTextKind.Damage or FlyTextKind.DamageCrit or FlyTextKind.DamageDh or FlyTextKind.DamageCritDh)) return;
        var name = text1?.TextValue;
        var now = Environment.TickCount64;
        var damage = value1; var bonus = value2;
        // Same action name, damage and bonus byte from our own result within the window: no other source can match.
        var index = pending.FindIndex(entry => entry.Name == name && entry.Damage == damage && entry.Bonus == bonus
            && now - entry.Tick <= MatchWindowMs);
        if (index < 0) return;
        var outcome = pending[index].Outcome;
        pending.RemoveAt(index);
        var (colorOn, rgb, iconOn, mark) = outcome switch
        {
            Outcome.PositionalHit => (config.PositionalHitColorEnabled, config.PositionalHitColor, config.PositionalHitIconEnabled, config.PositionalHitIcon),
            Outcome.PositionalMiss => (config.PositionalMissColorEnabled, config.PositionalMissColor, config.PositionalMissIconEnabled, config.PositionalMissIcon),
            _ => (config.ComboFailColorEnabled, config.ComboFailColor, config.ComboFailIconEnabled, config.ComboFailIcon),
        };
        if (colorOn) color = ToFlyTextColor(rgb);
        // Native inline icon after the action name (game bitmap font icons).
        if (iconOn && mark != 0 && text1 is not null)
            text1 = new SeStringBuilder().Append(text1).AddText(" ").AddIcon((BitmapFontIcon)mark).Build();
    }

    public void Dispose()
    {
        framework.Update -= Update;
        flyText.FlyTextCreated -= OnFlyText;
        hook?.Dispose();
        pending.Clear();
        table.Clear();
    }
}
