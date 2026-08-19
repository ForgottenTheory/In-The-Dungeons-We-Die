using Dungeons.Items;
using Dungeons.Realms;
using Xunit;

namespace Dungeons.Tests.Realms;

/// <summary>
/// Reading a loadout back to the player: filled and empty positions, what is wrong, and the
/// starter-kit guarantee.
///
/// <para>The load-bearing fence in this file is
/// <see cref="NoAmountOfMissingGearEverStopsThePlayerEntering"/>. GDD §13.1 promises a player can
/// never become permanently stuck, and a preparation screen that refuses to open the door is the
/// most obvious way to break that promise by accident.</para>
/// </summary>
public class LoadoutCheckTests
{
    private static ItemInstance Gear(long id, string definitionId, ItemType type, string name) => new()
    {
        InstanceId = id,
        BaseDefinitionId = definitionId,
        ItemType = type,
        DisplayName = name,
    };

    /// <summary>Every definition resolves — the normal case.</summary>
    private static readonly Func<string, bool> AllDefinitionsKnown = _ => true;

    private static RunLoadout PreparedFor(string realmId = "realm.dark_forest")
    {
        var loadout = new RunLoadout();
        loadout.SelectRealm(realmId);
        return loadout;
    }

    [Fact]
    public void EverySlotIsReported_FilledOrEmpty()
    {
        var worn = new Equipment();
        worn.Equip(EquipmentSlot.Weapon, Gear(1, "equip.rusty_sword", ItemType.Weapon, "Rusty Sword"));

        var report = LoadoutCheck.Inspect(worn, new Inventory(), PreparedFor(), AllDefinitionsKnown);

        Assert.Equal(EquipmentSlots.DisplayOrder.Count, report.Slots.Count);
        Assert.Equal(1, report.FilledSlotCount);

        var weapon = report.Slots.Single(slot => slot.Slot == EquipmentSlot.Weapon);
        Assert.True(weapon.Filled);
        Assert.Equal("Rusty Sword", weapon.ItemName);

        Assert.All(report.Slots.Where(slot => slot.Slot != EquipmentSlot.Weapon),
            slot => Assert.False(slot.Filled));
    }

    [Fact]
    public void AnEmptyWeaponHandAndBareBodyAreBothCalledOut()
    {
        var report = LoadoutCheck.Inspect(new Equipment(), new Inventory(), PreparedFor(), AllDefinitionsKnown);

        Assert.True(report.Has(LoadoutIssue.NoWeaponEquipped));
        Assert.True(report.Has(LoadoutIssue.NothingWorn));
    }

    [Fact]
    public void WearingAnythingArmourBearingClearsTheBareBodyWarning()
    {
        var worn = new Equipment();
        worn.Equip(EquipmentSlot.Feet, Gear(1, "equip.boots", ItemType.Armor, "Boots"));

        var report = LoadoutCheck.Inspect(worn, new Inventory(), PreparedFor(), AllDefinitionsKnown);

        Assert.False(report.Has(LoadoutIssue.NothingWorn));
    }

    /// <summary>A trinket is worn but is not armour — <see cref="EquipmentSlots.ArmorBearing"/>
    /// states that rather than inferring it, and this pins that the loadout screen agrees.</summary>
    [Fact]
    public void ATrinketIsNotArmour()
    {
        var worn = new Equipment();
        worn.Equip(EquipmentSlot.Trinket, Gear(1, "equip.charm", ItemType.Armor, "Charm"));

        var report = LoadoutCheck.Inspect(worn, new Inventory(), PreparedFor(), AllDefinitionsKnown);

        Assert.True(report.Has(LoadoutIssue.NothingWorn));
    }

    [Fact]
    public void GearWhoseDefinitionNoLongerResolvesIsReported()
    {
        var worn = new Equipment();
        worn.Equip(EquipmentSlot.Weapon, Gear(1, "equip.emergent.gone", ItemType.Weapon, "Something"));

        var report = LoadoutCheck.Inspect(worn, new Inventory(), PreparedFor(), definitionId => false);

        Assert.True(report.Has(LoadoutIssue.EquippedItemUnresolved));
    }

    // --- Starter protection (GDD §13.1) -------------------------------------

    [Fact]
    public void ACharacterWithNoWeaponAnywhereIsOwedAStarterKit()
    {
        var report = LoadoutCheck.Inspect(new Equipment(), new Inventory(), PreparedFor(), AllDefinitionsKnown);

        Assert.True(report.NeedsStarterKit);
    }

    /// <summary>An unequipped sword is not "stuck" — it is a click away. Handing out a second
    /// rusty one would be the game misreading the situation.</summary>
    [Fact]
    public void AWeaponSittingInTheStashIsNotStuck()
    {
        var stash = new Inventory();
        stash.AddInstance(Gear(1, "equip.iron_sword", ItemType.Weapon, "Iron Sword"));

        var report = LoadoutCheck.Inspect(new Equipment(), stash, PreparedFor(), AllDefinitionsKnown);

        Assert.False(report.NeedsStarterKit);
    }

    [Fact]
    public void AStashFullOfArmourIsStillStuck()
    {
        var stash = new Inventory();
        stash.AddInstance(Gear(1, "equip.iron_armor", ItemType.Armor, "Iron Armor"));

        var report = LoadoutCheck.Inspect(new Equipment(), stash, PreparedFor(), AllDefinitionsKnown);

        Assert.True(report.NeedsStarterKit);
    }

    /// <summary>A weapon whose definition is gone cannot be swung, so it does not count as one.</summary>
    [Fact]
    public void AnUnreadableWeaponDoesNotCountAsAWeapon()
    {
        var worn = new Equipment();
        worn.Equip(EquipmentSlot.Weapon, Gear(1, "equip.emergent.gone", ItemType.Weapon, "Something"));

        var report = LoadoutCheck.Inspect(worn, new Inventory(), PreparedFor(), definitionId => false);

        Assert.True(report.NeedsStarterKit);
    }

    // --- The door is never locked -------------------------------------------

    /// <summary>
    /// <b>The anti-soft-lock fence.</b> Naked, unarmed, carrying gear the game cannot read, with
    /// a pack the Stash cannot fill — the player may still walk in. Every gear condition is
    /// advice; the only thing that stops entry is having nowhere to go.
    /// </summary>
    [Fact]
    public void NoAmountOfMissingGearEverStopsThePlayerEntering()
    {
        var worn = new Equipment();
        worn.Equip(EquipmentSlot.Weapon, Gear(1, "equip.emergent.gone", ItemType.Weapon, "Something"));
        var loadout = PreparedFor();
        loadout.Pack("consumable.healing_salve", 3);

        var report = LoadoutCheck.Inspect(worn, new Inventory(), loadout, definitionId => false);

        Assert.True(report.Issues.Count >= 3);
        Assert.True(report.CanEnter);
    }

    [Fact]
    public void OnlyAMissingDestinationStopsEntry()
    {
        var report = LoadoutCheck.Inspect(new Equipment(), new Inventory(), new RunLoadout(), AllDefinitionsKnown);

        Assert.True(report.Has(LoadoutIssue.NoRealmSelected));
        Assert.False(report.CanEnter);
    }

    // --- Packing against the Stash ------------------------------------------

    [Fact]
    public void APackTheStashCanFillTakesEverythingAndIsNotShort()
    {
        var stash = new Inventory();
        stash.Add("consumable.healing_salve", 5);
        var loadout = PreparedFor();
        loadout.Pack("consumable.healing_salve", 3);

        var manifest = LoadoutCheck.PackableFrom(loadout, stash);

        Assert.Equal(new ItemStack("consumable.healing_salve", 3), Assert.Single(manifest.Taking));
        Assert.False(manifest.IsShort);
    }

    /// <summary>A standing plan outrunning the shelf takes what is left rather than failing —
    /// otherwise spending a salve at the bench would silently break the next run's loadout.</summary>
    [Fact]
    public void APackTheStashCannotFillIsClampedAndTheShortfallIsNamed()
    {
        var stash = new Inventory();
        stash.Add("consumable.healing_salve", 1);
        var loadout = PreparedFor();
        loadout.Pack("consumable.healing_salve", 3);

        var manifest = LoadoutCheck.PackableFrom(loadout, stash);

        Assert.Equal(new ItemStack("consumable.healing_salve", 1), Assert.Single(manifest.Taking));
        Assert.Equal(new ItemStack("consumable.healing_salve", 2), Assert.Single(manifest.Short));
    }

    [Fact]
    public void APackTheStashCannotFillAtAllTakesNothing()
    {
        var loadout = PreparedFor();
        loadout.Pack("consumable.healing_salve", 2);

        var manifest = LoadoutCheck.PackableFrom(loadout, new Inventory());

        Assert.Empty(manifest.Taking);
        Assert.Equal(new ItemStack("consumable.healing_salve", 2), Assert.Single(manifest.Short));
    }

    [Fact]
    public void AShortPackShowsUpAsAnIssueOnTheReport()
    {
        var loadout = PreparedFor();
        loadout.Pack("consumable.healing_salve", 2);

        var report = LoadoutCheck.Inspect(new Equipment(), new Inventory(), loadout, AllDefinitionsKnown);

        Assert.True(report.Has(LoadoutIssue.PackedConsumableNotHeld));
    }
}
