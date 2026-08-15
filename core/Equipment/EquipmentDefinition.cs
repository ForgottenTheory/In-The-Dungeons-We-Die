using Dungeons.Combat;

namespace Dungeons.Items;

public enum EquipmentSlot
{
    Weapon,
    Armor,
}

/// <summary>Weapon base stats (before instance-property derivation). Uses the same nested
/// <see cref="AbilityTiming"/> shape as abilities so timing is authored one way everywhere.</summary>
public sealed class WeaponStats
{
    public double BaseDamage { get; init; } = 1;
    public DamageType DamageType { get; init; } = DamageType.Slashing;
    public AbilityTiming Timing { get; init; } = new() { TelegraphTicks = 2, WindupTicks = 8, RecoveryTicks = 15 };
    public int StaminaCost { get; init; } = 5;
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

    public WeaponStats? Weapon { get; init; }
    public ArmorStats? Armor { get; init; }

    /// <summary>Intrinsic material-style properties, as a name→value map.</summary>
    public Dictionary<string, double> Properties { get; init; } = new();

    public ItemType ItemType => Slot == EquipmentSlot.Weapon ? ItemType.Weapon : ItemType.Armor;
    public bool Stackable => false;
    public PropertySet BaseProperties => new(Properties);
}
