using Dungeons.Items;
using Dungeons.Realms;
using Xunit;

namespace Dungeons.Tests.Realms;

public class RealmExtractionTests
{
    private static RealmDefinition Forest() => new()
    {
        Id = "realm.dark_forest",
        Name = "The Dark Forest",
        Locations = new[]
        {
            new RealmLocationDefinition { Id = "entrance", Type = RealmLocationType.Entrance, Depth = 1, Connections = new[] { "extract" } },
            new RealmLocationDefinition { Id = "extract", Type = RealmLocationType.Extraction, Depth = 1, Connections = new[] { "entrance" } },
        },
    };

    [Fact]
    public void Secure_MovesRunLootToStash_AndEndsRun()
    {
        var run = new RealmRun(Forest(), 1);
        run.RunInventory.Add("material.oak_log", 3);
        run.RunInventory.Add("material.goblin_scrap", 1);

        var stash = new Inventory();
        stash.Add("material.oak_log", 5); // pre-existing

        var summary = RealmExtraction.Secure(run, stash);

        Assert.True(summary.Secured);
        Assert.Equal(4, summary.TotalQuantity); // 3 + 1
        Assert.Equal(8, stash.GetQuantity("material.oak_log")); // 5 + 3
        Assert.Equal(1, stash.GetQuantity("material.goblin_scrap"));
        Assert.Empty(run.RunInventory.Snapshot());
        Assert.False(run.Active);
    }

    [Fact]
    public void Secure_MovesInstances_ToStash()
    {
        var run = new RealmRun(Forest(), 1);
        run.RunInventory.AddInstance(new ItemInstance { InstanceId = 7, BaseDefinitionId = "equip.iron_sword", ItemType = ItemType.Weapon, DisplayName = "Looted Sword" });

        var stash = new Inventory();
        var summary = RealmExtraction.Secure(run, stash);

        Assert.Single(summary.Instances);
        Assert.Equal(1, summary.TotalQuantity);
        Assert.Equal("Looted Sword", stash.GetInstance(7)!.DisplayName);
        Assert.Equal(0, run.RunInventory.InstanceCount);
    }

    [Fact]
    public void Forfeit_LosesInstances_Too()
    {
        var run = new RealmRun(Forest(), 1);
        run.RunInventory.AddInstance(new ItemInstance { InstanceId = 7, BaseDefinitionId = "x", ItemType = ItemType.Armor });

        var summary = RealmExtraction.Forfeit(run);

        Assert.Single(summary.Instances);
        Assert.Equal(0, run.RunInventory.InstanceCount);
    }

    [Fact]
    public void Forfeit_LosesRunLoot_StashUntouched_EndsRun()
    {
        var run = new RealmRun(Forest(), 1);
        run.RunInventory.Add("material.oak_log", 3);

        var summary = RealmExtraction.Forfeit(run);

        Assert.False(summary.Secured);
        Assert.Equal(3, summary.TotalQuantity); // reported as lost
        Assert.Empty(run.RunInventory.Snapshot());
        Assert.False(run.Active);
    }

    [Fact]
    public void GatheringDuringRun_DepositsToRunInventory_ViaProvider()
    {
        // Provider returns the run bag while a run is active, else the stash.
        var stash = new Inventory();
        var run = new RealmRun(Forest(), 1);
        Inventory Provider() => run.Active ? run.RunInventory : stash;

        Provider().Add("material.oak_log", 1); // simulates a realm gather deposit
        Assert.Equal(1, run.RunInventory.GetQuantity("material.oak_log"));
        Assert.Equal(0, stash.GetQuantity("material.oak_log"));

        RealmExtraction.Secure(run, stash);
        Assert.Equal(1, stash.GetQuantity("material.oak_log")); // now secured
        Provider().Add("material.oak_log", 1); // run ended → provider now targets stash
        Assert.Equal(2, stash.GetQuantity("material.oak_log"));
    }
}
