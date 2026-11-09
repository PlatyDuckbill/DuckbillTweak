#if DEBUG
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace DuckbillTweak.Plugin.Emotes;

/// <summary>
/// Research-only probe for the emote sync plan (docs/PLANS.md), Debug builds only, toggled by "/dbtw emoteprobe".
/// Reads the local player's and the current target's EmoteController and ActionTimelineSequencer slots on the
/// framework thread and logs emote changes, slot timeline changes and loop restarts (timestamp going backwards)
/// with the measured period. Reads only; nothing is executed.
/// </summary>
internal sealed unsafe class EmoteProbe(IFramework framework, IObjectTable objects, ITargetManager targets, IPluginLog log) : IDisposable
{
    private const int Slots = 14;
    private sealed class Track
    {
        public ushort Emote;
        public bool Loop;
        public readonly ushort[] Timeline = new ushort[Slots];
        public readonly float[] Time = new float[Slots];
        public readonly long[] LastRestart = new long[Slots];
        public long LastSnapshot;
    }

    private readonly Dictionary<ulong, Track> tracks = [];
    public bool Enabled { get; private set; }

    public bool Toggle()
    {
        Enabled = !Enabled;
        if (Enabled) framework.Update += OnUpdate;
        else { framework.Update -= OnUpdate; tracks.Clear(); }
        log.Information("[EmoteProbe] {State}", Enabled ? "enabled" : "disabled");
        return Enabled;
    }

    private void OnUpdate(IFramework _)
    {
        try
        {
            if (objects.LocalPlayer is { } self) Observe("self", self);
            if (targets.Target is ICharacter target && target.Address != objects.LocalPlayer?.Address) Observe("target", target);
        }
        catch (Exception exception) { log.Error(exception, "[EmoteProbe] read failed"); }
    }

    private void Observe(string role, IGameObject actor)
    {
        var character = (Character*)actor.Address;
        if (character is null) return;
        var now = Environment.TickCount64;
        if (!tracks.TryGetValue(actor.GameObjectId, out var track)) tracks[actor.GameObjectId] = track = new();
        var emote = character->EmoteController.EmoteId;
        var loop = character->EmoteController.IsInEmoteLoop();
        if (emote != track.Emote || loop != track.Loop)
        {
            log.Information("[EmoteProbe] {Role} {Name} emote {Old}->{New} loop={Loop} t={Tick}", role, actor.Name.TextValue, track.Emote, emote, loop, now);
            track.Emote = emote; track.Loop = loop;
        }
        var sequencer = &character->Timeline.TimelineSequencer;
        for (var slot = 0; slot < Slots; slot++)
        {
            var timeline = sequencer->GetSlotTimeline((uint)slot);
            var scheduler = sequencer->GetSchedulerTimeline((uint)slot);
            var time = scheduler is null ? -1f : scheduler->CurrentTimestamp;
            if (timeline != track.Timeline[slot])
            {
                var key = scheduler is null ? "" : scheduler->ActionTimelineKey.ToString();
                log.Information("[EmoteProbe] {Role} slot{Slot} timeline {Old}->{New} key={Key} speed={Speed:F2} t={Tick}",
                    role, slot, track.Timeline[slot], timeline, key, sequencer->GetSlotSpeed((uint)slot), now);
                track.Timeline[slot] = timeline;
                track.LastRestart[slot] = now;
            }
            else if (timeline != 0 && time >= 0 && time + 0.05f < track.Time[slot])
            {
                // Timestamp went backwards: the looping timeline restarted.
                log.Information("[EmoteProbe] {Role} slot{Slot} restart timeline={Timeline} prevTime={Prev:F3} newTime={Time:F3} periodMs={Period} t={Tick}",
                    role, slot, timeline, track.Time[slot], time, now - track.LastRestart[slot], now);
                track.LastRestart[slot] = now;
            }
            track.Time[slot] = time;
        }
        if (track.Emote != 0 && now - track.LastSnapshot >= 1000)
        {
            track.LastSnapshot = now;
            var active = Enumerable.Range(0, Slots).Where(slot => track.Timeline[slot] != 0)
                .Select(slot => $"{slot}:{track.Timeline[slot]}@{track.Time[slot]:F2}");
            log.Information("[EmoteProbe] {Role} snapshot emote={Emote} loop={Loop} slots=[{Slots}] t={Tick}", role, track.Emote, track.Loop, string.Join(' ', active), now);
        }
    }

    public void Dispose()
    {
        if (Enabled) framework.Update -= OnUpdate;
        tracks.Clear();
    }
}
#endif
