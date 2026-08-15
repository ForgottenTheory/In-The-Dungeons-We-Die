using System.Text.Json.Serialization;
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

    /// <summary>
    /// Optional authored override for derived potency (§6.1). Left unset by the whole
    /// authored library — <see cref="MaterialProfileResolver"/> derives it — and exists so a
    /// single material whose derived value reads wrong can be hand-tuned without special
    /// cases in code.
    /// </summary>
    public int? Potency { get; init; }

    /// <summary>Optional authored override for derived integrity (§6.2). See <see cref="Potency"/>.</summary>
    public int? Integrity { get; init; }

    /// <summary>
    /// The explicit profile of an <b>emergent</b> archetype, computed by the reaction engine
    /// and registered under its signature (§12). Null for authored materials, whose profile
    /// is derived by <see cref="MaterialProfileResolver"/>. Never authored in JSON — always
    /// resolve through the resolver rather than reading this directly.
    /// </summary>
    [JsonIgnore]
    public MaterialProfile? Profile { get; init; }

    public ItemType ItemType => ItemType.Material;
    public bool Stackable => true;

    public PropertySet BaseProperties => new(Properties);

    public bool HasProperty(string property) => BaseProperties.Has(property);

    /// <summary>Returns the value of <paramref name="property"/>, or 0 if the material lacks it.</summary>
    public double GetProperty(string property) => BaseProperties.Get(property);
}
