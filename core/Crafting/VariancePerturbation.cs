using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Randomness;

namespace Dungeons.Crafting;

/// <summary>
/// The seeded scatter applied to a result before it is quantized (docs/emergent-item-system.md
/// §12.3).
///
/// <para>The subtle and important consequence: <b>variance produces different materials, not
/// random stats on the same material.</b> A bad roll does not hand you "Emberveined Iron (bad
/// roll)" — it hands you a different, weaker material with its own name and signature,
/// possibly one nobody has seen. For a discovery game that is strictly the better outcome.</para>
///
/// <para>With <see cref="MaterialTransformationRules"/>'s quality multiplier, this is one of only two
/// probabilistic things in the entire system (§12.5), and it draws from an injected
/// <see cref="IRandomSource"/> so a craft stays reproducible from its seed.</para>
/// </summary>
public static class VariancePerturbation
{
    /// <summary>
    /// Scatters the channel properties of <paramref name="state"/> by up to
    /// <paramref name="varianceMagnitude"/>. High skill drives the magnitude to zero, so a
    /// master reliably hits the material they were aiming at; low skill or low workability
    /// scatters across neighbouring buckets and finds things by accident.
    /// </summary>
    public static PropertySet Apply(
        PropertySet state,
        CraftingActionDefinition craftingAction,
        double varianceMagnitude,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(craftingAction);
        ArgumentNullException.ThrowIfNull(random);

        var spread = Math.Max(0.0, varianceMagnitude) * QuantizationTuning.VarianceScale;
        if (spread <= 0.0)
            return state;

        var scattered = new Dictionary<string, double>(state.AsDictionary(), StringComparer.OrdinalIgnoreCase);

        // Ordered so the draws are consumed in a stable sequence — otherwise the same seed
        // would give different results depending on dictionary iteration order.
        foreach (var entry in craftingAction.AffectedQualities.OrderBy(c => c.Property, StringComparer.OrdinalIgnoreCase))
        {
            // Only properties the material actually has are scattered. Perturbing an absent
            // one would conjure heat into something the crafting action never heated.
            if (!state.Has(entry.Property))
                continue;

            var offset = (random.NextDouble() * 2.0 - 1.0) * spread;
            scattered[entry.Property] = Math.Clamp(
                state.Get(entry.Property) + offset,
                MaterialTransformationTuning.MinPropertyValue,
                MaterialTransformationTuning.MaxPropertyValue);
        }

        return new PropertySet(scattered);
    }
}
