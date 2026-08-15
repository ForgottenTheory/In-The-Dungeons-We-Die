namespace Dungeons.Content;

/// <summary>
/// Tuning for the base <see cref="MaterialProfile"/> derived for authored materials. The
/// ~470-material library was written before the emergent system existed and carries no
/// potency/integrity, so both are derived from data each material already has — its rarity
/// tag, its state tag, and its own property profile. Every value here is a first pass and
/// expected to be tuned; any single material can be overridden in JSON
/// (<c>potency</c> / <c>integrity</c>) when a derived value is wrong.
/// </summary>
public static class MaterialProfileTuning
{
    // ---- Potency (§6.1) ----------------------------------------------------------------
    //
    // potency = Floor + Slope × expressiveness + rarity bonus
    //
    // Expressiveness leans on the material's own numbers rather than on its rarity, so a
    // strong common material (Granite) out-potencies a weak rare one (Ember Sap). That is
    // §6.1's stated intent — "a high-potency mundane material beats a low-potency exotic
    // one" — and it is what keeps base resources economically relevant forever. Rarity is
    // therefore only a modest thumb on the scale, never the driver.

    /// <summary>Potency floor before expressiveness and rarity are added.</summary>
    public const double PotencyFloor = 20.0;

    /// <summary>How much of a material's expressiveness becomes potency.</summary>
    public const double PotencySlope = 0.30;

    /// <summary>Weight of the strongest expressive property in the expressiveness blend.</summary>
    public const double PeakWeight = 0.70;

    /// <summary>Weight of the second-strongest, so a broad profile beats a one-trick one.</summary>
    public const double SecondPeakWeight = 0.30;

    /// <summary>Rarity's contribution to potency — availability nudges power, never sets it.</summary>
    public static readonly IReadOnlyDictionary<string, double> PotencyByRarity =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["common"] = 0.0,
            ["uncommon"] = 3.0,
            ["rare"] = 6.0,
            ["very_rare"] = 11.0,
            ["exceptional"] = 16.0,
        };

    /// <summary>Potency for a material carrying no recognised rarity tag.</summary>
    public const double DefaultRarityBonus = 0.0;

    // ---- Integrity (§6.2) --------------------------------------------------------------
    //
    // Integrity is the remaining transformation budget. An authored material that has
    // already been worked (an ingot, an extract) has spent some of it; a raw one has not.

    /// <summary>Remaining transformation budget by <c>state:</c> tag.</summary>
    public static readonly IReadOnlyDictionary<string, int> IntegrityByState =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["raw"] = 100,
            ["refined"] = 90,
            ["processed"] = 90,
            ["alloy"] = 85,
            ["extract"] = 85,
            ["distillate"] = 85,
            ["composite"] = 85,
            ["spent"] = 60,
        };

    /// <summary>Integrity for a material carrying no recognised state tag.</summary>
    public const int DefaultIntegrity = 100;
}
