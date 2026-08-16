namespace Dungeons.Crafting;

/// <summary>
/// What a craft will cost and what it risks, computed <b>before</b> the player commits
/// (docs/emergent-item-system.md §6.2c). Integrity 0 destroys the material, and that rule is
/// only fair if destruction is never a surprise — so this projection is required scope, not
/// polish.
/// </summary>
/// <param name="ExpectedCost">Integrity the craft is expected to consume.</param>
/// <param name="CostSpread">Half-width of the cost's range, from variance (§12.3).</param>
/// <param name="ProjectedIntegrity">Integrity remaining at the expected cost.</param>
/// <param name="DestructionChance">Probability the craft destroys the material, 0–1.</param>
public sealed record IntegrityProjection(
    double ExpectedCost,
    double CostSpread,
    int ProjectedIntegrity,
    double DestructionChance)
{
    /// <summary>Destruction is unavoidable — the UI must warn explicitly rather than showing
    /// a percentage (§6.2c).</summary>
    public bool IsCertainDestruction => DestructionChance >= 1.0;

    /// <summary>There is real risk but not certainty; the UI shows a percentage (§6.2c).</summary>
    public bool IsAtRisk => DestructionChance is > 0.0 and < 1.0;
}

/// <summary>
/// Integrity — the transformation budget (docs/emergent-item-system.md §6.2). Explicitly
/// <i>not</i> durability, hardness, condition or stability: it is how much further structural
/// change the material can tolerate.
///
/// <para>Because it is monotonically non-increasing and reaching zero destroys the material,
/// every refinement step is a live question — push one more transmutation, or commit and
/// forge it now? That is the same shape as the extract-or-go-deeper decision the realm layer
/// already runs on.</para>
/// </summary>
public static class IntegrityCalculator
{
    /// <summary>
    /// Integrity consumed by one step (§6.2a). Cost scales with how violent the change was,
    /// not with how many times the player has crafted — so elegant paths are mechanically
    /// rewarded.
    /// </summary>
    /// <param name="stateDelta">Total normalized property movement (§6.2a).</param>
    /// <param name="severity">The process's severity.</param>
    /// <param name="strainReleased">Energy released by annihilation (§8.5).</param>
    /// <param name="qualityNorm">Normalized execution quality, 0–1.</param>
    /// <param name="traitsCreated">Always 0 in P1 — traits are P2.</param>
    /// <param name="extraCost">Signature-reaction surcharge. Always 0 in P1 — signatures are P4.</param>
    public static double Cost(
        double stateDelta,
        double severity,
        double strainReleased,
        double qualityNorm,
        int traitsCreated = 0,
        double extraCost = 0.0)
    {
        var gross = stateDelta * severity * RefinementTuning.StateDeltaCost
            + traitsCreated * RefinementTuning.TraitCost
            + strainReleased * RefinementTuning.StrainCost
            + extraCost;

        var mitigation = Math.Clamp(qualityNorm, 0.0, 1.0) * RefinementTuning.SkillMitigationFraction;
        return Math.Max(gross * (1.0 - mitigation), RefinementTuning.MinimumIntegrityCost);
    }

    /// <summary>
    /// The instability the engine actually uses (§6.2b, §6.3). Contradiction and wear produce
    /// instability: a material is unpredictable <i>because of what it is</i>, and the
    /// inspector can say exactly why.
    /// </summary>
    /// <param name="baseInstability">The material's authored/derived <c>instability</c>.</param>
    /// <param name="integrity">Remaining transformation budget.</param>
    /// <param name="essenceStrain">Essence beyond resonance capacity (§5.3; live since C1b).</param>
    public static double EffectiveInstability(double baseInstability, int integrity, double essenceStrain = 0.0)
    {
        var spent = RefinementTuning.MaxIntegrity - Math.Clamp(integrity, RefinementTuning.MinIntegrity, RefinementTuning.MaxIntegrity);
        return baseInstability + essenceStrain + spent * RefinementTuning.LostIntegrityInstability;
    }

    /// <summary>
    /// How far outcomes scatter (§12.3). High skill drives this toward zero, so mastery means
    /// reliably hitting the material you were aiming for; low skill or low integrity scatters
    /// you across neighbouring buckets, and you find things by accident.
    /// </summary>
    public static double VarianceMagnitude(double effectiveInstability, double qualityNorm, double severity) =>
        Math.Max(0.0, effectiveInstability * (1.0 - Math.Clamp(qualityNorm, 0.0, 1.0)) * severity);

    /// <summary>
    /// Projects the cost, the resulting integrity and the destruction risk before the player
    /// commits (§6.2c). The edge is a visible risk band rather than a hidden cliff: as
    /// integrity falls, variance rises, so the projection widens into a percentage.
    /// </summary>
    public static IntegrityProjection Project(int currentIntegrity, double expectedCost, double varianceMagnitude)
    {
        var spread = Math.Max(0.0, varianceMagnitude) * RefinementTuning.CostSpreadFactor;
        var projected = (int)Math.Round(currentIntegrity - expectedCost, MidpointRounding.AwayFromZero);

        return new IntegrityProjection(
            ExpectedCost: expectedCost,
            CostSpread: spread,
            ProjectedIntegrity: Math.Max(RefinementTuning.MinIntegrity, projected),
            DestructionChance: DestructionChance(currentIntegrity, expectedCost, spread));
    }

    /// <summary>
    /// Probability that the realised cost meets or exceeds the remaining integrity, treating
    /// the cost as uniform across its spread. Uniform rather than normal on purpose: it makes
    /// the number the player is shown a straight linear reading of how far into the band they
    /// are, which is far easier to build intuition about than a bell curve.
    /// </summary>
    public static double DestructionChance(int currentIntegrity, double expectedCost, double spread)
    {
        if (spread <= 0.0)
            return expectedCost >= currentIntegrity ? 1.0 : 0.0;

        var overshoot = expectedCost + spread - currentIntegrity;
        return Math.Clamp(overshoot / (2.0 * spread), 0.0, 1.0);
    }

    /// <summary>Integrity after a step, floored at 0. Zero means the material was destroyed
    /// (§6.2c) — it is a terminal event, never a state held in inventory.</summary>
    public static int Apply(int currentIntegrity, double cost) =>
        (int)Math.Max(
            RefinementTuning.MinIntegrity,
            Math.Round(currentIntegrity - cost, MidpointRounding.AwayFromZero));

    /// <summary>True when a step of <paramref name="cost"/> destroys the material.</summary>
    public static bool Destroys(int currentIntegrity, double cost) => Apply(currentIntegrity, cost) <= 0;
}
