using Dungeons.Items;

namespace Dungeons.Realms;

/// <summary>
/// Something the party ought to know before they walk in. Typed rather than worded: the
/// sentences live in <c>Dungeons.Presentation.PreparationText</c>, so the one-way presentation
/// rule (D30) holds here exactly as it does for materials.
///
/// <para><b>Every one of these is a warning, never a bar.</b> The player may enter a Realm
/// barefoot and unarmed if they want to; refusing to open the door is the one way to build the
/// permanently-stuck state GDD §13.1 rules out, and "you are not ready" is a judgement the
/// player gets to make.</para>
/// </summary>
public enum LoadoutIssue
{
    /// <summary>Nowhere to go. The only condition that stops entry, and it stops it because
    /// there is nothing to enter, not because the party is unprepared.</summary>
    NoRealmSelected,

    /// <summary>Nothing in hand. Combat still works — the species' bare fists are a moveset —
    /// but a weapon is where most of a build's damage lives.</summary>
    NoWeaponEquipped,

    /// <summary>No armour-bearing slot filled at all.</summary>
    NothingWorn,

    /// <summary>An equipped item whose base definition no longer resolves — a fabricated
    /// archetype that failed to come back with the save. Worth saying out loud: the item is
    /// visible in the slot and is contributing nothing.</summary>
    EquippedItemUnresolved,

    /// <summary>The pack asks for more than the Stash holds. Nothing breaks; the run takes what
    /// is actually there.</summary>
    PackedConsumableNotHeld,
}

/// <summary>One position on the character, and what is in it. An empty slot is a first-class
/// answer here rather than a gap in a list — "clearly see missing slots" is the requirement.</summary>
public sealed record LoadoutSlotStatus(EquipmentSlot Slot, string? ItemName)
{
    public bool Filled => ItemName is not null;
}

/// <summary>What the party takes if they leave right now, and what is wrong with it.</summary>
public sealed record LoadoutReport(
    IReadOnlyList<LoadoutSlotStatus> Slots,
    IReadOnlyList<LoadoutIssue> Issues,
    bool NeedsStarterKit,
    bool CanEnter)
{
    public int FilledSlotCount => Slots.Count(slot => slot.Filled);

    public bool Has(LoadoutIssue issue) => Issues.Contains(issue);
}

/// <summary>What the pack can actually take out of the Stash right now.</summary>
public sealed record PackManifest(IReadOnlyList<ItemStack> Taking, IReadOnlyList<ItemStack> Short)
{
    public bool IsShort => Short.Count > 0;
}

/// <summary>
/// Reads a prepared loadout back to the player: which positions are filled, what is wrong, and
/// whether the game owes them a starter kit.
///
/// <para>It reads state and reports; it changes nothing. That is what lets the preparation
/// screen call it on every refresh without wondering whether looking at the loadout altered
/// it.</para>
/// </summary>
public static class LoadoutCheck
{
    /// <param name="equipmentDefinitionIsKnown">Whether an equipped instance's base definition
    /// still resolves. Passed in rather than looked up so Core stays free of the content store
    /// and this stays trivially testable.</param>
    public static LoadoutReport Inspect(
        Equipment worn,
        Inventory stash,
        RunLoadout loadout,
        Func<string, bool> equipmentDefinitionIsKnown)
    {
        ArgumentNullException.ThrowIfNull(worn);
        ArgumentNullException.ThrowIfNull(stash);
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(equipmentDefinitionIsKnown);

        var slots = EquipmentSlots.DisplayOrder
            .Select(slot => new LoadoutSlotStatus(slot, worn.InSlot(slot)?.DisplayName))
            .ToList();

        var issues = new List<LoadoutIssue>();

        if (loadout.RealmId is null)
            issues.Add(LoadoutIssue.NoRealmSelected);

        var equippedWeapon = worn.InSlot(EquipmentSlot.Weapon);
        if (equippedWeapon is null)
            issues.Add(LoadoutIssue.NoWeaponEquipped);

        if (!EquipmentSlots.ArmorBearing.Any(slot => worn.InSlot(slot) is not null))
            issues.Add(LoadoutIssue.NothingWorn);

        if (worn.Slots.Values.Any(item => !equipmentDefinitionIsKnown(item.BaseDefinitionId)))
            issues.Add(LoadoutIssue.EquippedItemUnresolved);

        if (PackableFrom(loadout, stash).IsShort)
            issues.Add(LoadoutIssue.PackedConsumableNotHeld);

        return new LoadoutReport(
            slots,
            issues,
            NeedsStarterKit: !HasAnyUsableWeapon(worn, stash, equipmentDefinitionIsKnown),
            CanEnter: loadout.RealmId is not null);
    }

    /// <summary>
    /// Splits the pack into what the Stash can actually supply and what it cannot.
    ///
    /// <para>Clamping rather than refusing is the whole point. A loadout is prepared once and
    /// entered many times; between two runs the player will spend a salve at the bench without
    /// thinking about the pack, and the correct response to that is to take the two that are
    /// left, not to make them re-prepare from scratch.</para>
    /// </summary>
    public static PackManifest PackableFrom(RunLoadout loadout, Inventory stash)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(stash);

        var taking = new List<ItemStack>();
        var shortfall = new List<ItemStack>();

        foreach (var wanted in loadout.PackedStacks())
        {
            var available = Math.Min(wanted.Quantity, stash.GetQuantity(wanted.ItemId));
            if (available > 0)
                taking.Add(new ItemStack(wanted.ItemId, available));
            if (available < wanted.Quantity)
                shortfall.Add(new ItemStack(wanted.ItemId, wanted.Quantity - available));
        }

        return new PackManifest(taking, shortfall);
    }

    /// <summary>
    /// The starter-kit trigger: is there a weapon anywhere the player could put in their hand?
    /// The Stash counts — a character with a sword they simply have not equipped is not stuck,
    /// and handing them a second rusty one would be the game misreading the situation.
    /// </summary>
    private static bool HasAnyUsableWeapon(
        Equipment worn,
        Inventory stash,
        Func<string, bool> equipmentDefinitionIsKnown) =>
        worn.Slots.Values.Concat(stash.Instances).Any(item =>
            item.ItemType == ItemType.Weapon && equipmentDefinitionIsKnown(item.BaseDefinitionId));
}
