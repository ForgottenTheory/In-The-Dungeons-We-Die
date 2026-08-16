using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Tests;
using Xunit;

namespace Dungeons.Tests.Items;

/// <summary>
/// Validates the shipped equipment JSON: it deserializes (slot enum, move grants, armor block,
/// property maps) and resolves into sane combat shapes — moves for weapons, profiles for armour
/// (E4, D-18).
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

    private static DataStore<MoveDefinition> LoadMoves() => TestPaths.LoadStore<MoveDefinition>("moves");

    [Fact]
    public void EveryPieceIsWellFormedAndResolves()
    {
        var equipment = LoadEquipment();
        var moves = LoadMoves();
        Assert.True(equipment.Count >= 2);
        Assert.True(equipment.Contains("equip.rusty_sword")); // starter weapon
        Assert.True(equipment.Contains("equip.tattered_armor")); // starter armor

        foreach (var def in equipment.GetAll())
        {
            if (def.Slot == EquipmentSlot.Weapon)
            {
                Assert.NotEmpty(def.Moves);   // weapon-granted moves are mandatory (docs/moves.md §5.1)
                Assert.Equal(ItemType.Weapon, def.ItemType);

                var resolved = EquipmentResolver.ResolveWeaponMoves(def, instance: null, moves);
                Assert.Equal(def.Moves.Count, resolved.Count);

                foreach (var move in resolved.Where(m => m.Packets.Count > 0))
                {
                    Assert.True(move.Packets.Sum(p => p.Amount) > 0);
                    Assert.True(move.Timing.TimeToImpactTicks >= 1);
                }
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
        var moves = LoadMoves();

        var rusty = EquipmentResolver.ResolveWeaponMoves(equipment.GetById("equip.rusty_sword"), null, moves)
            .First(m => m.Packets.Count > 0);
        var iron = EquipmentResolver.ResolveWeaponMoves(equipment.GetById("equip.iron_sword"), null, moves)
            .First(m => m.Packets.Count > 0);

        Assert.True(iron.Packets.Sum(p => p.Amount) > rusty.Packets.Sum(p => p.Amount)); // stronger
        Assert.True(iron.Timing.WindupTicks >= rusty.Timing.WindupTicks);                // and no faster (more mass)
    }
}
