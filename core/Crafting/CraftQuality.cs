using Dungeons.Professions;

namespace Dungeons.Crafting;

/// <summary>
/// Execution quality (docs/emergent-item-system.md §7.4).
///
/// <para>Quality's most important effect is <b>narrowing variance</b> (§12.3), not adding a
/// flat bonus — mastery means <i>control</i>, which is a far better progression fantasy for
/// this system than "+5% stats". A master reliably hits the material they aimed at; a novice
/// scatters across neighbouring buckets and finds things by accident.</para>
///
/// <para>§21 leaves open how much the timing minigame should move this relative to profession
/// level — that is the choice between crafting <i>skill</i> and crafting <i>play</i> being the
/// dominant lever. The weights below are a first pass with them roughly balanced.</para>
/// </summary>
public static class CraftQuality
{
    /// <summary>Floor, so a level-1 crafter is clumsy rather than incapable.</summary>
    public const double Baseline = 0.10;

    public const double LevelWeight = 0.40;
    public const double PerformanceWeight = 0.35;

    /// <summary>How strongly an unpredictable material fights the crafter (§7.4).</summary>
    public const double InstabilityPenaltyDivisor = 150.0;

    /// <summary>
    /// Normalized execution quality, 0–1.
    /// </summary>
    /// <param name="professionLevel">Level in the gating profession; 0 for an ungated process.</param>
    /// <param name="effectiveInstability">The material's instability including wear (§6.2b).</param>
    /// <param name="performance">Active-crafting timing result, 0–1; 0.5 for a passive craft.</param>
    public static double Norm(int professionLevel, double effectiveInstability, double performance = 0.5)
    {
        var level = Math.Clamp(professionLevel, 0, ProfessionLeveling.MaxLevel) / (double)ProfessionLeveling.MaxLevel;

        return Math.Clamp(
            Baseline
            + LevelWeight * level
            + PerformanceWeight * Math.Clamp(performance, 0.0, 1.0)
            - effectiveInstability / InstabilityPenaltyDivisor,
            0.0,
            1.0);
    }
}
