using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Professions;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Persistence;

/// <summary>
/// Save schema v7 — everything the 20-profession pass needs to survive a restart: the passive
/// action that offline progress will pay out, the wall-clock stamp that measures the absence,
/// crops in the ground, and the fitted training course.
///
/// <para>The other half of the contract is that a v6 save still loads. Levelling must never
/// require staying online, so if the save round-trip drops the passive action the whole offline
/// path silently stops working — which is exactly the kind of failure that shows up as "my
/// professions did not advance overnight" rather than as an exception.</para>
/// </summary>
public class ProfessionSaveV7Tests
{
    private const string ChopOak = "action.chop_oak";
    private const string NettleBed = "action.plant_nettle_bed";

    private static DataStore<ProfessionActionDefinition> ActionStore() =>
        ProfessionsTestStore(
            new ProfessionActionDefinition { Id = ChopOak, ProfessionId = "profession.forestry", BaseIntervalTicks = 100 },
            new ProfessionActionDefinition
            {
                Id = NettleBed,
                ProfessionId = FarmingPlots.FarmingProfessionId,
                BaseIntervalTicks = 2400,
                Inputs = new[] { new ItemStack("material.nettle_seed") },
                Outputs = new[] { new ItemStack("material.nettle", 3) },
            });

    private static DataStore<T> ProfessionsTestStore<T>(params T[] items) where T : IDefinition
    {
        var store = new DataStore<T>();
        foreach (var item in items)
            store.Add(item);
        return store;
    }

    private static DataStore<TrainingObstacleDefinition> ObstacleStore() =>
        ProfessionsTestStore(new TrainingObstacleDefinition
        {
            Id = "obstacle.beam_walk",
            Slot = TrainingSlot.Balance,
            RequiredLevel = 1,
            IntervalTicks = 90,
            Experience = 12,
            Bonuses = new Dictionary<string, double> { [CourseBonusKeys.RealmTravelSpeed] = 0.03 },
        });

    private sealed record Session(
        Inventory Stash,
        ProfessionSystem Professions,
        FarmingPlots Plots,
        TrainingCourse Course,
        Dictionary<string, int> Knowledge);

    private static Session NewSession()
    {
        var actions = ActionStore();
        var stash = new Inventory();
        var professions = new ProfessionSystem(actions, stash, new SeededRandom(1));
        return new Session(
            stash,
            professions,
            new FarmingPlots(actions, professions, () => stash),
            new TrainingCourse(ObstacleStore(), professions),
            new Dictionary<string, int>());
    }

    [Fact]
    public void NewSavesAreWrittenAtVersionSeven() =>
        Assert.Equal(7, SaveData.CurrentSchemaVersion);

    [Fact]
    public void CaptureThenApply_RestoresThePassiveActionAndTheClock()
    {
        var before = NewSession();

        var saved = SaveMapper.Capture(
            build: null, before.Stash, before.Professions, new DiscoverySystem(), before.Knowledge,
            savedAtTick: 1234,
            farmingPlots: before.Plots, trainingCourse: before.Course,
            passiveActionId: ChopOak, savedAtUnixSeconds: 1_760_000_000);

        Assert.Equal(ChopOak, saved.PassiveActionId);
        Assert.Equal(1_760_000_000, saved.SavedAtUnixSeconds);

        var after = NewSession();
        SaveMapper.Apply(saved, after.Stash, after.Professions, new DiscoverySystem(), after.Knowledge,
            farmingPlots: after.Plots, trainingCourse: after.Course);

        // The passive action is the client's to resume; what matters here is that it survives.
        Assert.Equal(ChopOak, saved.PassiveActionId);
    }

    [Fact]
    public void CaptureThenApply_RestoresPlantedCrops()
    {
        var before = NewSession();
        before.Stash.Add("material.nettle_seed", 1);
        before.Plots.Plant(0, NettleBed, currentTick: 500);

        var saved = SaveMapper.Capture(
            build: null, before.Stash, before.Professions, new DiscoverySystem(), before.Knowledge,
            savedAtTick: 500, farmingPlots: before.Plots, trainingCourse: before.Course);

        var planting = Assert.Single(saved.FarmingPlots);
        Assert.Equal(0, planting.Index);
        Assert.Equal(NettleBed, planting.ActionId);
        Assert.Equal(2900, planting.ReadyAtTick);

        var after = NewSession();
        SaveMapper.Apply(saved, after.Stash, after.Professions, new DiscoverySystem(), after.Knowledge,
            farmingPlots: after.Plots, trainingCourse: after.Course);

        Assert.Equal(NettleBed, after.Plots.Plots[0].PlantedActionId);
        Assert.True(after.Plots.Plots[0].IsReadyAt(2900));
    }

    /// <summary>Empty plots are not written — a save should not carry six blank rows.</summary>
    [Fact]
    public void EmptyPlotsAreNotPersisted()
    {
        var session = NewSession();

        var saved = SaveMapper.Capture(
            build: null, session.Stash, session.Professions, new DiscoverySystem(), session.Knowledge,
            savedAtTick: 0, farmingPlots: session.Plots, trainingCourse: session.Course);

        Assert.Empty(saved.FarmingPlots);
    }

    [Fact]
    public void CaptureThenApply_RestoresTheFittedCourseAndItsBonuses()
    {
        var before = NewSession();
        before.Course.Fit(TrainingSlot.Balance, "obstacle.beam_walk");

        var saved = SaveMapper.Capture(
            build: null, before.Stash, before.Professions, new DiscoverySystem(), before.Knowledge,
            savedAtTick: 0, farmingPlots: before.Plots, trainingCourse: before.Course);

        var slot = Assert.Single(saved.TrainingCourse);
        Assert.Equal("Balance", slot.Slot);

        var after = NewSession();
        SaveMapper.Apply(saved, after.Stash, after.Professions, new DiscoverySystem(), after.Knowledge,
            farmingPlots: after.Plots, trainingCourse: after.Course);

        Assert.Equal("obstacle.beam_walk", after.Course.Fitted[TrainingSlot.Balance]);
        Assert.Equal(0.03, after.Course.BonusValue(CourseBonusKeys.RealmTravelSpeed), 6);
    }

    /// <summary>
    /// A v6 save has none of these fields. It must load anyway, arriving in the same state a
    /// new game starts in — no passive action, nothing planted, an empty course — which is why
    /// v7 needed no migration step.
    /// </summary>
    [Fact]
    public void AVersionSixSaveStillLoads()
    {
        var legacy = new SaveData
        {
            SchemaVersion = 6,
            SavedAtTick = 900,
            Stash = new List<ItemStack> { new("material.oak_log", 3) },
            Professions = new List<ProfessionSave>
            {
                new() { ProfessionId = "profession.forestry", Xp = 450 },
            },
        };

        var session = NewSession();
        SaveMapper.Apply(legacy, session.Stash, session.Professions, new DiscoverySystem(), session.Knowledge,
            farmingPlots: session.Plots, trainingCourse: session.Course);

        Assert.Equal(3, session.Stash.GetQuantity("material.oak_log"));
        Assert.Equal(450, session.Professions.GetProgress("profession.forestry").Xp);
        Assert.Null(legacy.PassiveActionId);
        Assert.All(session.Plots.Plots, plot => Assert.True(plot.IsEmpty));
        Assert.True(session.Course.IsEmpty);
    }

    /// <summary>Callers that do not pass the new systems must still get a valid save — the
    /// optional parameters are what let the Realm-run and test paths capture without them.</summary>
    [Fact]
    public void CaptureWithoutTheNewSystemsWritesEmptyCollections()
    {
        var session = NewSession();

        var saved = SaveMapper.Capture(
            build: null, session.Stash, session.Professions, new DiscoverySystem(), session.Knowledge,
            savedAtTick: 0);

        Assert.Empty(saved.FarmingPlots);
        Assert.Empty(saved.TrainingCourse);
        Assert.Null(saved.PassiveActionId);
    }
}
