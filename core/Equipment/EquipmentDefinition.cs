using Dungeons.Combat;

namespace Dungeons.Items;

public enum EquipmentSlot
{
    Weapon,
    Armor,
}

/// <summary>Armor base stats (before instance-property derivation).</summary>
public sealed class ArmorStats
{
    public double Armor { get; init; }
    public Dictionary<string, double> Resistances { get; init; } = new();
}

/// <summary>
/// Data-driven equipment blueprint (a non-stackable <see cref="IItemDefinition"/>).
/// A specific worn item is always an <see cref="ItemInstance"/>; the definition holds
/// the base stats that the instance's derived properties adjust (docs/itemization.md §3).
/// </summary>
public sealed class EquipmentDefinition : IItemDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public EquipmentSlot Slot { get; init; } = EquipmentSlot.Weapon;

    /// <summary>
    /// The moves this item grants while worn (E4, docs/moves.md §5.1). <b>Weapon-granted moves
    /// are mandatory, not optional</b> — the Fighter's entire identity is "moveset comes from
    /// the weapon; reconfigures by re-equipping". This replaces the old <c>WeaponStats</c>
    /// block: a weapon's numbers now live on its moves, adjusted per instance by
    /// <c>EquipmentResolver</c>.
    /// </summary>
    public IReadOnlyList<MoveGrantSpec> Moves { get; init; } = Array.Empty<MoveGrantSpec>();

    /// <summary>Move modifiers granted while worn, by id.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("move_modifiers")]
    public IReadOnlyList<string> MoveModifierIds { get; init; } = Array.Empty<string>();

    public ArmorStats? Armor { get; init; }

    /// <summary>Traits expressed through the fabrication trait expression (C2a §16.3). Empty on
    /// authored gear.</summary>
    public IReadOnlyList<Crafting.TraitInstance> ExpressedTraits { get; init; } = Array.Empty<Crafting.TraitInstance>();

    /// <summary>Traits the trait expression or cap held back — kept for value, flavour, and future
    /// refabrication (§16.2's dormancy rule).</summary>
    public IReadOnlyList<Crafting.TraitInstance> DormantTraits { get; init; } = Array.Empty<Crafting.TraitInstance>();

    /// <summary>Mass-share-weighted, arcane-amplified essence (§16.3 step 5).</summary>
    public IReadOnlyDictionary<string, double> Essence { get; init; } = new Dictionary<string, double>();

    /// <summary>Intrinsic material-style properties, as a name→value map.</summary>
    public Dictionary<string, double> Properties { get; init; } = new();

    public ItemType ItemType => Slot == EquipmentSlot.Weapon ? ItemType.Weapon : ItemType.Armor;
    public bool Stackable => false;
    public PropertySet BaseProperties => new(Properties);
}
