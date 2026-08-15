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

    public static int EffectiveIntervalTicks(int baseIntervalTicks, int mastery)
    {
        var reduction = Math.Min(MaxIntervalReduction, mastery * IntervalReductionPerMastery);
        var effective = (int)Math.Round(baseIntervalTicks * (1.0 - reduction));
        return Math.Max(MinimumIntervalTicks, effective);
    }

    public static double MasteryBonusChance(int mastery) =>
        Math.Min(MaxMasteryBonusChance, mastery * BonusChancePerMastery);

    /// <summary>
    /// Active-play performance [0,1] from a timing-bar position [0,1]: 1.0 dead-centre,
    /// falling linearly to 0 at the edges. The scoring rule lives in Core so every UI
    /// (and tests) share it rather than reimplementing the curve in the client.
    /// </summary>
    public static double TimingPerformance(double position) =>
        Math.Clamp(1.0 - Math.Abs(position - 0.5) * 2.0, 0.0, 1.0);
}
