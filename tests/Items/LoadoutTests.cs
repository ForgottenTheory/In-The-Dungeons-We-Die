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

    private static ItemInstance Instance(string definitionId) => new()
    {
        InstanceId = 1,
        BaseDefinitionId = definitionId,
        ItemType = ItemType.Armor,
    };

    private static EquipmentDefinition Hardened(EquipmentDefinition piece, double hardness) => new()
    {
        Id = piece.Id,
        Name = piece.Name,
        Slot = piece.Slot,
        Armor = piece.Armor,
        Properties = new Dictionary<string, double> { ["hardness"] = hardness },
    };

    // --- Armour is the sum of the loadout -----------------------------------

    [Fact]
    public void WornArmourAddsUpAcrossEveryPiece()
    {
        var worn = new (EquipmentDefinition, ItemInstance?)[]
        {
            (Hardened(Piece("helm", EquipmentSlot.Head, 1), 2), null),
            (Hardened(Piece("vest", EquipmentSlot.Body, 3), 4), null),
            (Hardened(Piece("boots", EquipmentSlot.Feet, 1), 1), null),
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

        // Rings are worn and mitigate nothing — the same ruling as the trinket. Nine slots means
        // nine chances for "worn" to be mistaken for "armour".
        foreach (var ring in EquipmentSlots.RingPositions)
            Assert.False(EquipmentSlots.GrantsArmor(ring), $"{ring} should not mitigate.");

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

    // --- The two ring positions ---------------------------------------------

    /// <summary>
    /// The failure this exists to prevent: every ring definition names <c>Ring1</c>, so equipping
    /// by declared slot would put the second ring on top of the first and the player would own a
    /// ring slot they could never fill.
    /// </summary>
    [Fact]
    public void ASecondRingGoesOnTheOtherHandRatherThanDisplacingTheFirst()
    {
        var equipment = new Dungeons.Items.Equipment();
        var first = Instance("ring.first");
        var second = new ItemInstance { InstanceId = 2, BaseDefinitionId = "ring.second", ItemType = ItemType.Armor };

        Assert.Null(equipment.EquipInFirstFreePosition(EquipmentSlot.Ring1, first));
        Assert.Null(equipment.EquipInFirstFreePosition(EquipmentSlot.Ring1, second));

        Assert.Same(first, equipment.InSlot(EquipmentSlot.Ring1));
        Assert.Same(second, equipment.InSlot(EquipmentSlot.Ring2));
    }

    /// <summary>With both hands full a third ring has to evict one, and it must always be the
    /// same predictable one rather than whichever the dictionary happened to yield.</summary>
    [Fact]
    public void AThirdRingDisplacesTheFirstPosition()
    {
        var equipment = new Dungeons.Items.Equipment();
        var first = Instance("ring.first");
        equipment.EquipInFirstFreePosition(EquipmentSlot.Ring1, first);
        equipment.EquipInFirstFreePosition(EquipmentSlot.Ring1, Instance("ring.second"));

        var displaced = equipment.EquipInFirstFreePosition(EquipmentSlot.Ring1, Instance("ring.third"));

        Assert.Same(first, displaced);
        Assert.Equal("ring.third", equipment.InSlot(EquipmentSlot.Ring1)!.BaseDefinitionId);
        Assert.Equal("ring.second", equipment.InSlot(EquipmentSlot.Ring2)!.BaseDefinitionId);
    }

    /// <summary>Every other slot has exactly one position, so the free-position rule must behave
    /// exactly like <c>Equip</c> there — a second vest replaces the first.</summary>
    [Fact]
    public void ASlotWithOnePositionStillReplacesWhatIsThere()
    {
        var equipment = new Dungeons.Items.Equipment();
        var firstVest = Instance("vest.first");
        equipment.EquipInFirstFreePosition(EquipmentSlot.Body, firstVest);

        var displaced = equipment.EquipInFirstFreePosition(EquipmentSlot.Body, Instance("vest.second"));

        Assert.Same(firstVest, displaced);
        Assert.Equal("vest.second", equipment.InSlot(EquipmentSlot.Body)!.BaseDefinitionId);
    }

    [Fact]
    public void OnlyRingsHaveMoreThanOnePosition()
    {
        foreach (var slot in EquipmentSlots.DisplayOrder)
        {
            var positions = EquipmentSlots.InterchangeablePositions(slot);
            if (EquipmentSlots.RingPositions.Contains(slot))
                Assert.Equal(EquipmentSlots.RingPositions, positions);
            else
                Assert.Equal(new[] { slot }, positions);
        }
    }

    // --- The v9 migration ---------------------------------------------------

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
