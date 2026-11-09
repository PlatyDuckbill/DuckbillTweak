namespace DuckbillTweak.Core.Emotes;

/// <summary>
/// Timing for emote sync. The target's loop phase comes from its Havok animation control; "lead" is the measured
/// time from executing our emote to our own loop restarting at phase 0 (start segment + client delay). In game the
/// start segment replays on every execution and keeps the same length (docs/PLANS.md 2026-11-10).
/// </summary>
public static class EmoteSyncTiming
{
    /// <summary>
    /// Seconds from now until we should execute so that our loop starts exactly on one of the target's wraps,
    /// never sooner than <paramref name="minimumDelay"/>.
    /// </summary>
    /// <param name="remaining">Seconds until the target's next wrap.</param>
    /// <param name="period">Target loop period in seconds.</param>
    /// <param name="lead">Seconds from our execution to our loop phase 0.</param>
    public static double DelayUntilFire(double remaining, double period, double lead, double minimumDelay = 0)
    {
        if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period));
        var delay = remaining - lead;
        if (delay < minimumDelay) delay += Math.Ceiling((minimumDelay - delay) / period) * period;
        return delay;
    }

    /// <summary>Signed phase difference a - b in seconds, wrapped to (-period/2, period/2]. Positive: a is ahead.</summary>
    public static double PhaseDifference(double a, double b, double period)
    {
        var diff = (a - b) % period;
        if (diff > period / 2) diff -= period;
        if (diff <= -period / 2) diff += period;
        return diff;
    }

    /// <summary>
    /// Lead after a result: ending <paramref name="difference"/> seconds ahead of the target means we fired too early,
    /// so the lead shrinks by that amount (and grows when we ended behind).
    /// </summary>
    public static double CorrectLead(double lead, double difference) => Math.Max(0, lead - difference);
}
