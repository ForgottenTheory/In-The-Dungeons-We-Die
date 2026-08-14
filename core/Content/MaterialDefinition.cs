namespace Dungeons.Content;

/// <summary>A named quantitative property a material carries (e.g. toxin_resistance 0.05).</summary>
public sealed class MaterialProperty
{
    public string Property { get; init; } = string.Empty;
    public double Value { get; init; }
}

/// <summary>
/// Data-driven material definition. Properties let materials be more than recipe
/// tokens — combining them can produce new outcomes in crafting
/// (docs/json-schema.md §7, docs/crafting.md §4).
/// </summary>
public sealed class MaterialDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<MaterialProperty> Properties { get; init; } = Array.Empty<MaterialProperty>();

    public bool HasProperty(string property) => Properties.Any(p => p.Property == property);

    /// <summary>Returns the value of <paramref name="property"/>, or 0 if the material lacks it.</summary>
    public double GetProperty(string property) =>
        Properties.FirstOrDefault(p => p.Property == property)?.Value ?? 0.0;
}
