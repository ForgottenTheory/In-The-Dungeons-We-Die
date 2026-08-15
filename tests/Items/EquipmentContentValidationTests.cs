using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Tests;
using Xunit;

namespace Dungeons.Tests.Items;

/// <summary>
/// Validates the shipped equipment JSON: it deserializes (slot enum, nested
/// weapon/armor blocks, property maps) and resolves into sane combat profiles.
/// </summary>
public class EquipmentContentValidationTests
{
    private static DataStore<EquipmentDefinition> LoadEquipment()
    {
        var store = new DataStore<EquipmentDefinition>();
        foreach (var file in Directory.GetFiles(Path.Combine(TestPaths.DataDir, "equipment"), "*.json"))
            store.LoadOne(File.ReadAllText(file));
        return store;
    }

    private static readonly AttackProfile Unarmed = new()
    {
        Name = "Fists",
        DamageType = DamageType.Crushing,
        BaseDamage = 2,
        StaminaCost = 3,
        Timing = new AbilityTiming { TelegraphTicks = 2, WindupTicks = 6, RecoveryTicks = 12 },
    };

    [Fact]
    public void EveryPieceIsWellFormedAndResolves()
    {
        var equipment = LoadEquipment();
        Assert.True(equipment.Count >= 2);
        Assert.True(equipment.Contains("equip.rusty_sword")); // starter weapon
        Assert.True(equipment.Contains("equip.tattered_armor")); // starter armor

        foreach (var def in equipment.GetAll())
        {
            if (def.Slot == EquipmentSlot.Weapon)
            {
                Assert.NotNull(def.Weapon);
                Assert.Equal(ItemType.Weapon, def.ItemType);
                var attack = EquipmentResolver.ResolveWeapon(def, instance: null, Unarmed);
                Assert.True(attack.BaseDamage > 0);
                Assert.True(attack.Timing.TimeToImpactTicks >= 1);
            }
            else
            {
                Assert.NotNull(def.Armor);
                Assert.Equal(ItemType.Armor, def.ItemType);
                var armor = EquipmentResolver.ResolveArmor(def, instance: null);
                Assert.True(armor.Armor >= 0);
            }
        }
    }

    [Fact]
    public void IronSword_IsHeavierAndHitsHarderThanRusty()
    {
        var equipment = LoadEquipment();
        var rusty = EquipmentResolver.ResolveWeapon(equipment.GetById("equip.rusty_sword"), null, Unarmed);
        var iron = EquipmentResolver.ResolveWeapon(equipment.GetById("equip.iron_sword"), null, Unarmed);

        Assert.True(iron.BaseDamage > rusty.BaseDamage);            // stronger
        Assert.True(iron.Timing.WindupTicks >= rusty.Timing.WindupTicks); // and no faster (more mass)
    }
}
