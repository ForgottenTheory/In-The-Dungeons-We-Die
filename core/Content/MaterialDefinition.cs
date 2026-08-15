using Dungeons.Items;

namespace Dungeons.Content;

/// <summary>A named quantitative property a material carries (e.g. toxin_resistance 0.05).</summary>
public sealed class MaterialProperty
{
    public string Property { get; init; } = string.Empty;
    public double Value { get; init; }
}

/// <summary>
/// Data-driven raw-material definition — a stackable <see cref="IItemDefinition"/>.
/// Its properties are the intrinsic starting point that crafting derives from
/// (docs/itemization.md §1–2, docs/json-schema.md §7).
/// </summary>
public sealed class MaterialDefinition : IItemDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<MaterialProperty> Properties { get; init; } = Array.Empty<MaterialProperty>();

    public ItemType ItemType => ItemType.Material;
    public bool Stackable => true;

    public PropertySet BaseProperties =>
        new(Properties.ToDictionary(p => p.Property, p => p.Value));

    public bool HasProperty(string property) => Properties.Any(p => p.Property == property);

    /// <summary>Returns the value of <paramref name="property"/>, or 0 if the material lacks it.</summary>
    public double GetProperty(string property) =>
        Properties.FirstOrDefault(p => p.Property == property)?.Value ?? 0.0;
}
