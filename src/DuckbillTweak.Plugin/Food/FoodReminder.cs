using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Food;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace DuckbillTweak.Plugin.Food;

/// <summary>Framework-only observation; retains managed values in memory for the current character/territory.</summary>
internal sealed unsafe class FoodReminder : IDisposable
{
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IObjectTable objects;
    private readonly IPartyList party;
    private readonly ICondition conditions;
    private readonly IChatGui chat;
    private readonly IToastGui toast;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly Dictionary<uint, FoodDuty> duties;
    private ulong character;
    private uint territory;
    private long lastRead;
    private bool notified;
    private float lastCountdown;
    private bool failed;
#if DEBUG
    private bool observationLogged;
    private string? lastCountdownDiagnostic;
#endif
    public IReadOnlyList<FoodMember> Members { get; private set; } = [];
    public int? MinimumMinutes { get; private set; }
    public bool ObservationFailed => failed;
    public bool Ready => config.FoodReminder && !failed && client.IsLoggedIn && objects.LocalPlayer is { } self &&
        self.GameObjectId == character && client.TerritoryType == territory && Environment.TickCount64 - lastRead < 1500;
    public string ObservationState => $"Enabled={config.FoodReminder}; Failed={failed}; LoggedIn={client.IsLoggedIn}; Members={Members.Count}; AgeMs={(lastRead == 0 ? -1 : Environment.TickCount64 - lastRead)}; Ready={Ready}";

    public FoodReminder(IFramework framework, IClientState client, IObjectTable objects, IPartyList party,
        ICondition conditions, IDataManager data, IChatGui chat, IToastGui toast, IPluginLog log, Configuration config)
    {
        this.framework = framework; this.client = client; this.objects = objects; this.party = party;
        this.conditions = conditions; this.chat = chat; this.toast = toast; this.log = log; this.config = config;
        duties = data.GetExcelSheet<ContentFinderCondition>().Where(c => c.TerritoryType.RowId != 0 && !c.PvP)
            .GroupBy(c => c.TerritoryType.RowId).ToDictionary(g => g.Key, g =>
            {
                var kinds = g.Select(c => FoodCheck.Classify(c.ContentType.RowId, c.ContentUICategory.RowId, c.Name.ExtractText())).Distinct().ToArray();
                return kinds.Length == 1 ? kinds[0] : FoodDuty.None;
            });
        framework.Update += Update;
    }

    private void Update(IFramework _)
    {
        if (!config.FoodReminder || !client.IsLoggedIn || client.IsPvP ||
            conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51] || objects.LocalPlayer is not { } self)
        { Clear(); failed = false; return; }
        if (character != self.GameObjectId || territory != client.TerritoryType)
        { Clear(); failed = false; character = self.GameObjectId; territory = client.TerritoryType; }
        if (failed) return;
        try
        {
            var countdown = AgentCountDownSettingDialog.Instance();
            var remaining = countdown is not null && countdown->Active && float.IsFinite(countdown->TimeRemaining) &&
                countdown->TimeRemaining is > 0 and <= 60 ? countdown->TimeRemaining : 0;
            if (remaining == 0 || remaining > lastCountdown + 0.5f) notified = false;
            var began = remaining > 0 && (lastCountdown == 0 || remaining > lastCountdown + 0.5f);
            lastCountdown = remaining;
            if (began || Environment.TickCount64 - lastRead >= 500)
            {
                ReadMembers();
                MinimumMinutes = duties.GetValueOrDefault(territory) switch
                {
                    FoodDuty.Extreme => Math.Clamp(config.FoodExtremeMinutes, 1, 60),
                    FoodDuty.Savage => Math.Clamp(config.FoodSavageMinutes, 1, 60),
                    FoodDuty.Ultimate => Math.Clamp(config.FoodUltimateMinutes, 1, 60),
                    _ => null,
                };
                lastRead = Environment.TickCount64;
#if DEBUG
                if (!observationLogged)
                {
                    log.Information("Food observation active: Members={Members}; Known={Known}; Territory={Territory}", Members.Count, Members.Count(m => m.Seconds is not null), territory);
                    observationLogged = true;
                }
#endif
            }
            TraceCountdown(countdown, remaining);
            if (remaining <= 0 || notified || conditions[ConditionFlag.InCombat] || MinimumMinutes is not { } minutes) return;
            var low = Members.Where(m => m.IsLow(minutes)).ToArray();
            if (low.Length == 0) return;
            notified = true;
            var details = string.Join(", ", low.Select(m => $"{m.Name} ({m.Display})"));
            var unknown = Members.Count(m => m.Seconds is null);
            chat.Print($"음식 {minutes}분 미만: {details}" + (unknown > 0 ? $" · {unknown}명 미확인" : ""), "DuckbillTweak");
            // Public toast API, no hooks or server chat. Keep the centre readable even with eight low members.
            try { toast.ShowError($"음식 {minutes}분 미만 {low.Length}명 · 내 채팅창 확인"); }
            catch (Exception exception) { log.Warning(exception, "Food centre warning unavailable; chat summary shown"); }
        }
        catch (Exception exception)
        {
            Clear(); character = self.GameObjectId; territory = client.TerritoryType; failed = true;
            log.Error(exception, "Food reminder disabled for this session; toggle off/on to retry");
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void TraceCountdown(AgentCountDownSettingDialog* countdown, float remaining)
    {
#if DEBUG
        var diagnostic = $"Agent={countdown is not null}; Active={countdown is not null && countdown->Active}; Showing={countdown is not null && countdown->ShowingCountdown}; Running={remaining > 0}; Territory={territory}; Duty={duties.GetValueOrDefault(territory)}; Minimum={MinimumMinutes?.ToString() ?? "none"}; InCombat={conditions[ConditionFlag.InCombat]}; Members={Members.Count}; Known={Members.Count(m => m.Seconds is not null)}; Low={(MinimumMinutes is { } minutes ? Members.Count(m => m.IsLow(minutes)) : -1)}; Notified={notified}";
        if (lastCountdownDiagnostic == diagnostic) return;
        lastCountdownDiagnostic = diagnostic;
        log.Information("Food countdown: {State}; Timer={Time:F2}; Previous={Previous:F2}; CountdownAddon={Addon}", diagnostic,
            countdown is null ? 0 : countdown->TimeRemaining, countdown is null ? 0 : countdown->PrevTimeRemaining, countdown is null ? 0 : countdown->CountdownAddonId);
#endif
    }

    private void ReadMembers()
    {
        var next = new List<FoodMember>();
        foreach (var member in party)
        {
            // A missing actor or an actor in another territory cannot prove that food is absent.
            var actor = member.Territory.RowId == territory && member.EntityId != 0 ? objects.SearchByEntityId(member.EntityId) as IPlayerCharacter : null;
            if (actor is not null && (!actor.IsValid() || actor.Name.TextValue != member.Name.TextValue)) actor = null;
            next.Add(new(member.ContentId, member.Name.TextValue, ReadFood(actor)));
        }
        if (next.Count == 0 && objects.LocalPlayer is { } self)
            next.Add(new(0, self.Name.TextValue, ReadFood(self)));
        Members = next;
    }

    private static double? ReadFood(IBattleChara? actor)
    {
        if (actor is null || !actor.IsValid()) return null;
        foreach (var status in actor.StatusList)
            if (status.StatusId == 48)
                return float.IsFinite(status.RemainingTime) && status.RemainingTime is >= 0 and < int.MaxValue - 60 ? status.RemainingTime : null;
        return 0;
    }

    private void Clear()
    {
        Members = []; MinimumMinutes = null; character = 0; territory = 0; lastRead = 0; notified = false; lastCountdown = 0;
#if DEBUG
        observationLogged = false;
        lastCountdownDiagnostic = null;
#endif
    }
    public void Dispose() { framework.Update -= Update; Clear(); }
}
