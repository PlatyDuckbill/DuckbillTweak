using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace DuckbillTweak.Plugin.Emotes;

/// <summary>Playback state of one Havok animation control (seconds).</summary>
internal readonly record struct LoopPlayback(nint Control, float LocalTime, float Duration, float Speed, float Weight, uint Overflow)
{
    /// <summary>Seconds until LocalTime wraps back to 0 at the current speed.</summary>
    public float Remaining => Speed > 0.01f ? (Duration - LocalTime) / Speed : float.PositiveInfinity;
}

/// <summary>
/// Reads the looping emote animation from the body skeleton (partial 0, animated skeleton 0). In game the scheduler
/// timeline timestamp stops after one pass, while the Havok control keeps the real loop phase (docs/PLANS.md 2026-11-10).
/// Framework thread only; reads, never writes.
/// </summary>
internal static unsafe class EmoteAnimation
{
    /// <summary>The dominant (highest weight) animation control, or null when the skeleton is not available.</summary>
    public static LoopPlayback? Dominant(Character* character)
    {
        if (character is null) return null;
        var draw = character->GameObject.DrawObject;
        if (draw is null || draw->Object.GetObjectType() != ObjectType.CharacterBase) return null;
        var skeleton = ((CharacterBase*)draw)->Skeleton;
        if (skeleton is null || skeleton->PartialSkeletonCount == 0) return null;
        var animated = skeleton->PartialSkeletons[0].GetHavokAnimatedSkeleton(0);
        if (animated is null) return null;
        LoopPlayback? best = null;
        for (var i = 0; i < animated->AnimationControls.Length; i++)
        {
            var control = animated->AnimationControls.Data[i].Value;
            if (control is null) continue;
            var binding = control->hkaAnimationControl.Binding.ptr;
            var animation = binding is null ? null : binding->Animation.ptr;
            if (animation is null || animation->Duration <= 0) continue;
            var playback = new LoopPlayback((nint)control, control->hkaAnimationControl.LocalTime, animation->Duration,
                control->PlaybackSpeed, control->hkaAnimationControl.Weight, control->OverflowCount);
            if (best is null || playback.Weight > best.Value.Weight) best = playback;
        }
        return best;
    }

    /// <summary>Signed phase difference a - b in seconds, wrapped to (-period/2, period/2].</summary>
    public static float PhaseDifference(float a, float b, float period)
    {
        var diff = (a - b) % period;
        if (diff > period / 2) diff -= period;
        if (diff <= -period / 2) diff += period;
        return diff;
    }
}
