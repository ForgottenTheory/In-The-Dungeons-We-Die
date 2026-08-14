namespace Dungeons.Characters.Modifiers;

/// <summary>
/// Applies numeric modifiers in the canonical order from docs/architecture.md §18:
/// base value → additive modifiers → multiplicative modifiers → clamp. Rule-based
/// overrides are handled separately by the rule system, not here.
/// </summary>
public static class ModifierPipeline
{
    /// <summary>
    /// Resolves the final value of one stat: <c>(base + Σ adds) × Π multiplies</c>,
    /// then clamped to <paramref name="min"/>. Only modifiers whose
    /// <see cref="StatModifier.Stat"/> equals <paramref name="stat"/> are considered.
    /// </summary>
    public static double Resolve(StatId stat, double baseValue, IEnumerable<StatModifier> modifiers, double min = 0.0)
    {
        var additive = baseValue;
        var multiplier = 1.0;

        foreach (var modifier in modifiers)
        {
            if (modifier.Stat != stat)
                continue;

            switch (modifier.Op)
            {
                case ModifierOperation.Add:
                    additive += modifier.Value;
                    break;
                case ModifierOperation.Multiply:
                    multiplier *= modifier.Value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(modifiers), modifier.Op, "Unknown modifier operation.");
            }
        }

        return Math.Max(min, additive * multiplier);
    }

    /// <summary>Convenience wrapper returning a rounded integer stat value.</summary>
    public static int ResolveInt(StatId stat, double baseValue, IEnumerable<StatModifier> modifiers, int min = 0) =>
        (int)Math.Round(Resolve(stat, baseValue, modifiers, min), MidpointRounding.AwayFromZero);
}
