namespace Dungeons.Content;

/// <summary>
/// Tuning for the base <see cref="MaterialState"/> derived for authored materials. The
/// ~470-material library was written before the emergent system existed and carries no
/// material strength/workability, so both are derived from data each material already has — its rarity
/// tag, its state tag, and its own property profile. Every value here is a first pass and
/// expected to be tuned; any single material can be overridden in JSON
/// (<c>material strength</c> / <c>workability</c>) when a derived value is wrong.
/// </summary>
public static class MaterialStateTuning
{
    // ---- MaterialStrength (§6.1) ----------------------------------------------------------------
    //
    // material strength = Floor + Slope × expressiveness + rarity bonus
    //
    // Expressiveness leans on the material's own numbers rather than on its rarity, so a
    // strong common material (Granite) out-potencies a weak rare one (Ember Sap). That is
    // §6.1's stated intent — "a high-material strength mundane material beats a low-materialStrength exotic
    // one" — and it is what keeps base resources economically relevant forever. Rarity is
    // therefore only a modest thumb on the scale, never the driver.

    /// <summary>MaterialStrength floor before expressiveness and rarity are added.</summary>
    public const double MaterialStrengthFloor = 20.0;

    /// <summary>How much of a material's expressiveness becomes material strength.</summary>
    public const double MaterialStrengthSlope = 0.30;

    /// <summary>Weight of the strongest expressive property in the expressiveness blend.</summary>
    public const double PeakWeight = 0.70;

    /// <summary>Weight of the second-strongest, so a broad profile beats a one-trick one.</summary>
    public const double SecondPeakWeight = 0.30;

    /// <summary>Rarity's contribution to material strength — availability nudges power, never sets it.</summary>
    public static readonly IReadOnlyDictionary<string, double> MaterialStrengthByRarity =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["common"] = 0.0,
            ["uncommon"] = 3.0,
            ["rare"] = 6.0,
            ["very_rare"] = 11.0,
            ["exceptional"] = 16.0,
        };

    /// <summary>MaterialStrength for a material carrying no recognised rarity tag.</summary>
    public const double DefaultRarityBonus = 0.0;

    // ---- Workability (§6.2) --------------------------------------------------------------
    //
    // Workability is the remaining transformation budget. An authored material that has
    // already been worked (an ingot, an extract) has spent some of it; a raw one has not.

    /// <summary>
    /// Remaining transformation budget by <c>state:</c> tag. Every value in
    /// <see cref="TagFamilies.State"/> must appear here — a missing one silently falls back to
    /// <see cref="DefaultWorkability"/>, i.e. reads as untouched, which is the wrong answer for
    /// anything that has been worked. <c>AuthoredMaterialTests</c> pins the coverage.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> WorkabilityByState =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["raw"] = 100,
            ["refined"] = 90,
            ["processed"] = 90,
            ["alloy"] = 85,
            ["extract"] = 85,
            ["distillate"] = 85,
            ["composite"] = 85,

            // Attuning drives essence into a material and leaves the least room of anything
            // short of spent. Added when Runecrafting's authored runes became the first
            // *authored* material to carry the tag — until then only the Attune craftingAction
            // set it, and derived materials take their budget from the chain, not this table.
            ["attuned"] = 75,

            ["spent"] = 60,
        };

    /// <summary>Workability for a material carrying no recognised state tag.</summary>
    public const int DefaultWorkability = 100;
}
