namespace Dungeons.Professions;

/// <summary>
/// Central balance constants for profession execution. Kept in one place so the
/// numbers can be tuned without hunting through logic. Placeholder values for the
/// vertical slice.
/// </summary>
public static class ProfessionTuning
{
    /// <summary>Interval reduction per mastery point.</summary>
    public const double IntervalReductionPerMastery = 0.005;

    /// <summary>Maximum fraction the interval can be reduced by mastery.</summary>
    public const double MaxIntervalReduction = 0.5;

    /// <summary>Lowest an effective interval may fall, preventing zero-time actions.</summary>
    public const int MinimumIntervalTicks = 5;

    /// <summary>Bonus-output chance added per mastery point.</summary>
    public const double BonusChancePerMastery = 0.0025;

    /// <summary>Maximum bonus-output chance contributed by mastery.</summary>
    public const double MaxMasteryBonusChance = 0.25;

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

    /// <summary>Discovery chance added per mastery point in the action being performed.</summary>
    public const double OpportunityChancePerMastery = 0.001;

    /// <summary>Maximum discovery chance contributed by mastery.</summary>
    public const double MaxMasteryOpportunityChance = 0.10;

    /// <summary>How strongly active performance scales an opportunity's base discovery chance.
    /// At performance 0 the base chance still applies, so an unskilled active attempt can
    /// still stumble onto something; at 1 it is worth this much more.</summary>
    public const double OpportunityChancePerformanceScale = 1.0;

    /// <summary>Fraction of an opportunity's risk that high mastery can talk down.</summary>
    public const double MaxRiskReductionFromMastery = 0.5;

    /// <summary>Risk reduction per mastery point, before the cap above.</summary>
    public const double RiskReductionPerMastery = 0.002;

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

    public static int EffectiveIntervalTicks(int baseIntervalTicks, int mastery)
    {
        var reduction = Math.Min(MaxIntervalReduction, mastery * IntervalReductionPerMastery);
        var effective = (int)Math.Round(baseIntervalTicks * (1.0 - reduction));
        return Math.Max(MinimumIntervalTicks, effective);
    }

    public static double MasteryBonusChance(int mastery) =>
        Math.Min(MaxMasteryBonusChance, mastery * BonusChancePerMastery);

    /// <summary>
    /// Chance for one active attempt to surface <paramref name="baseChance"/>'s opportunity.
    /// Passive never calls this — the discovery roll happens only on the active path, which
    /// is what makes "fewer rare outcomes" structural rather than a tuning number.
    /// </summary>
    public static double OpportunityDiscoveryChance(double baseChance, int mastery, double performance)
    {
        var fromPerformance = baseChance * (1.0 + (performance * OpportunityChancePerformanceScale));
        var fromMastery = Math.Min(MaxMasteryOpportunityChance, mastery * OpportunityChancePerMastery);
        return Math.Clamp(fromPerformance + fromMastery, 0.0, 1.0);
    }

    /// <summary>Effective risk of pursuing, after mastery in the action has talked it down.</summary>
    public static double EffectiveRisk(double riskWeight, int mastery)
    {
        var reduction = Math.Min(MaxRiskReductionFromMastery, mastery * RiskReductionPerMastery);
        return Math.Clamp(riskWeight * (1.0 - reduction), 0.0, 1.0);
    }

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
