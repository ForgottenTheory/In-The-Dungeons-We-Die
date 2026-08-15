using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>Why a property moved during a reagent step — the vocabulary the Reaction Log
/// (§15.3) explains a craft in.</summary>
public enum PropertyChangeKind
{
    /// <summary>Converged toward the reagent because the process opened it (§8.2).</summary>
    Channel,

    /// <summary>Blended toward the mass-weighted mixture while off-channel (§8.3).</summary>
    StructuralBlend,

    /// <summary>Diluted toward zero while off-channel, receiving nothing (§8.3).</summary>
    Dilution,

    /// <summary>Fell below its floor and was pruned to zero (§8.3).</summary>
    Pruned,

    /// <summary>Mutually annihilated against its opposite (§8.5).</summary>
    Annihilation,

    /// <summary>Dropped because resistances are derived, not carried (§2.2, §2.3).</summary>
    DerivedResistance,
}

/// <summary>One property's movement during a step, recorded for the Reaction Log.</summary>
public sealed record PropertyChange(string Property, double Before, double After, PropertyChangeKind Kind)
{
    public double Delta => After - Before;
}

/// <summary>
/// The outcome of applying one reagent to a substrate (docs/emergent-item-system.md §8.7
/// steps 2–6). Potency, integrity, tags, quantization and naming are computed by later
/// stages from this — the algebra itself only moves properties.
/// </summary>
public sealed record ReactionStepResult(
    PropertySet Properties,
    ReactionCoefficients Coefficients,
    double StrainReleased,
    IReadOnlyList<PropertyChange> Changes)
{
    /// <summary>
    /// Total absolute movement across the vector, normalized — the <c>Δstate</c> that
    /// integrity is charged against (§6.2a). Change is what costs, so a gentle step that
    /// achieves its goal precisely costs little and a brute-force one costs a lot.
    ///
    /// <para>Dropped resistances are excluded: they are a bookkeeping consequence of
    /// resistances being derived rather than carried (§2.2), not a transformation the player
    /// caused, and charging integrity for one would make the first craft on any
    /// resistance-authored material inexplicably expensive.</para>
    /// </summary>
    public double StateDelta => Changes
        .Where(c => c.Kind != PropertyChangeKind.DerivedResistance)
        .Sum(c => Math.Abs(c.Delta)) / 100.0;
}
