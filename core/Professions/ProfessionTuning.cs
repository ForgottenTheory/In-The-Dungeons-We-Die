namespace Dungeons.Professions;

/// <summary>
/// Central balance constants for profession execution. Kept in one place so the
/// numbers can be tuned without hunting through logic. Placeholder values for the
/// vertical slice.
/// </summary>
public static class ProfessionTuning
{
    /// <summary>Lowest an effective interval may fall, preventing zero-time actions.</summary>
    public const int MinimumIntervalTicks = 5;

    /// <summary>Extra bonus-output chance at full active performance.</summary>
    public const double ActiveBonusChanceAtFullPerformance = 0.3;

    /// <summary>Extra XP fraction at full active performance.</summary>
    public const double ActiveXpBonusAtFullPerformance = 0.5;

    /// <summary>Mastery points granted per completed action.</summary>
    public const int MasteryPerAction = 1;

    /// <summary>XP fraction kept when a success roll misses. Not zero: the hunter learned
    /// something from the miss, and a run of bad luck should not feel like lost time.</summary>
    public const double MissedAttemptXpFraction = 0.25;

    // --- Opportunities (active only) ----------------------------------------

    /// <summary>How strongly active performance scales an opportunity's base discovery chance.
    /// At performance 0 the base chance still applies, so an unskilled active attempt can
    /// still stumble onto something; at 1 it is worth this much more.</summary>
    public const double OpportunityChancePerformanceScale = 1.0;

    // --- Offline progression -------------------------------------------------

    /// <summary>
    /// Longest stretch of absence that pays out, in ticks (20 ticks = 1 second, so this is
    /// 12 hours). Leaving for a week should not bank a week; the cap is what keeps offline
    /// progress a convenience rather than the optimal way to play.
    /// </summary>
    public const long MaxOfflineTicks = 12 * 60 * 60 * TicksPerSecond;

    /// <summary>Hard ceiling on completions resolved for one absence, so a long interval-1
    /// action cannot spin for minutes on load.</summary>
    public const int MaxOfflineCompletions = 20_000;

    /// <summary>Simulation ticks per real second. Mirrors the client's tick rate; offline
    /// progress is the one place Core has to convert wall-clock time into ticks.</summary>
    public const int TicksPerSecond = 20;

    /// <summary>
    /// An interval after <paramref name="reduction"/> has been taken off it, floored so no action
    /// can ever cost nothing.
    ///
    /// <para>The <em>magnitude</em> of the reduction is content now
    /// (<see cref="MasteryBenefitKind.IntervalReduction"/>); what stays here is the rounding and
    /// the floor, which are rules rather than balance.</para>
    /// </summary>
    public static int EffectiveIntervalTicks(int baseIntervalTicks, double reduction)
    {
        var effective = (int)Math.Round(baseIntervalTicks * (1.0 - Math.Clamp(reduction, 0.0, 1.0)));
        return Math.Max(MinimumIntervalTicks, effective);
    }

    /// <summary>
    /// Chance for one active attempt to surface an opportunity with base chance
    /// <paramref name="baseChance"/>, given what mastery already adds.
    ///
    /// <para>Passive never calls this — the discovery roll happens only on the active path, which
    /// is what makes "fewer rare outcomes" structural rather than a tuning number.</para>
    /// </summary>
    public static double OpportunityDiscoveryChance(double baseChance, double masteryBonus, double performance)
    {
        var fromPerformance = baseChance * (1.0 + (performance * OpportunityChancePerformanceScale));
        return Math.Clamp(fromPerformance + masteryBonus, 0.0, 1.0);
    }

    /// <summary>Effective risk of pursuing, after experience has talked it down.</summary>
    public static double EffectiveRisk(double riskWeight, double riskReduction) =>
        Math.Clamp(riskWeight * (1.0 - Math.Clamp(riskReduction, 0.0, 1.0)), 0.0, 1.0);

    /// <summary>Ticks elapsed during an absence of <paramref name="seconds"/> real seconds,
    /// clamped to <see cref="MaxOfflineTicks"/>.</summary>
    public static long OfflineTicks(double seconds)
    {
        if (seconds <= 0)
            return 0;
        var ticks = (long)Math.Floor(seconds * TicksPerSecond);
        return Math.Min(MaxOfflineTicks, ticks);
    }

    /// <summary>
    /// Active-play performance [0,1] from a timing-bar position [0,1]: 1.0 dead-centre,
    /// falling linearly to 0 at the edges. The scoring rule lives in Core so every UI
    /// (and tests) share it rather than reimplementing the curve in the client.
    /// </summary>
    public static double TimingPerformance(double position) =>
        Math.Clamp(1.0 - Math.Abs(position - 0.5) * 2.0, 0.0, 1.0);
}
