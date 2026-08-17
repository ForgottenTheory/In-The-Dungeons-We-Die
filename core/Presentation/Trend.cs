using Dungeons.Crafting;

namespace Dungeons.Presentation;

/// <summary>
/// §2D of docs/presentation-architecture.md — what a property is <i>doing</i> across a
/// projected craft, derived from the algebra's own typed <see cref="PropertyChangeKind"/>
/// records, never re-inferred from numbers.
/// </summary>
public enum Trend
{
    /// <summary>Net movement inside the steady window — nothing worth an arrow.</summary>
    Steady,

    /// <summary>Channel-driven gain — the crafting action is reinforcing it.</summary>
    Rising,

    /// <summary>Channel-driven loss — converging down toward the reagent.</summary>
    Falling,

    /// <summary>Off-channel structural settling toward the mass-weighted mixture.</summary>
    Drifting,

    /// <summary>Off-channel dilution — washing out toward zero.</summary>
    Fading,

    /// <summary>Pruned to nothing — lost to trace.</summary>
    Vanishing,

    /// <summary>Mutually annihilating against its opposite — strain released.</summary>
    Conflicting,

    /// <summary>Newly present — absent before the craft, real after it.</summary>
    Emerging,
}

/// <summary>One property's aggregate movement across a projected craft, first state to last.</summary>
public sealed record PropertyMovement(string Property, double Initial, double Final, Trend Trend)
{
    public double Net => Final - Initial;
    public PropertyTier TierBefore => Tiers.Of(Initial);
    public PropertyTier TierAfter => Tiers.Of(Final);

    /// <summary>A tier boundary was crossed — the format layer doubles the arrow.</summary>
    public bool CrossesTier => TierBefore != TierAfter;
}

public static class Trends
{
    /// <summary>
    /// Folds per-step typed changes into one movement per property. Precedence: annihilation
    /// marks a property <see cref="Trend.Conflicting"/> whatever else happened to it; ending at
    /// zero reads <see cref="Trend.Vanishing"/>; starting from zero reads
    /// <see cref="Trend.Emerging"/>; net movement inside the steady window reads
    /// <see cref="Trend.Steady"/>; then channel movement wins over dilution over blending.
    /// Derived-resistance drops are bookkeeping (§2.2) and excluded entirely. Ordered by
    /// absolute net movement, largest first, ties by id — deterministic.
    /// </summary>
    public static IReadOnlyList<PropertyMovement> Aggregate(IReadOnlyList<TransformationStepResult> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var order = new List<string>();
        var first = new Dictionary<string, double>(StringComparer.Ordinal);
        var last = new Dictionary<string, double>(StringComparer.Ordinal);
        var sawAnnihilation = new HashSet<string>(StringComparer.Ordinal);
        var sawChannel = new HashSet<string>(StringComparer.Ordinal);
        var sawDilution = new HashSet<string>(StringComparer.Ordinal);

        foreach (var step in steps)
        {
            foreach (var change in step.Changes)
            {
                if (change.Kind == PropertyChangeKind.DerivedResistance)
                    continue;

                if (!first.ContainsKey(change.Property))
                {
                    first[change.Property] = change.Before;
                    order.Add(change.Property);
                }

                last[change.Property] = change.After;

                switch (change.Kind)
                {
                    case PropertyChangeKind.Annihilation:
                        sawAnnihilation.Add(change.Property);
                        break;
                    case PropertyChangeKind.OnChannelTransfer:
                        sawChannel.Add(change.Property);
                        break;
                    case PropertyChangeKind.Dilution:
                    case PropertyChangeKind.Pruned:
                        sawDilution.Add(change.Property);
                        break;
                }
            }
        }

        var movements = new List<PropertyMovement>(order.Count);
        foreach (var property in order)
        {
            var initial = first[property];
            var final = last[property];

            var trend =
                sawAnnihilation.Contains(property) ? Trend.Conflicting
                : initial > 0 && final <= 0 ? Trend.Vanishing
                : initial <= 0 && final > 0 ? Trend.Emerging
                : Math.Abs(final - initial) <= PresentationTuning.SteadyWindow ? Trend.Steady
                : sawChannel.Contains(property) ? (final > initial ? Trend.Rising : Trend.Falling)
                : sawDilution.Contains(property) ? Trend.Fading
                : Trend.Drifting;

            movements.Add(new PropertyMovement(property, initial, final, trend));
        }

        return movements
            .OrderByDescending(m => Math.Abs(m.Net))
            .ThenBy(m => m.Property, StringComparer.Ordinal)
            .ToList();
    }
}
