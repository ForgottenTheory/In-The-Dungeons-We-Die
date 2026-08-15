using Dungeons.Items;

namespace Dungeons.Content;

/// <summary>
/// Computes a material's resistance to a reactive property from that property's
/// <c>resisted_by</c> contributors (docs/emergent-item-system.md §2.2). An authored
/// Response property (e.g. <c>heat_resistance</c>) acts as an override so existing content
/// keeps working; otherwise resistance is derived from the remaining contributors
/// (e.g. <c>insulation</c>, <c>mass</c>). Pure — no engine dependency; ready for P1.
/// </summary>
public static class ResistanceCalculator
{
    /// <summary>
    /// Resistance (0–100) of <paramref name="material"/> to the reactive property
    /// <paramref name="reactiveProperty"/>. Returns 0 for unknown or non-reactive properties.
    /// </summary>
    public static double Resistance(
        string reactiveProperty,
        PropertySet material,
        DataStore<PropertyDefinition> properties)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(properties);

        if (!properties.TryGetById(reactiveProperty, out var def) || def.Role != PropertyRole.Reactive)
            return 0.0;

        // Override: the authored Response contributor (e.g. heat_resistance) wins if present.
        var overrideContributor = def.ResistedBy.FirstOrDefault(c =>
            properties.TryGetById(c.Property, out var cDef) && cDef.Role == PropertyRole.Response);

        if (overrideContributor is not null && material.Has(overrideContributor.Property))
            return Clamp(material.Get(overrideContributor.Property));

        // Otherwise derive from the non-override contributors.
        var sum = def.ResistedBy
            .Where(c => c != overrideContributor)
            .Sum(c => material.Get(c.Property) * c.Weight);

        return Clamp(sum);
    }

    private static double Clamp(double value) => Math.Clamp(value, 0.0, 100.0);
}
