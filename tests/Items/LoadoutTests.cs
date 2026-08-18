using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Professions;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Items;

/// <summary>
/// The seven-slot loadout: how the pieces add up, and the one persistent-identifier rename the
/// expansion needed (<c>Armor</c> → <c>Body</c>, save v9).
///
/// <para>The rename is the interesting half. Slot names are save keys, so getting it wrong
/// silently drops whatever the player was wearing — the failure mode a migration exists to
/// prevent, and the reason it gets a test rather than a comment.</para>
/// </summary>
public class LoadoutTests
{
    private static EquipmentDefinition Piece(string id, EquipmentSlot slot, double armor, params (string Lane, double Value)[] resistances) => new()
    {
        Id = id,
        Name = id,
        Slot = slot,
        Armor = new ArmorStats { Armor = armor, Resistances = resistances.ToDictionary(r => r.Lane, r => r.Value) },
    };

    private static ItemInstance Instance(string definitionId, double hardness) => new()
    {
        InstanceId = 1,
        BaseDefinitionId = definitionId,
        ItemType = ItemType.Armor,
        Properties = new PropertySet(new Dictionary<string, double> { ["hardness"] = hardness }),
    };

    // --- Armour is the sum of the loadout -----------------------------------

    [Fact]
    public void WornArmourAddsUpAcrossEveryPiece()
    {
        var worn = new (EquipmentDefinition, ItemInstance?)[]
        {
            (Piece("helm", EquipmentSlot.Head, 1), Instance("helm", 2)),
            (Piece("vest", EquipmentSlot.Body, 3), Instance("vest", 4)),
            (Piece("boots", EquipmentSlot.Feet, 1), Instance("boots", 1)),
        };

        var profile = EquipmentResolver.ResolveWornArmor(worn);

        // Each piece is its own base armour plus hardness × ArmorPerHardness.
        var expected = 1 + (2 * EquipmentTuning.ArmorPerHardness)
                     + 3 + (4 * EquipmentTuning.ArmorPerHardness)
                     + 1 + (1 * EquipmentTuning.ArmorPerHardness);
        Assert.Equal(expected, profile.Armor);
    }

    [Fact]
    public void ResistancesAddPerLaneAndStayRaw()
    {
        var worn = new (EquipmentDefinition, ItemInstance?)[]
        {
            (Piece("helm", EquipmentSlot.Head, 0, ("heat", 0.30)), null),
            (Piece("vest", EquipmentSlot.Body, 0, ("heat", 0.50), ("cold", 0.20)), null),
        };

        var profile = EquipmentResolver.ResolveWornArmor(worn);

        // Raw and uncapped — the cap belongs to the pipeline (D-05a), and capping here would
        // lose the overcap that absorbs exposure.
        Assert.Equal(0.80, profile.ResistanceFor("heat"), precision: 6);
        Assert.Equal(0.20, profile.ResistanceFor("cold"), precision: 6);
    }

    [Fact]
    public void AnEmptyLoadoutMitigatesNothing() =>
        Assert.Same(ArmorProfile.None, EquipmentResolver.ResolveWornArmor(Array.Empty<(EquipmentDefinition, ItemInstance?)>()));

    // --- Which slots are armour at all --------------------------------------

    [Fact]
    public void OnlyWornSlotsBearArmour()
    {
        Assert.False(EquipmentSlots.GrantsArmor(EquipmentSlot.Weapon));
        Assert.False(EquipmentSlots.GrantsArmor(EquipmentSlot.Trinket));

        foreach (var slot in new[] { EquipmentSlot.Offhand, EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Hands, EquipmentSlot.Feet })
            Assert.True(EquipmentSlots.GrantsArmor(slot), $"{slot} should mitigate.");
    }

    [Fact]
    public void EverySlotAppearsExactlyOnceInDisplayOrder()
    {
        Assert.Equal(
            Enum.GetValues<EquipmentSlot>().OrderBy(s => s).ToList(),
            EquipmentSlots.DisplayOrder.OrderBy(s => s).ToList());
        Assert.Equal(EquipmentSlots.DisplayOrder.Count, EquipmentSlots.DisplayOrder.Distinct().Count());
    }

    // --- The v9 migration ---------------------------------------------------

    /// <summary>
    /// A save written before the slot vocabulary grew calls the torso slot <c>Armor</c>. Without
    /// the migration the key fails to parse and the item is dropped on the floor — so this is
    /// the test that says the player keeps their vest.
    /// </summary>
    [Fact]
    public void APreExpansionSaveKeepsWhatThePlayerWasWearing()
    {
        var save = new SaveData
        {
            SchemaVersion = 8,
            Equipment = new Dictionary<string, ItemInstanceSave>
            {
                [EquipmentSlots.LegacyBodySlotName] = new()
                {
                    InstanceId = 7,
                    BaseDefinitionId = "equip.iron_armor",
                    ItemType = ItemType.Armor,
                    DisplayName = "Iron Armor",
                },
            },
        };

        var equipment = new Dungeons.Items.Equipment();
        SaveMapper.Apply(save, new Inventory(), NewProfessions(), new DiscoverySystem(),
            new Dictionary<string, int>(), equipment);

        var restored = equipment.InSlot(EquipmentSlot.Body);
        Assert.NotNull(restored);
        Assert.Equal("Iron Armor", restored!.DisplayName);
    }

    /// <summary>The same rename reaches fabricated archetypes: a vest minted before the
    /// expansion stored its slot as "Armor" too.</summary>
    [Fact]
    public void APreExpansionFabricatedArchetypeRestoresToTheBodySlot()
    {
        var save = new SaveData
        {
            SchemaVersion = 8,
            EmergentEquipment = new List<EquipmentArchetypeSave>
            {
                new() { Id = "equip.emergent.abc123", Name = "Leather Vest", Slot = EquipmentSlots.LegacyBodySlotName },
            },
        };

        var store = new DataStore<EquipmentDefinition>();
        SaveMapper.Apply(save, new Inventory(), NewProfessions(), new DiscoverySystem(),
            new Dictionary<string, int>(), equipmentStore: store);

        Assert.Equal(EquipmentSlot.Body, store.GetById("equip.emergent.abc123").Slot);
    }

    [Fact]
    public void ACurrentSaveRoundTripsEverySlot()
    {
        var equipment = new Dungeons.Items.Equipment();
        foreach (var slot in EquipmentSlots.DisplayOrder)
        {
            equipment.Equip(slot, new ItemInstance
            {
                InstanceId = (long)slot + 1,
                BaseDefinitionId = $"equip.{slot}".ToLowerInvariant(),
                ItemType = slot == EquipmentSlot.Weapon ? ItemType.Weapon : ItemType.Armor,
                DisplayName = slot.ToString(),
            });
        }

        var saved = SaveMapper.Capture(
            build: null, new Inventory(), NewProfessions(), new DiscoverySystem(),
            new Dictionary<string, int>(), savedAtTick: 0, equipment);

        var restored = new Dungeons.Items.Equipment();
        SaveMapper.Apply(saved, new Inventory(), NewProfessions(), new DiscoverySystem(),
            new Dictionary<string, int>(), restored);

        foreach (var slot in EquipmentSlots.DisplayOrder)
            Assert.Equal(slot.ToString(), restored.InSlot(slot)?.DisplayName);
    }

    private static ProfessionSystem NewProfessions() =>
        new(new DataStore<ProfessionActionDefinition>(), new Inventory(), new SeededRandom(1));
}
