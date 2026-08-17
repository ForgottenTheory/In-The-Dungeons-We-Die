namespace Dungeons.Items;

/// <summary>
/// A specific owned item whose properties may differ from its definition — every
/// piece of equipment, and any generated/processed material (e.g. "Bloodmoss Iron
/// Ingot"). Instances are the unit that carries derived crafting results and can be
/// crafted again recursively (docs/itemization.md §1, docs/crafting.md §17).
/// </summary>
public sealed class ItemInstance
{
    public required long InstanceId { get; init; }

    /// <summary>The definition this instance derives its identity from.</summary>
    public required string BaseDefinitionId { get; init; }

    public required ItemType ItemType { get; init; }

    /// <summary>Generated display name, e.g. "Bloodmoss Iron Ingot".</summary>
    public string DisplayName { get; init; } = string.Empty;

    public ItemQuality Quality { get; init; } = ItemQuality.Normal;

    /// <summary>The derived properties — what makes this instance different from its definition.</summary>
    public PropertySet Properties { get; init; } = PropertySet.Empty;

    /// <summary>Definition ids of the materials this instance was made from.</summary>
    public IReadOnlyList<string> Provenance { get; init; } = Array.Empty<string>();

    /// <summary>The genetic profile computed at fabrication (docs/affixes.md §2.1) — stored,
    /// never recomputed (save v6). Null on pre-affix instances and authored starter gear.</summary>
    public Crafting.ItemPotential? Potential { get; init; }

    /// <summary>Innates + rolled modifiers, in display order: innates first (D-21).</summary>
    public IReadOnlyList<Affixes.RolledAffix> Affixes { get; init; } = Array.Empty<Affixes.RolledAffix>();

    /// <summary>Named traits/effects generated during crafting (reserved for the reaction sim).</summary>
    public IReadOnlyList<string> Traits { get; init; } = Array.Empty<string>();

    public bool IsEquipment => ItemType is ItemType.Weapon or ItemType.Armor;
}
