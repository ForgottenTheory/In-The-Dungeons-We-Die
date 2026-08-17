using Dungeons.Items;
using Dungeons.Professions;
using Xunit;
using static Dungeons.Tests.Professions.ProfessionsTestData;

namespace Dungeons.Tests.Professions;

/// <summary>
/// Farming's plots — the one profession that runs in parallel with itself. The behaviour worth
/// pinning is the split payment: the seed is taken at planting, the crop arrives much later,
/// and the harvest must not ask for the seed a second time.
/// </summary>
public class FarmingPlotsTests
{
    private const string NettleBed = "action.plant_nettle_bed";
    private const int GrowTicks = 2400;

    private static ProfessionActionDefinition Bed(int requiredLevel = 1) => new()
    {
        Id = NettleBed,
        ProfessionId = FarmingPlots.FarmingProfessionId,
        Name = "Plant a Nettle Bed",
        RequiredLevel = requiredLevel,
        BaseIntervalTicks = GrowTicks,
        Experience = 40,
        Inputs = new[] { Amount("material.nettle_seed") },
        Outputs = new[] { Amount("material.nettle", 3), Amount("material.nettle_seed", 2) },
    };

    private static (FarmingPlots Plots, ProfessionSystem System, Inventory Bag) Hideout(
        ProfessionActionDefinition? action = null)
    {
        var store = Store(action ?? Bed());
        var bag = new Inventory();
        var system = new ProfessionSystem(store, bag, new FakeRandom());
        return (new FarmingPlots(store, system, () => bag), system, bag);
    }

    [Fact]
    public void PlantingTakesTheSeedImmediately()
    {
        var (plots, _, bag) = Hideout();
        bag.Add("material.nettle_seed", 1);

        Assert.Equal(PlotFailure.None, plots.Plant(0, NettleBed, currentTick: 0));
        Assert.Equal(0, bag.GetQuantity("material.nettle_seed"));
    }

    /// <summary>
    /// The prepaid-harvest rule. The seed left the bag at planting, so by harvest time the
    /// player no longer holds the input — and the harvest must complete anyway.
    /// </summary>
    [Fact]
    public void HarvestDoesNotChargeForTheSeedAgain()
    {
        var (plots, _, bag) = Hideout();
        bag.Add("material.nettle_seed", 1);
        plots.Plant(0, NettleBed, currentTick: 0);

        var outcome = plots.Harvest(0, currentTick: GrowTicks, out var failure);

        Assert.Equal(PlotFailure.None, failure);
        Assert.True(outcome!.Success);
        Assert.Empty(outcome.Consumed);
        Assert.Equal(3, bag.GetQuantity("material.nettle"));
        Assert.Equal(2, bag.GetQuantity("material.nettle_seed")); // the bed reseeds itself
    }

    [Fact]
    public void ACropCannotBeLiftedEarly()
    {
        var (plots, _, bag) = Hideout();
        bag.Add("material.nettle_seed", 1);
        plots.Plant(0, NettleBed, currentTick: 0);

        Assert.Null(plots.Harvest(0, currentTick: GrowTicks - 1, out var failure));
        Assert.Equal(PlotFailure.StillGrowing, failure);
    }

    [Fact]
    public void HarvestingClearsThePlotForReplanting()
    {
        var (plots, _, bag) = Hideout();
        bag.Add("material.nettle_seed", 1);
        plots.Plant(0, NettleBed, currentTick: 0);
        plots.Harvest(0, GrowTicks, out _);

        Assert.True(plots.Plots[0].IsEmpty);
        Assert.Equal(PlotFailure.None, plots.Plant(0, NettleBed, currentTick: GrowTicks));
    }

    [Fact]
    public void AnOccupiedPlotRefusesASecondPlanting()
    {
        var (plots, _, bag) = Hideout();
        bag.Add("material.nettle_seed", 2);
        plots.Plant(0, NettleBed, currentTick: 0);

        Assert.Equal(PlotFailure.PlotOccupied, plots.Plant(0, NettleBed, currentTick: 10));
    }

    [Fact]
    public void PlantingWithoutASeedIsRefused()
    {
        var (plots, _, _) = Hideout();

        Assert.Equal(PlotFailure.ActionUnavailable, plots.Plant(0, NettleBed, currentTick: 0));
    }

    /// <summary>
    /// Plots are tended at the Hideout, so they read the Stash — while profession actions
    /// deposit into whichever bag is active, which inside a Realm is the unsecured run
    /// inventory. Planting must check the bag it is about to take the seed out of; asking the
    /// profession system would read the wrong one and take the seed anyway.
    /// </summary>
    [Fact]
    public void PlantingChecksTheStashItSpends_NotWhicheverBagIsActive()
    {
        var actions = Store(Bed());
        var stash = new Inventory();
        var runInventory = new Inventory();

        // The profession system is pointed at the run inventory, as it is inside a Realm.
        var system = new ProfessionSystem(actions, () => runInventory, new FakeRandom());
        var plots = new FarmingPlots(actions, system, () => stash);

        runInventory.Add("material.nettle_seed", 1);
        Assert.Equal(PlotFailure.ActionUnavailable, plots.Plant(0, NettleBed, currentTick: 0));
        Assert.Equal(1, runInventory.GetQuantity("material.nettle_seed")); // untouched

        stash.Add("material.nettle_seed", 1);
        Assert.Equal(PlotFailure.None, plots.Plant(0, NettleBed, currentTick: 0));
        Assert.Equal(0, stash.GetQuantity("material.nettle_seed"));
        Assert.Equal(1, runInventory.GetQuantity("material.nettle_seed"));
    }

    /// <summary>Parallelism is the reason this system exists: plots run independently.</summary>
    [Fact]
    public void PlotsGrowInParallelAndReadyOnesHarvestTogether()
    {
        var (plots, system, bag) = Hideout();
        system.GetProgress(FarmingPlots.FarmingProfessionId).AddXp(ProfessionLeveling.XpForLevel(30));
        bag.Add("material.nettle_seed", 3);

        plots.Plant(0, NettleBed, currentTick: 0);
        plots.Plant(1, NettleBed, currentTick: 0);
        plots.Plant(2, NettleBed, currentTick: 500);

        var lifted = plots.HarvestAllReady(currentTick: GrowTicks);

        Assert.Equal(2, lifted.Count);           // the third was planted 500 ticks later
        Assert.False(plots.Plots[2].IsEmpty);
    }

    [Fact]
    public void PlotsUnlockWithFarmingLevel()
    {
        Assert.Equal(1, FarmingTuning.UnlockedPlots(1));
        Assert.Equal(2, FarmingTuning.UnlockedPlots(5));
        Assert.Equal(FarmingTuning.MaximumPlots, FarmingTuning.UnlockedPlots(99));

        var (plots, system, bag) = Hideout();
        bag.Add("material.nettle_seed", 2);

        Assert.Equal(PlotFailure.PlotLocked, plots.Plant(1, NettleBed, currentTick: 0));

        system.GetProgress(FarmingPlots.FarmingProfessionId).AddXp(ProfessionLeveling.XpForLevel(5));
        Assert.Equal(PlotFailure.None, plots.Plant(1, NettleBed, currentTick: 0));
    }

    [Fact]
    public void PlantableActionsRespectTheLevelGate()
    {
        var (plots, system, _) = Hideout(Bed(requiredLevel: 20));

        Assert.Empty(plots.PlantableActions());

        system.GetProgress(FarmingPlots.FarmingProfessionId).AddXp(ProfessionLeveling.XpForLevel(20));
        Assert.Single(plots.PlantableActions());
    }

    [Fact]
    public void RestoreRebuildsPlantingsAndDropsActionsContentNoLongerHas()
    {
        var (plots, _, _) = Hideout();

        plots.Restore(new[]
        {
            (0, NettleBed, 5_000L),
            (1, "action.deleted_from_content", 5_000L),
        });

        Assert.Equal(NettleBed, plots.Plots[0].PlantedActionId);
        Assert.Equal(5_000, plots.Plots[0].ReadyAtTick);
        Assert.True(plots.Plots[1].IsEmpty);
    }
}
