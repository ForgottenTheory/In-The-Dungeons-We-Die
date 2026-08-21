using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Items;

public class ItemModelTests
{
    [Fact]
    public void PropertySet_GetWithCombine_AndDropsZeros()
    {
        var set = new PropertySet(new Dictionary<string, double> { ["mass"] = 3, ["heat"] = 0 });
        Assert.Equal(3, set.Get("mass"));
        Assert.False(set.Has("heat"));           // zero dropped
        Assert.Equal(0, set.Get("missing"));
        Assert.Equal(1, set.Count);

        var with = set.With("MASS", 5);           // case-insensitive
        Assert.Equal(5, with.Get("mass"));
        Assert.Equal(3, set.Get("mass"));          // original unchanged (immutable)

        var other = new PropertySet(new Dictionary<string, double> { ["mass"] = 2, ["toxicity"] = 4 });
        var merged = set.Combine(other, (a, b) => a + b);
        Assert.Equal(5, merged.Get("mass"));
        Assert.Equal(4, merged.Get("toxicity"));
    }

    [Fact]
    public void InstanceIdSource_IsMonotonicAndSeedable()
    {
        var source = new InstanceIdSource();
        Assert.Equal(1, source.Next());
        Assert.Equal(2, source.Next());
        Assert.Equal(3, source.Peek());

        source.EnsureAtLeast(10);
        Assert.Equal(10, source.Next());
    }

    [Fact]
    public void Inventory_HoldsStacksAndInstancesIndependently()
    {
        var inv = new Inventory();
        inv.Add("material.iron_ore", 5);

        var sword = new ItemInstance { InstanceId = 1, BaseDefinitionId = "equip.iron_sword", ItemType = ItemType.Weapon, DisplayName = "Iron Sword" };
        inv.AddInstance(sword);

        Assert.Equal(5, inv.GetQuantity("material.iron_ore"));
        Assert.Equal(1, inv.InstanceCount);
        Assert.Equal("Iron Sword", inv.GetInstance(1)!.DisplayName);

        var removed = inv.RemoveInstance(1);
        Assert.Same(sword, removed);
        Assert.Equal(0, inv.InstanceCount);
        Assert.Equal(5, inv.GetQuantity("material.iron_ore")); // stacks untouched
        Assert.Null(inv.RemoveInstance(999));
    }

    [Fact]
    public void Inventory_ClearRemovesBoth_AndRaisesChangedOnce()
    {
        var inv = new Inventory();
        inv.Add("a", 1);
        inv.AddInstance(new ItemInstance { InstanceId = 1, BaseDefinitionId = "x", ItemType = ItemType.Armor });

        var changes = 0;
        inv.Changed += () => changes++;
        inv.Clear();

        Assert.Empty(inv.Snapshot());
        Assert.Equal(0, inv.InstanceCount);
        Assert.Equal(1, changes);
    }
}
