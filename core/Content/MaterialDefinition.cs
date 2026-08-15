using Dungeons.Items;

namespace Dungeons.Content;

/// <summary>A named quantitative property value (e.g. toxin_resistance 0.05). Used to
/// report a crafting outcome's derived properties; material definitions store their
/// properties as a flat name→value map (see <see cref="MaterialDefinition.Properties"/>).</summary>
public sealed class MaterialProperty
{
    public string Property { get; init; } = string.Empty;
    public double Value { get; init; }
}

/// <summary>
/// Data-driven raw-material definition — a stackable <see cref="IItemDefinition"/>.
/// Its properties are the intrinsic starting point that crafting derives from. They are
/// a flat name→value map (matching <see cref="Dungeons.Items.EquipmentDefinition"/>), on
/// a 0–100 scale, and only the properties a material actually has are listed — anything
/// absent reads as 0 (docs/itemization.md §2, docs/json-schema.md §7).
/// </summary>
public sealed class MaterialDefinition : IItemDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public Dictionary<string, double> Properties { get; init; } = new();

    public ItemType ItemType => ItemType.Material;
    public bool Stackable => true;

    public PropertySet BaseProperties => new(Properties);

    public bool HasProperty(string property) => BaseProperties.Has(property);

    /// <summary>Returns the value of <paramref name="property"/>, or 0 if the material lacks it.</summary>
    public double GetProperty(string property) => BaseProperties.Get(property);
}
