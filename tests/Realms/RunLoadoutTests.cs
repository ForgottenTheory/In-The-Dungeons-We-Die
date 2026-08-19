using Dungeons.Items;
using Dungeons.Realms;
using Xunit;

namespace Dungeons.Tests.Realms;

/// <summary>The prepared loadout: where the party is going and what they mean to take.</summary>
public class RunLoadoutTests
{
    [Fact]
    public void ANewLoadoutHasNoDestinationAndAnEmptyPack()
    {
        var loadout = new RunLoadout();

        Assert.Null(loadout.RealmId);
        Assert.Empty(loadout.PackedConsumables);
    }

    [Fact]
    public void PackingTheSameItemTwiceAccumulates()
    {
        var loadout = new RunLoadout();

        loadout.Pack("consumable.healing_salve", 2);
        loadout.Pack("consumable.healing_salve");

        Assert.Equal(3, loadout.PackedQuantity("consumable.healing_salve"));
    }

    /// <summary>"Take it out" must mean the same thing however many times it is clicked —
    /// an unpack that threw or went negative would be a button the player can break.</summary>
    [Fact]
    public void UnpackingMoreThanIsPackedEmptiesTheEntryRatherThanGoingNegative()
    {
        var loadout = new RunLoadout();
        loadout.Pack("consumable.healing_salve", 2);

        loadout.Unpack("consumable.healing_salve", 5);

        Assert.Equal(0, loadout.PackedQuantity("consumable.healing_salve"));
        Assert.Empty(loadout.PackedConsumables);
    }

    [Fact]
    public void UnpackingSomethingThatWasNeverPackedDoesNothing()
    {
        var loadout = new RunLoadout();

        loadout.Unpack("consumable.healing_salve");

        Assert.Empty(loadout.PackedConsumables);
    }

    [Fact]
    public void ClearingThePackLeavesTheDestinationAlone()
    {
        var loadout = new RunLoadout();
        loadout.SelectRealm("realm.dark_forest");
        loadout.Pack("consumable.healing_salve", 3);

        loadout.ClearPacked();

        Assert.Equal("realm.dark_forest", loadout.RealmId);
        Assert.Empty(loadout.PackedConsumables);
    }

    /// <summary>The pack is written to the save and drawn on screen; an unstable order would
    /// make two identical loadouts look and serialise differently.</summary>
    [Fact]
    public void PackedStacksComeOutInAStableOrder()
    {
        var loadout = new RunLoadout();
        loadout.Pack("consumable.zeta", 1);
        loadout.Pack("consumable.alpha", 2);
        loadout.Pack("consumable.mid", 3);

        Assert.Equal(
            new[] { "consumable.alpha", "consumable.mid", "consumable.zeta" },
            loadout.PackedStacks().Select(stack => stack.ItemId));
    }

    [Fact]
    public void RestoreReplacesTheWholeLoadout()
    {
        var loadout = new RunLoadout();
        loadout.SelectRealm("realm.meadow");
        loadout.Pack("consumable.old", 4);

        loadout.Restore("realm.dark_forest", new[] { new ItemStack("consumable.healing_salve", 2) });

        Assert.Equal("realm.dark_forest", loadout.RealmId);
        Assert.Equal(2, loadout.PackedQuantity("consumable.healing_salve"));
        Assert.Equal(0, loadout.PackedQuantity("consumable.old"));
    }

    /// <summary>A v9 save carries no loadout at all, and that must restore as "never prepared"
    /// rather than throwing.</summary>
    [Fact]
    public void RestoringNothingIsTheStateOfAPlayerWhoHasNeverPrepared()
    {
        var loadout = new RunLoadout();
        loadout.SelectRealm("realm.dark_forest");
        loadout.Pack("consumable.healing_salve");

        loadout.Restore(null, Array.Empty<ItemStack>());

        Assert.Null(loadout.RealmId);
        Assert.Empty(loadout.PackedConsumables);
    }

    [Fact]
    public void EveryChangeRaisesChanged()
    {
        var loadout = new RunLoadout();
        var changes = 0;
        loadout.Changed += () => changes++;

        loadout.SelectRealm("realm.dark_forest");
        loadout.Pack("consumable.healing_salve");
        loadout.Unpack("consumable.healing_salve");

        Assert.Equal(3, changes);
    }

    [Fact]
    public void SelectingTheRealmAlreadySelectedIsNotAChange()
    {
        var loadout = new RunLoadout();
        loadout.SelectRealm("realm.dark_forest");
        var changes = 0;
        loadout.Changed += () => changes++;

        loadout.SelectRealm("realm.dark_forest");

        Assert.Equal(0, changes);
    }
}
