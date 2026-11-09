using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using DuckbillTweak.Core.Emotes;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using LuminaEmote = Lumina.Excel.Sheets.Emote;

namespace DuckbillTweak.Plugin.Emotes;

/// <summary>
/// "따라하기": right-click a character in a looping emote and execute the same emote so that our loop starts on one of
/// the target's loop restarts. The phase comes from the Havok animation control (EmoteAnimation); the lead (our start
/// segment + client delay) is measured per emote on first use by executing once, then the emote is executed again on
/// time. Every execution is a single user-requested emote; nothing repeats on its own after the session ends.
/// Allowed by AGENTS.md (automation inside a feature the user enabled; no server chat).
/// </summary>
internal sealed unsafe class EmoteFollow : IDisposable
{
    private enum Stage { Idle, Priming, Scheduled, Verifying }

    private const long TimeoutMs = 30_000;
    private const double Tolerance = 0.05;     // seconds; about one animation frame and a half
    private const double DefaultLead = 0.04;   // measured execution-to-loop delay without a start segment
    private const int MaxAttempts = 3;

    private readonly IContextMenu contextMenu;
    private readonly IFramework framework;
    private readonly IObjectTable objects;
    private readonly IClientState client;
    private readonly ICondition conditions;
    private readonly IDataManager data;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly Action save;

    private Stage stage;
    private ulong targetId;
    private ushort emoteId;
    private string emoteName = "";
    private double lead;
    private bool leadMeasured;
    private int attempts;
    private long started, firedAt, verifyFrom;
    private nint previousControl;
    private double previousDelay = double.NaN;
    private bool armed;
    private uint territory;
    private readonly List<double> samples = [];
    private Vector3 firedPosition;
    private bool toggleRetried;

    /// <summary>User setting: run ahead of our own screen by this much so other players see us on the beat.</summary>
    private double Offset => Math.Clamp(config.EmoteFollowOffsetMs, 0, 500) / 1000.0;

    public EmoteFollow(IContextMenu contextMenu, IFramework framework, IObjectTable objects, IClientState client,
        ICondition conditions, IDataManager data, IChatGui chat, IPluginLog log, Configuration config, Action save)
    {
        this.contextMenu = contextMenu; this.framework = framework; this.objects = objects; this.client = client;
        this.conditions = conditions; this.data = data; this.chat = chat; this.log = log; this.config = config; this.save = save;
        contextMenu.OnMenuOpened += OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (!config.EmoteFollow || args.MenuType != ContextMenuType.Default) return;
        if (args.Target is not MenuTargetDefault target || target.TargetObject is not ICharacter character) return;
        if (objects.LocalPlayer is not { } self || character.Address == self.Address) return;
        var native = (Character*)character.Address;
        var id = native->EmoteController.EmoteId;
        if (id == 0 || !native->EmoteController.IsInEmoteLoop()) return;
        if (data.GetExcelSheet<LuminaEmote>().GetRowOrDefault(id) is not { } emote || emote.Name.IsEmpty) return;
        var name = emote.Name.ExtractText();
        var manager = EmoteManager.Instance();
        var usable = manager is not null && manager->CanExecuteEmote(id);
        var objectId = character.GameObjectId;
        args.AddMenuItem(new MenuItem
        {
            Name = usable ? $"따라하기 · {name}" : $"따라하기 · {name} (사용 불가)",
            IsEnabled = usable,
            OnClicked = _ => Start(objectId, id, name),
        });
    }

    private void Start(ulong objectId, ushort id, string name)
    {
        Stop(null);
        targetId = objectId; emoteId = id; emoteName = name;
        leadMeasured = config.EmoteLeadMs.TryGetValue(id, out var ms);
        lead = leadMeasured ? ms / 1000.0 : DefaultLead;
        attempts = 0; samples.Clear(); armed = false; previousDelay = double.NaN; toggleRetried = false;
        started = Environment.TickCount64; territory = client.TerritoryType;
        framework.Update += OnUpdate;
        if (leadMeasured) stage = Stage.Scheduled;
        else
        {
            // First use of this emote: execute now to measure our start segment, then execute again on time.
            stage = Stage.Priming;
            if (!Fire()) return;
        }
        log.Information("Emote follow start: Emote={Emote}/{Name}; Lead={Lead:F3}; Measured={Measured}", emoteId, emoteName, lead, leadMeasured);
    }

    private void OnUpdate(IFramework _)
    {
        try { Step(); }
        catch (Exception exception) { log.Error(exception, "Emote follow failed"); Stop("따라하기 중 오류가 나서 멈췄습니다."); }
    }

    private void Step()
    {
        var now = Environment.TickCount64;
        if (!config.EmoteFollow) { Stop(null); return; }
        if (now - started > TimeoutMs) { Stop("시간 안에 맞추지 못해 따라하기를 멈췄습니다."); return; }
        if (client.TerritoryType != territory || conditions[ConditionFlag.InCombat]) { Stop("지역 이동이나 전투로 따라하기를 멈췄습니다."); return; }
        if (objects.SearchById(targetId) is not ICharacter target || objects.LocalPlayer is not { } self) { Stop("대상이 사라져 따라하기를 멈췄습니다."); return; }
        var targetNative = (Character*)target.Address;
        var selfNative = (Character*)self.Address;
        if (targetNative->EmoteController.EmoteId != emoteId || !targetNative->EmoteController.IsInEmoteLoop())
        { Stop("대상이 감정표현을 멈춰 따라하기를 멈췄습니다."); return; }
        if (EmoteAnimation.Dominant(targetNative) is not { } targetLoop) return;
        if (Math.Abs(targetLoop.Speed - 1) > 0.01f) { Stop("대상의 재생 속도가 달라 따라하지 않았습니다."); return; }

        switch (stage)
        {
            case Stage.Priming:
            case Stage.Verifying:
                if (selfNative->EmoteController.EmoteId != emoteId)
                {
                    var moved = Vector3.Distance(self.Position, firedPosition) > 0.05f;
                    // Some emotes end instead of restarting when executed again while playing. If we did not move,
                    // we are idle now: execute again (priming) or wait for the next beat from idle (verifying), once.
                    if (!moved && !toggleRetried && now - firedAt < 1500)
                    {
                        toggleRetried = true;
                        log.Information("Emote follow: emote {Emote} ended on re-execution; retrying from idle", emoteId);
                        if (stage == Stage.Priming) Fire();
                        else { stage = Stage.Scheduled; armed = false; previousDelay = double.NaN; }
                        return;
                    }
                    if (moved || now - firedAt > 500) { Stop("직접 움직이거나 다른 동작을 해서 따라하기를 멈췄습니다."); return; }
                    return;
                }
                if (EmoteAnimation.Dominant(selfNative) is not { } selfLoop) return;
                // Our new loop: a different control than before the execution, fully weighted, normal speed.
                var isNewLoop = selfLoop.Control != previousControl && selfLoop.Weight > 0.99f && Math.Abs(selfLoop.Speed - 1) < 0.01f;
                if (!isNewLoop) return;
                // Race-specific animations can differ in length; then only the start can be aligned.
                var sameLength = Math.Abs(selfLoop.Duration - targetLoop.Duration) < 0.05f;
                if (stage == Stage.Priming)
                {
                    var loopStart = now - selfLoop.LocalTime * 1000;
                    lead = Math.Max(0, (loopStart - firedAt) / 1000.0);
                    leadMeasured = true;
                    Remember();
                    stage = Stage.Scheduled; armed = false; previousDelay = double.NaN;
                    log.Information("Emote follow measured lead: Emote={Emote}; Lead={Lead:F3}", emoteId, lead);
                    return;
                }
                if (!sameLength)
                {
                    log.Information("Emote follow: loop length differs (self {Self:F3}s, target {Target:F3}s); start aligned only",
                        selfLoop.Duration, targetLoop.Duration);
                    Stop("종족마다 동작 길이가 달라 시작만 맞췄습니다. 반복할수록 조금씩 어긋날 수 있습니다.");
                    return;
                }
                if (verifyFrom == 0) verifyFrom = now + 300; // let the blend settle
                if (now < verifyFrom) return;
                samples.Add(EmoteSyncTiming.PhaseDifference(selfLoop.LocalTime, targetLoop.LocalTime, targetLoop.Duration));
                if (samples.Count < 3) { verifyFrom = now + 250; return; }
                // We intentionally run ahead by the user's offset (to look right on other players' screens).
                var difference = samples.Average() - Offset;
                lead = EmoteSyncTiming.CorrectLead(lead, difference);
                Remember();
                log.Information("Emote follow result: Emote={Emote}; DifferenceMs={Difference:F0}; OffsetMs={Offset}; Attempt={Attempt}; NewLead={Lead:F3}",
                    emoteId, difference * 1000, config.EmoteFollowOffsetMs, attempts, lead);
                if (Math.Abs(difference) <= Tolerance) { Stop(null); return; }
                if (attempts >= MaxAttempts) { Stop($"따라하기: {Math.Abs(difference) * 1000:F0}ms 차이가 남았습니다."); return; }
                stage = Stage.Scheduled; armed = false; previousDelay = double.NaN; samples.Clear();
                return;

            case Stage.Scheduled:
            {
                var delay = EmoteSyncTiming.DelayUntilFire(targetLoop.Remaining, targetLoop.Duration, lead + Offset);
                // Fire on the approach; a jump upward means the fire point passed between frames, so fire now (<= 1 frame late).
                var passed = !double.IsNaN(previousDelay) && delay > previousDelay + targetLoop.Duration / 2;
                previousDelay = delay;
                if (delay > 0.05 && !passed) { armed = true; return; }
                if (armed && (delay <= 0.008 || passed)) Fire();
                return;
            }
        }
    }

    private bool Fire()
    {
        var manager = EmoteManager.Instance();
        previousControl = objects.LocalPlayer is { } self && EmoteAnimation.Dominant((Character*)self.Address) is { } current ? current.Control : 0;
        if (manager is null || !manager->CanExecuteEmote(emoteId) || !manager->ExecuteEmote(emoteId, null))
        {
            Stop("지금은 그 감정표현을 실행할 수 없습니다.");
            return false;
        }
        firedAt = Environment.TickCount64;
        firedPosition = objects.LocalPlayer?.Position ?? Vector3.Zero;
        attempts++;
        verifyFrom = 0;
        samples.Clear();
        if (stage == Stage.Scheduled) stage = Stage.Verifying;
        return true;
    }

    private void Remember()
    {
        config.EmoteLeadMs[emoteId] = (int)Math.Round(lead * 1000);
        save();
    }

    private void Stop(string? message)
    {
        if (stage == Stage.Idle) return;
        framework.Update -= OnUpdate;
        stage = Stage.Idle;
        if (message is not null)
        {
            chat.Print(message, "DuckbillTweak");
            log.Information("Emote follow stopped: {Message}", message);
        }
    }

    public void Dispose()
    {
        contextMenu.OnMenuOpened -= OnMenuOpened;
        Stop(null);
    }
}
