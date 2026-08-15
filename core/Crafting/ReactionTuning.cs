namespace Dungeons.Crafting;

/// <summary>
/// Constants of the universal reaction algebra (docs/emergent-item-system.md §8). The spec
/// states plainly that these are first-pass and expected to be tuned, so they live in one
/// place rather than scattered through the algebra. Changing a value here changes the
/// physics of every craft in the game — there are no per-combination overrides anywhere.
/// </summary>
public static class ReactionTuning
{
    // ---- §8.1 acceptance / release / integrity -----------------------------------------
    //
    // All three share the shape `base + scale × (property / 100)`: even a wholly unwilling
    // substrate accepts a little, so no craft is ever a total no-op.

    public const double AcceptanceBase = 0.25;
    public const double AcceptanceScale = 0.75;
    public const double ReleaseBase = 0.25;
    public const double ReleaseScale = 0.75;
    public const double IntegrityFactorBase = 0.50;
    public const double IntegrityFactorScale = 0.50;

    /// <summary>Catalyst factor when no catalyst is slotted (§8.1).</summary>
    public const double NoCatalyst = 1.0;

    // ---- §8.2 convergence ---------------------------------------------------------------

    /// <summary>
    /// The most of the remaining gap a single step may close. Properties move a fraction of
    /// the way toward the reagent and never past it, which is what makes unbounded stat
    /// escalation impossible without caps or diminishing-return fudge factors.
    /// </summary>
    public const double MaxConvergence = 0.85;

    // ---- §8.3 off-channel handling ------------------------------------------------------

    /// <summary>Rate at which a non-diluting off-channel property blends toward the
    /// mass-weighted mixture — an alloy does get a bit heavier if you add heavy things.</summary>
    public const double StructuralBlendRate = 0.10;

    /// <summary>Rate at which a diluting off-channel property decays toward zero. This is what
    /// stops generation-5 materials carrying twenty-five nonzero properties: each step focuses
    /// the material along the channel and washes out the rest.</summary>
    public const double ReactiveDilutionRate = 0.08;

    /// <summary>Floor used when a property definition declares none.</summary>
    public const int DefaultFloor = 5;

    // ---- §8.5 opposition ----------------------------------------------------------------

    /// <summary>Fraction of an opposed overlap that mutually annihilates. Only the asymmetry
    /// survives, so opposites cannot be stockpiled.</summary>
    public const double AnnihilationRate = 0.9;

    // ---- Scale ---------------------------------------------------------------------------

    public const double MinPropertyValue = 0.0;
    public const double MaxPropertyValue = 100.0;
}
