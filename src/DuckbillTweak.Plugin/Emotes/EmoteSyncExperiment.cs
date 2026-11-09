#if DEBUG
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace DuckbillTweak.Plugin.Emotes;

/// <summary>
/// Research-only experiment (Debug builds, "/dbtw emotesync [leadMs]"): waits until the target's looping emote is
/// leadMs before its next wrap, executes the same emote once, then logs the delay until the player's own loop starts
/// and the phase difference to the target for a few loops. Used to calibrate the real feature.
/// </summary>
internal sealed unsafe class EmoteSyncExperiment(IFramework framework, IObjectTable objects, ITargetManager targets,
    IChatGui chat, IPluginLog log) : IDisposable
{
    private enum Phase { Idle, Waiting, Fired, Measuring }

    private Phase phase;
    private ulong targetId;
    private ushort emoteId;
    private int leadMs;
    private bool armed;
    private long started, firedAt, selfEmoteAt, selfLoopAt, lastSample;
    private float targetPeriod;
    private readonly List<float> differences = [];

    public void Start(int lead)
    {
        Stop(null);
        if (targets.Target is not ICharacter target || objects.LocalPlayer is not { } self || target.Address == self.Address)
        { Report("대상(춤추는 다른 캐릭터)을 먼저 타겟하세요."); return; }
        var character = (Character*)target.Address;
        if (!character->EmoteController.IsInEmoteLoop() || character->EmoteController.EmoteId == 0)
        { Report("대상이 반복 감정표현 중이 아닙니다."); return; }
        emoteId = character->EmoteController.EmoteId;
        var manager = EmoteManager.Instance();
        if (manager is null || !manager->CanExecuteEmote(emoteId))
        { Report($"감정표현 {emoteId}을(를) 지금 실행할 수 없습니다."); return; }
        if (EmoteAnimation.Dominant(character) is not { } playback)
        { Report("대상의 애니메이션을 읽을 수 없습니다."); return; }
        targetId = target.GameObjectId;
        leadMs = Math.Max(0, lead);
        targetPeriod = playback.Duration;
        armed = false;
        differences.Clear();
        started = Environment.TickCount64;
        firedAt = selfEmoteAt = selfLoopAt = lastSample = 0;
        phase = Phase.Waiting;
        framework.Update += OnUpdate;
        log.Information("[EmoteSync] start emote={Emote} target=0x{Target:X} period={Period:F3} leadMs={Lead} targetTime={Time:F3}",
            emoteId, targetId, targetPeriod, leadMs, playback.LocalTime);
        Report($"동기화 실험 시작: 감정표현 {emoteId}, 주기 {targetPeriod:F2}초, 앞당김 {leadMs}ms");
    }

    private void OnUpdate(IFramework _)
    {
        try { Step(); }
        catch (Exception exception) { log.Error(exception, "[EmoteSync] step failed"); Stop("오류로 중단했습니다."); }
    }

    private void Step()
    {
        var now = Environment.TickCount64;
        if (objects.SearchById(targetId) is not ICharacter target || objects.LocalPlayer is not { } self) { Stop("대상을 잃어 중단했습니다."); return; }
        var targetCharacter = (Character*)target.Address;
        var selfCharacter = (Character*)self.Address;
        if (targetCharacter->EmoteController.EmoteId != emoteId) { Stop("대상이 감정표현을 바꿔 중단했습니다."); return; }
        if (EmoteAnimation.Dominant(targetCharacter) is not { } targetLoop) return;

        switch (phase)
        {
            case Phase.Waiting:
            {
                var lead = (leadMs % (int)(targetLoop.Duration * 1000)) / 1000f;
                // Arm only after seeing the target farther than the lead from its wrap, so we fire on the approach.
                if (targetLoop.Remaining > lead + 0.05f) armed = true;
                if (armed && targetLoop.Remaining <= lead)
                {
                    var ok = EmoteManager.Instance()->ExecuteEmote(emoteId, null);
                    firedAt = now;
                    phase = Phase.Fired;
                    log.Information("[EmoteSync] fire ok={Ok} targetTime={Time:F3} remainingMs={Remaining:F0} t={Tick}",
                        ok, targetLoop.LocalTime, targetLoop.Remaining * 1000, now);
                    if (!ok) Stop("감정표현 실행이 거부됐습니다.");
                }
                else if (now - started > 30_000) Stop("30초 안에 실행 시점을 찾지 못했습니다.");
                break;
            }
            case Phase.Fired:
            {
                if (selfEmoteAt == 0 && selfCharacter->EmoteController.EmoteId == emoteId)
                {
                    selfEmoteAt = now;
                    log.Information("[EmoteSync] self emote set delayMs={Delay} t={Tick}", now - firedAt, now);
                }
                if (selfEmoteAt != 0 && EmoteAnimation.Dominant(selfCharacter) is { } selfLoop
                    && Math.Abs(selfLoop.Duration - targetLoop.Duration) < 0.05f && selfLoop.Weight > 0.99f)
                {
                    // Back-compute when the player's loop began from its current LocalTime.
                    selfLoopAt = now - (long)(selfLoop.LocalTime / Math.Max(selfLoop.Speed, 0.01f) * 1000);
                    phase = Phase.Measuring;
                    log.Information("[EmoteSync] self loop running localTime={Time:F3} loopStartDelayMs={Delay} t={Tick}",
                        selfLoop.LocalTime, selfLoopAt - firedAt, now);
                }
                else if (now - firedAt > 8_000) Stop("실행 후 8초 안에 내 반복 동작을 찾지 못했습니다.");
                break;
            }
            case Phase.Measuring:
            {
                if (selfCharacter->EmoteController.EmoteId != emoteId) { Stop("내 감정표현이 끝나 중단했습니다."); return; }
                if (now - lastSample < 500 || EmoteAnimation.Dominant(selfCharacter) is not { } selfLoop) return;
                lastSample = now;
                var diff = EmoteAnimation.PhaseDifference(selfLoop.LocalTime, targetLoop.LocalTime, targetLoop.Duration);
                differences.Add(diff);
                log.Information("[EmoteSync] sample self={Self:F3} target={Target:F3} diffMs={Diff:F0} t={Tick}",
                    selfLoop.LocalTime, targetLoop.LocalTime, diff * 1000, now);
                if (differences.Count >= 20) Stop(null);
                break;
            }
        }
    }

    private void Stop(string? reason)
    {
        if (phase == Phase.Idle) return;
        framework.Update -= OnUpdate;
        if (differences.Count > 0)
        {
            var mean = differences.Average() * 1000;
            var spread = (differences.Max() - differences.Min()) * 1000;
            log.Information("[EmoteSync] result fireToLoopMs={Delay} meanDiffMs={Mean:F0} spreadMs={Spread:F0} samples={Count}",
                selfLoopAt - firedAt, mean, spread, differences.Count);
            Report($"동기화 실험 결과: 실행→내 반복 시작 {selfLoopAt - firedAt}ms, 위치 차이 평균 {mean:F0}ms(내가 앞서면 +), 흔들림 {spread:F0}ms");
        }
        if (reason is not null) { log.Information("[EmoteSync] stop {Reason}", reason); Report(reason); }
        phase = Phase.Idle;
    }

    private void Report(string message) => chat.Print(message, "DuckbillTweak");

    public void Dispose() => Stop(null);
}
#endif
