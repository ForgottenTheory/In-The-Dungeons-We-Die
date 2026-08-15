using Dungeons.Combat;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Items;

public class EquipmentTests
{
    private static readonly AttackProfile Unarmed = new()
    {
        Name = "Fists",
        DamageType = DamageType.Crushing,
        BaseDamage = 2,
        StaminaCost = 3,
        Timing = new AbilityTiming { TelegraphTicks = 2, WindupTicks = 6, RecoveryTicks = 12 },
    };

    private static EquipmentDefinition IronSword() => new()
    {
        Id = "equip.iron_sword",
        Name = "Iron Sword",
        Slot = EquipmentSlot.Weapon,
        Weapon = new WeaponStats { BaseDamage = 10, DamageType = DamageType.Slashing, Timing = new AbilityTiming { TelegraphTicks = 2, WindupTicks = 8, RecoveryTicks = 15 }, StaminaCost = 5 },
    };

    private static EquipmentDefinition IronArmor() => new()
    {
        Id = "equip.iron_armor",
        Name = "Iron Armor",
        Slot = EquipmentSlot.Armor,
        Armor = new ArmorStats { Armor = 4, Resistances = new() { ["Slashing"] = 0.1 } },
    };

    [Fact]
    public void EquipmentContainer_EquipReturnsDisplaced()
    {
        var equip = new Equipment();
        var a = new ItemInstance { InstanceId = 1, BaseDefinitionId = "equip.iron_sword", ItemType = ItemType.Weapon };
        var b = new ItemInstance { InstanceId = 2, BaseDefinitionId = "equip.iron_sword", ItemType = ItemType.Weapon };

        Assert.Null(equip.Equip(EquipmentSlot.Weapon, a));
        Assert.Same(a, equip.Equip(EquipmentSlot.Weapon, b)); // b displaces a
        Assert.Same(b, equip.InSlot(EquipmentSlot.Weapon));
        Assert.Same(b, equip.Unequip(EquipmentSlot.Weapon));
        Assert.Null(equip.InSlot(EquipmentSlot.Weapon));
    }

    [Fact]
    public void ResolveWeapon_UsesBaseStats_WhenNoInstanceProperties()
    {
        var profile = EquipmentResolver.ResolveWeapon(IronSword(), instance: null, Unarmed);
        Assert.Equal(10, profile.BaseDamage);
        Assert.Equal(DamageType.Slashing, profile.DamageType);
        Assert.Equal(8, profile.Timing.WindupTicks);
        Assert.Equal("Iron Sword", profile.Name);
    }

    [Fact]
    public void ResolveWeapon_DerivesFromInstanceMass()
    {
        var heavy = new ItemInstance
        {
            InstanceId = 1,
            BaseDefinitionId = "equip.iron_sword",
            ItemType = ItemType.Weapon,
            DisplayName = "Dense Iron Sword",
            Properties = new PropertySet(new Dictionary<string, double> { [ItemProperties.Mass] = 3 }),
        };

        var profile = EquipmentResolver.ResolveWeapon(IronSword(), heavy, Unarmed);
        Assert.Equal(13, profile.BaseDamage);          // 10 + mass 3 * 1.0
        Assert.Equal(8 + 6, profile.Timing.WindupTicks); // 8 + mass 3 * 2 → slower
        Assert.Equal("Dense Iron Sword", profile.Name);
    }

    [Fact]
    public void ResolveWeapon_FallsBackToUnarmed_WhenDefinitionHasNoWeaponBlock()
    {
        var profile = EquipmentResolver.ResolveWeapon(IronArmor(), instance: null, Unarmed);
        Assert.Same(Unarmed, profile);
    }

    [Fact]
    public void ResolveArmor_AddsHardnessAndKeepsResistances()
    {
        var hardened = new ItemInstance
        {
            InstanceId = 1,
            BaseDefinitionId = "equip.iron_armor",
            ItemType = ItemType.Armor,
            Properties = new PropertySet(new Dictionary<string, double> { [ItemProperties.Hardness] = 4 }),
        };

        var armor = EquipmentResolver.ResolveArmor(IronArmor(), hardened);
        Assert.Equal(4 + 2, armor.Armor);              // 4 + hardness 4 * 0.5
        Assert.Equal(0.1, armor.ResistanceFor("Slashing"));
    }
}
