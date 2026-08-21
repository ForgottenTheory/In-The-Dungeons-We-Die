using Dungeons.Actions;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Items;

/// <summary>
/// The equipment seam after E4: a weapon resolves into <b>moves</b>, not an attack profile
/// (D-18; D8's intent preserved — combat reads a neutral shape, never an equipment type).
/// Mass still buys damage and costs speed; it now lands on the move's packets the way
/// attribute scaling lands on a hit — once, split by share.
/// </summary>
public class EquipmentTests
{
    private static MoveDefinition Slash() => new()
    {
        Id = "move.iron_slash",
        Name = "Iron Slash",
        Tags = new[] { "action:attack", "delivery:melee", "form:sword" },
        Timing = new ActionTiming { TelegraphTicks = 2, WindupTicks = 8, RecoveryTicks = 15 },
        Costs = new[] { new ActionCost { Resource = "stamina", Amount = 5 } },
        Packets = new[] { new Packet(DamageType.Slashing, 10) },
    };

    private static DataStore<MoveDefinition> Moves()
    {
        var store = new DataStore<MoveDefinition>();
        store.Add(Slash());
        return store;
    }

    private static EquipmentDefinition IronSword() => new()
    {
        Id = "equip.iron_sword",
        Name = "Iron Sword",
        Slot = EquipmentSlot.Weapon,
        Moves = new[] { new MoveGrantSpec { Id = "move.iron_slash" } },
    };

    private static EquipmentDefinition IronArmor() => new()
    {
        Id = "equip.iron_armor",
        Name = "Iron Armor",
        Slot = EquipmentSlot.Body,
        Armor = new ArmorStats { Armor = 4, Resistances = new() { ["Slashing"] = 0.1 } },
    };

    [Fact]
    public void EquipmentContainer_EquipReturnsDisplaced()
    {
        var equip = new Dungeons.Items.Equipment();
        var a = new ItemInstance { InstanceId = 1, BaseDefinitionId = "equip.iron_sword", ItemType = ItemType.Weapon };
        var b = new ItemInstance { InstanceId = 2, BaseDefinitionId = "equip.iron_sword", ItemType = ItemType.Weapon };

        Assert.Null(equip.Equip(EquipmentSlot.Weapon, a));
        Assert.Same(a, equip.Equip(EquipmentSlot.Weapon, b)); // b displaces a
        Assert.Same(b, equip.InSlot(EquipmentSlot.Weapon));
        Assert.Same(b, equip.Unequip(EquipmentSlot.Weapon));
        Assert.Null(equip.InSlot(EquipmentSlot.Weapon));
    }

    [Fact]
    public void ResolveWeaponMoves_UsesTheAuthoredMove_WhenTheDefinitionCarriesNoMass()
    {
        var moves = EquipmentResolver.ResolveWeaponMoves(IronSword(), instance: null, Moves());

        var slash = Assert.Single(moves);
        Assert.Equal(10, slash.Packets.Sum(p => p.Amount), 3);
        Assert.Equal(DamageType.Slashing, slash.Packets[0].Type);
        Assert.Equal(8, slash.Timing.WindupTicks);
    }

    [Fact]
    public void ResolveWeaponMoves_DerivesFromDefinitionMass()
    {
        var heavy = new EquipmentDefinition
        {
            Id = "equip.heavy_iron_sword",
            Name = "Dense Iron Sword",
            Slot = EquipmentSlot.Weapon,
            Moves = new[] { new MoveGrantSpec { Id = "move.iron_slash" } },
            Properties = new Dictionary<string, double> { [ItemProperties.Mass] = 3 },
        };

        var slash = Assert.Single(EquipmentResolver.ResolveWeaponMoves(heavy, instance: null, Moves()));

        Assert.Equal(13, slash.Packets.Sum(p => p.Amount), 3);   // 10 + mass 3 × 1.0
        Assert.Equal(8 + 6, slash.Timing.WindupTicks);           // 8 + mass 3 × 2 → slower
    }

    /// <summary>Mass lands once per move, split by packet share — a two-packet move must not
    /// collect the bonus twice, the same rule attribute scaling follows in the pipeline.</summary>
    [Fact]
    public void ResolveWeaponMoves_SplitsMassBySharAcrossPackets()
    {
        var store = new DataStore<MoveDefinition>();
        store.Add(new MoveDefinition
        {
            Id = "move.flame_slash",
            Name = "Flame Slash",
            Tags = new[] { "action:attack", "delivery:melee" },
            Packets = new[] { new Packet(DamageType.Slashing, 8), new Packet(DamageType.Slashing, "heat", 2) },
        });

        var sword = new EquipmentDefinition
        {
            Id = "equip.flame_sword",
            Slot = EquipmentSlot.Weapon,
            Moves = new[] { new MoveGrantSpec { Id = "move.flame_slash" } },
            Properties = new Dictionary<string, double> { [ItemProperties.Mass] = 5 },
        };

        var move = Assert.Single(EquipmentResolver.ResolveWeaponMoves(sword, instance: null, store));

        Assert.Equal(15, move.Packets.Sum(p => p.Amount), 3);       // 10 + mass 5, once
        Assert.Equal(12, move.Packets[0].Amount, 3);                 // 8 + 5 × 0.8
        Assert.Equal(3, move.Packets[1].Amount, 3);                  // 2 + 5 × 0.2
    }

    [Fact]
    public void ResolveArmor_AddsDefinitionHardnessAndKeepsResistances()
    {
        var hardened = new EquipmentDefinition
        {
            Id = "equip.hard_iron_armor",
            Name = "Hardened Iron Armor",
            Slot = EquipmentSlot.Body,
            Armor = new ArmorStats { Armor = 4, Resistances = new() { ["Slashing"] = 0.1 } },
            Properties = new Dictionary<string, double> { [ItemProperties.Hardness] = 4 },
        };

        var armor = EquipmentResolver.ResolveArmor(hardened, instance: null);
        Assert.Equal(4 + 2, armor.Armor);              // 4 + hardness 4 * 0.5
        Assert.Equal(0.1, armor.ResistanceFor("Slashing"));
    }
}
