using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Professions;

public class InventoryTests
{
    [Fact]
    public void AddAndGetQuantity_Stacks()
    {
        var inv = new Inventory();
        inv.Add("material.oak_log", 3);
        inv.Add("material.oak_log", 2);
        Assert.Equal(5, inv.GetQuantity("material.oak_log"));
    }

    [Fact]
    public void TryRemove_IsAllOrNothing()
    {
        var inv = new Inventory();
        inv.Add("material.iron_ore", 2);

        Assert.False(inv.TryRemove("material.iron_ore", 3));
        Assert.Equal(2, inv.GetQuantity("material.iron_ore")); // unchanged

        Assert.True(inv.TryRemove("material.iron_ore", 2));
        Assert.Equal(0, inv.GetQuantity("material.iron_ore"));
    }

    [Fact]
    public void CanRemoveAll_AggregatesDuplicateIds()
    {
        var inv = new Inventory();
        inv.Add("material.iron_ore", 3);

        var need = new[] { new ItemStack("material.iron_ore", 2), new ItemStack("material.iron_ore", 2) };
        Assert.False(inv.CanRemoveAll(need)); // needs 4, has 3

        inv.Add("material.iron_ore", 1);
        Assert.True(inv.CanRemoveAll(need));
    }

    [Fact]
    public void TryRemoveAll_LeavesInventoryUnchangedOnShortfall()
    {
        var inv = new Inventory();
        inv.Add("a", 1);
        inv.Add("b", 5);

        var need = new[] { new ItemStack("a", 1), new ItemStack("b", 10) };
        Assert.False(inv.TryRemoveAll(need));
        Assert.Equal(1, inv.GetQuantity("a"));
        Assert.Equal(5, inv.GetQuantity("b"));
    }

    [Fact]
    public void RaisesChangedEvent()
    {
        var inv = new Inventory();
        var changes = 0;
        inv.Changed += () => changes++;
        inv.Add("a", 1);
        inv.TryRemove("a", 1);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void AddAll_RaisesChangedOnceForTheWholeBatch()
    {
        var inv = new Inventory();
        var changes = 0;
        inv.Changed += () => changes++;

        inv.AddAll(new[] { new ItemStack("a", 1), new ItemStack("b", 2), new ItemStack("a", 3) });

        Assert.Equal(1, changes);
        Assert.Equal(4, inv.GetQuantity("a")); // duplicate ids stack
        Assert.Equal(2, inv.GetQuantity("b"));
    }

    [Fact]
    public void AddAll_EmptyBatchDoesNotRaiseChanged()
    {
        var inv = new Inventory();
        var changes = 0;
        inv.Changed += () => changes++;

        inv.AddAll(Array.Empty<ItemStack>());

        Assert.Equal(0, changes);
    }

    [Fact]
    public void AddAll_InvalidStackLeavesInventoryUnchanged()
    {
        var inv = new Inventory();
        inv.Add("a", 1);
        var changes = 0;
        inv.Changed += () => changes++;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            inv.AddAll(new[] { new ItemStack("a", 5), new ItemStack("b", 0) }));

        Assert.Equal(1, inv.GetQuantity("a")); // the valid stack was not applied either
        Assert.Equal(0, changes);
    }
}
