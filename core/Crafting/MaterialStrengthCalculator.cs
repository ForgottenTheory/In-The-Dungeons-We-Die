using Dungeons.Content;

namespace Dungeons.Crafting;

/// <summary>
/// MaterialStrength — the coefficient that scales how strongly a material's properties express when it
/// is used (docs/emergent-item-system.md §6.1).
///
/// <para>Two rules carry the whole design. MaterialStrength is a <b>weighted mean, never a sum</b>, so
/// adding a junk input <i>lowers</i> it and the "feed everything into one item until
/// arithmetic gives up" loop is dead on arrival. And it is capped just above the best input,
/// so refinement can improve a material a little but cannot conjure quality from nothing.</para>
///
/// <para>The consequence worth stating: a high-material strength mundane material beats a low-material strength
/// exotic one, so base resources stay economically relevant forever.</para>
/// </summary>
public static class MaterialStrengthCalculator
{
    /// <summary>
    /// The material strength of a craft's result.
    /// </summary>
    /// <param name="substrateStrength">MaterialStrength of the thing being transformed.</param>
    /// <param name="reagentPotencies">Potencies of the applied reagents; averaged, so adding
    /// more reagents cannot raise material strength on its own.</param>
    /// <param name="catalystStrength">MaterialStrength of the catalyst, or null when none is slotted.</param>
    /// <param name="weights">The crafting action's role weights, summing to 1.0.</param>
    /// <param name="qualityMultiplier">Execution quality, 0.85–1.12 (§7.4).</param>
    /// <param name="craftQuality">Normalized execution quality, 0–1; raises the ceiling.</param>
    public static int Compute(
        int substrateStrength,
        IReadOnlyList<int> reagentPotencies,
        int? catalystStrength,
        RoleWeights weights,
        double qualityMultiplier,
        double craftQuality)
    {
        ArgumentNullException.ThrowIfNull(reagentPotencies);
        ArgumentNullException.ThrowIfNull(weights);

        var reagentMean = reagentPotencies.Count == 0 ? 0.0 : reagentPotencies.Average();

        // An empty slot contributes zero rather than being renormalized away, so leaving the
        // catalyst slot empty is a small real cost. That is what gives catalysts a job in P1,
        // where they transfer no properties of their own.
        var weighted = substrateStrength * weights.Substrate
            + reagentMean * weights.Reagent
            + (catalystStrength ?? 0) * weights.Catalyst;

        var result = weighted * qualityMultiplier;
        var ceiling = Ceiling(substrateStrength, reagentPotencies, catalystStrength, craftQuality);

        return (int)Math.Clamp(
            Math.Round(Math.Min(result, ceiling), MidpointRounding.AwayFromZero),
            RefinementTuning.MinMaterialStrength,
            RefinementTuning.MaxMaterialStrength);
    }

    /// <summary>
    /// The hard cap: the best input, plus a little for skill. Climbing from 40 to 90 therefore
    /// takes many generations — and the workability budget will not allow that many. That
    /// intersection is what closes the escalation loop (§6.1).
    /// </summary>
    public static double Ceiling(
        int substrateStrength,
        IReadOnlyList<int> reagentPotencies,
        int? catalystStrength,
        double craftQuality)
    {
        ArgumentNullException.ThrowIfNull(reagentPotencies);

        var best = substrateStrength;
        foreach (var materialStrength in reagentPotencies)
            best = Math.Max(best, materialStrength);
        if (catalystStrength is { } catalyst)
            best = Math.Max(best, catalyst);

        return best + RefinementTuning.MaterialStrengthCeilingBonus * Math.Clamp(craftQuality, 0.0, 1.0);
    }

    /// <summary>Execution quality as a multiplier, 0.85–1.12 (§7.4).</summary>
    public static double QualityMultiplier(double craftQuality) =>
        RefinementTuning.QualityMultiplierBase
        + RefinementTuning.QualityMultiplierScale * Math.Clamp(craftQuality, 0.0, 1.0);
}
