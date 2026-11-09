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
        public List<HavokControl> Havok = [];
        public readonly Dictionary<nint, long> HavokRestart = [];
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
        ObserveHavok(role, character, track, now);
        if (track.Emote != 0 && now - track.LastSnapshot >= 1000)
        {
            track.LastSnapshot = now;
            var active = Enumerable.Range(0, Slots).Where(slot => track.Timeline[slot] != 0)
                .Select(slot => $"{slot}:{track.Timeline[slot]}@{track.Time[slot]:F2}");
            log.Information("[EmoteProbe] {Role} snapshot emote={Emote} loop={Loop} slots=[{Slots}] havok=[{Havok}] t={Tick}",
                role, track.Emote, track.Loop, string.Join(' ', active), string.Join(' ', track.Havok.Select(h => h.Describe())), now);
        }
    }

    private sealed record HavokControl(nint Address, float Duration, float LocalTime, float Speed, uint Overflow, float Weight)
    {
        public string Describe() => $"{Address:X}:{LocalTime:F3}/{Duration:F3}x{Speed:F2} ov{Overflow} w{Weight:F2}";
    }

    /// <summary>
    /// Body skeleton (partial 0, animated skeleton 0) Havok animation controls: the real looping playback state.
    /// Logs a control's loop overflow (LocalTime wrapped past Duration) with the measured period.
    /// </summary>
    private void ObserveHavok(string role, Character* character, Track track, long now)
    {
        var current = new List<HavokControl>();
        var draw = character->GameObject.DrawObject;
        if (draw is not null && draw->Object.GetObjectType() == FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType.CharacterBase)
        {
            var skeleton = ((FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase*)draw)->Skeleton;
            if (skeleton is not null && skeleton->PartialSkeletonCount > 0)
            {
                var animated = skeleton->PartialSkeletons[0].GetHavokAnimatedSkeleton(0);
                if (animated is not null)
                    for (var i = 0; i < animated->AnimationControls.Length; i++)
                    {
                        var control = animated->AnimationControls.Data[i].Value;
                        if (control is null) continue;
                        var binding = control->hkaAnimationControl.Binding.ptr;
                        var animation = binding is null ? null : binding->Animation.ptr;
                        current.Add(new((nint)control, animation is null ? -1 : animation->Duration, control->hkaAnimationControl.LocalTime,
                            control->PlaybackSpeed, control->OverflowCount, control->hkaAnimationControl.Weight));
                    }
            }
        }
        foreach (var control in current)
        {
            var previous = track.Havok.FirstOrDefault(old => old.Address == control.Address);
            if (previous is null)
                log.Information("[EmoteProbe] {Role} havok add {Control} emote={Emote} t={Tick}", role, control.Describe(), track.Emote, now);
            else if (control.Overflow != previous.Overflow || control.LocalTime + 0.05f < previous.LocalTime)
            {
                track.HavokRestart.TryGetValue(control.Address, out var last);
                log.Information("[EmoteProbe] {Role} havok loop {Control} prev={Prev:F3} periodMs={Period} emote={Emote} t={Tick}",
                    role, control.Describe(), previous.LocalTime, last == 0 ? -1 : now - last, track.Emote, now);
                track.HavokRestart[control.Address] = now;
            }
        }
        foreach (var gone in track.Havok.Where(old => current.All(c => c.Address != old.Address)))
            log.Information("[EmoteProbe] {Role} havok remove {Control} t={Tick}", role, gone.Describe(), now);
        track.Havok = current;
    }

    public void Dispose()
    {
        if (Enabled) framework.Update -= OnUpdate;
        tracks.Clear();
    }
}
#endif
