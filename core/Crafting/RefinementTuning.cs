namespace Dungeons.Crafting;

/// <summary>
/// Constants for the meta fields — potency, integrity, volatility, generation
/// (docs/emergent-item-system.md §6). These are separate from <see cref="ReactionTuning"/>
/// on purpose: §8's algebra decides what a material <i>becomes</i>, while these decide how
/// strongly it expresses and how much further it can be pushed. Both are first-pass values.
/// </summary>
public static class RefinementTuning
{
    // ---- §6.1 potency ------------------------------------------------------------------

    /// <summary>
    /// How far above its best input a craft may lift potency, at perfect execution.
    /// Refinement can improve a material a little; it cannot conjure quality from nothing.
    /// Together with the integrity budget this is what closes the escalation loop.
    /// </summary>
    public const double PotencyCeilingBonus = 8.0;

    public const int MinPotency = 1;
    public const int MaxPotency = 100;

    // ---- §7.4 execution quality ----------------------------------------------------------

    public const double QualityMultiplierBase = 0.85;
    public const double QualityMultiplierScale = 0.27;

    // ---- §6.2a integrity cost --------------------------------------------------------------
    //
    // Cost scales with how violent the change was, not with how many times you crafted — so a
    // gentle, well-chosen step that achieves its goal precisely costs little, and a
    // brute-force process that thrashes half the vector costs a lot. This is the main
    // skill-expression axis of the whole system.

    /// <summary>Integrity charged per unit of Δstate × process severity.</summary>
    public const double StateDeltaCost = 12.0;

    /// <summary>Integrity charged per trait created. Always 0 in P1 — traits are P2.</summary>
    public const double TraitCost = 4.0;

    /// <summary>Integrity charged per unit of strain released by annihilation (§8.5).</summary>
    public const double StrainCost = 0.3;

    /// <summary>
    /// The share of a step's cost that perfect execution saves.
    ///
    /// <para>§6.2a writes skill mitigation as a flat subtraction, but a flat value cannot work
    /// here: §19's own arithmetic puts a violent forge step's base cost around 2.7, so any flat
    /// mitigation large enough to feel meaningful also erases the entire cost of most crafts
    /// and makes integrity a non-constraint. As a proportion it scales with how violent the
    /// step was, which is also the more sensible reading — skill saves you <i>some</i> of the
    /// damage you were going to do, not a fixed amount regardless.</para>
    ///
    /// <para>Kept modest because §7.4 is explicit that skill's main effect is narrowing
    /// variance, not a flat discount.</para>
    /// </summary>
    public const double SkillMitigationFraction = 0.25;

    /// <summary>
    /// A transformation always costs something. Without this floor a sufficiently skilled
    /// crafter could ratchet a material through free A→B→A loops forever, which §17 names as
    /// an exploit the monotonic integrity budget exists to prevent.
    /// </summary>
    public const double MinimumIntegrityCost = 1.0;

    // ---- §6.2b / §6.3 volatility -------------------------------------------------------------

    /// <summary>How strongly spent integrity feeds effective instability. Low integrity is the
    /// frontier, not a wall: deep-generation materials are a gamble, not a dead end.</summary>
    public const double LostIntegrityInstability = 0.4;

    /// <summary>Half-width of the projected-cost spread per unit of variance magnitude (§12.3),
    /// used to turn the edge into a visible risk band rather than a hidden cliff.</summary>
    public const double CostSpreadFactor = 0.5;

    /// <summary>Below this integrity the craft UI shows a destruction <i>chance</i> rather than
    /// a certainty (§6.2c). Presentation only — the projection is always available.</summary>
    public const int DestructionRiskBandIntegrity = 25;

    public const int MinIntegrity = 0;
    public const int MaxIntegrity = 100;
}
