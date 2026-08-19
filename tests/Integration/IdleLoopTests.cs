using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Professions;
using Dungeons.Randomness;
using Dungeons.Simulation;
using Xunit;

namespace Dungeons.Tests.Integration;

/// <summary>
/// Phase 10's definition of done, as one test each: <b>choose passive training → close the game →
/// come back later → receive correctly simulated progress</b>, and the same profession system
/// serving online passive, offline passive and active play without duplicated logic.
///
/// <para>The client half (wall-clock, the crop rebase, the panel) is <c>GameRoot</c>'s and is not
/// reachable from a headless test. Everything <em>authoritative</em> is here: the selection
/// survives the save, the absence pays out through the same execute path, and the three modes
/// produce the same work from the same definition.</para>
/// </summary>
public class IdleLoopTests
{
    private const string ChopOak = "action.chop_oak";
    private const string Forestry = "profession.forestry";

    private static DataStore<ProfessionActionDefinition> Actions()
    {
        var store = new DataStore<ProfessionActionDefinition>();
        store.Add(new ProfessionActionDefinition
        {
            Id = ChopOak,
            ProfessionId = Forestry,
            Name = "Chop Oak",
            BaseIntervalTicks = 100,
            Experience = 10,
            Outputs = new[] { new ItemStack("material.oak_log") },
        });
        return store;
    }

    private sealed record Session(
        TickEngine Tick,
        Inventory Stash,
        ProfessionSystem Professions,
        PassiveProfessionRunner Runner,
        FarmingPlots Plots);

    private static Session NewSession()
    {
        var actions = Actions();
        var tick = new TickEngine();
        var stash = new Inventory();
        var professions = new ProfessionSystem(actions, stash, new SeededRandom(7));
        return new Session(
            tick, stash, professions,
            new PassiveProfessionRunner(tick, professions),
            new FarmingPlots(actions, professions, () => stash));
    }

    /// <summary>
    /// The headline requirement. Twelve hours of absence at a 100-tick interval is 8,640
    /// completions — and every one of them ran <see cref="ProfessionSystem.Execute"/>, the same
    /// method the live runner calls.
    /// </summary>
    [Fact]
    public void ChoosePassive_Close_ReturnLater_AndTheProgressIsWaiting()
    {
        // --- Session one: choose something to train, then save and close ---------
        var before = NewSession();
        Assert.True(before.Runner.Start(ChopOak));

        var saved = SaveMapper.Capture(
            build: null, before.Stash, before.Professions, new DiscoverySystem(),
            new Dictionary<string, int>(),
            savedAtTick: before.Tick.CurrentTick,
            farmingPlots: before.Plots,
            passiveActionId: before.Runner.SelectedActionId,
            savedAtUnixSeconds: 1_760_000_000);

        Assert.Equal(ChopOak, saved.PassiveActionId);

        // --- Session two: a fresh process, four hours later ----------------------
        var after = NewSession();
        SaveMapper.Apply(saved, after.Stash, after.Professions, new DiscoverySystem(),
            new Dictionary<string, int>(), farmingPlots: after.Plots);

        const long fourHours = 4 * 3600;
        var report = AwayProgress.Resolve(
            after.Professions, saved.PassiveActionId, fourHours, after.Plots, after.Tick.CurrentTick);

        var expectedCompletions = fourHours * ProfessionTuning.TicksPerSecond / 100;
        Assert.Equal(expectedCompletions, report.PassiveWork!.CompletedActions);
        Assert.Equal(expectedCompletions, after.Stash.GetQuantity("material.oak_log"));
        Assert.True(report.LevelsGained.Count > 0);

        // …and the selection picks itself back up, so the second session continues the first.
        Assert.True(after.Runner.Start(saved.PassiveActionId!));
        Assert.True(after.Runner.IsRunning);
    }

    /// <summary>
    /// One system, three modes. Online passive, offline passive and active play all come out of
    /// the same definition through the same <see cref="ProfessionSystem.Execute"/>, so the same
    /// wall-clock buys the same work — and the only difference active play makes is the two
    /// things it is <em>supposed</em> to: better XP, and opportunities.
    /// </summary>
    [Fact]
    public void OnlinePassiveOfflinePassiveAndActivePlayShareOnePath()
    {
        // Online passive: run the tick engine for exactly ten intervals.
        var online = NewSession();
        online.Runner.Start(ChopOak);
        online.Tick.Advance(1000);

        // Offline passive: the same span as wall-clock.
        var offline = NewSession();
        var offlineReport = OfflineProgressCalculator.Apply(
            offline.Professions, ChopOak, elapsedRealSeconds: 1000.0 / ProfessionTuning.TicksPerSecond);

        Assert.Equal(10, offlineReport.CompletedActions);
        Assert.Equal(
            online.Stash.GetQuantity("material.oak_log"),
            offline.Stash.GetQuantity("material.oak_log"));
        Assert.Equal(
            online.Professions.GetProgress(Forestry).Xp,
            offline.Professions.GetProgress(Forestry).Xp);

        // Active play: ten attempts of the same action, at perfect timing.
        var active = NewSession();
        for (var attempt = 0; attempt < 10; attempt++)
            active.Professions.Execute(ChopOak, performance: 1.0, isActive: true);

        // Same items — active is not a second yield model…
        Assert.Equal(
            online.Stash.GetQuantity("material.oak_log"),
            active.Stash.GetQuantity("material.oak_log"));

        // …it is paid in XP and in opportunities, which passive structurally cannot roll.
        Assert.True(active.Professions.GetProgress(Forestry).Xp > online.Professions.GetProgress(Forestry).Xp);
    }

    /// <summary>
    /// Offline is limited by materials, exactly as live passive is — an absence cannot conjure
    /// inputs that were not in the stash, and the report says so rather than quietly paying less.
    /// </summary>
    [Fact]
    public void OfflineProgressIsLimitedByTheMaterialsThatWereActuallyThere()
    {
        var actions = new DataStore<ProfessionActionDefinition>();
        actions.Add(new ProfessionActionDefinition
        {
            Id = "action.saw_planks",
            ProfessionId = Forestry,
            BaseIntervalTicks = 100,
            Experience = 10,
            Inputs = new[] { new ItemStack("material.oak_log") },
            Outputs = new[] { new ItemStack("material.oak_plank", 2) },
        });

        var stash = new Inventory();
        stash.Add("material.oak_log", 5);
        var professions = new ProfessionSystem(actions, stash, new SeededRandom(7));

        var report = AwayProgress.Resolve(professions, "action.saw_planks", elapsedRealSeconds: 4 * 3600);

        Assert.Equal(5, report.PassiveWork!.CompletedActions);
        Assert.Equal(OfflineStopReason.InputsExhausted, report.StopReason);
        Assert.Equal(0, stash.GetQuantity("material.oak_log"));
    }

    /// <summary>
    /// Offline has a lower ceiling than active play, and it is <b>structural</b> rather than a
    /// tuning number: an absence never rolls for an opportunity at all, because only the active
    /// path rolls for one. Nothing about the offline path can be retuned into breaking this.
    /// </summary>
    [Fact]
    public void AnAbsenceCanNeverSurfaceAnOpportunity()
    {
        var actions = new DataStore<ProfessionActionDefinition>();
        actions.Add(new ProfessionActionDefinition
        {
            Id = ChopOak,
            ProfessionId = Forestry,
            BaseIntervalTicks = 100,
            Experience = 10,
            Outputs = new[] { new ItemStack("material.oak_log") },
            Opportunities = new[]
            {
                new ProfessionOpportunityDefinition
                {
                    Id = "opportunity.hollow_trunk",
                    Name = "Hollow Trunk",
                    DiscoveryChance = 1.0, // certain — on the path that rolls at all
                    Outputs = new[] { new ItemStack("material.oak_log", 10) },
                },
            },
        });

        var offered = new List<string>();
        var professions = new ProfessionSystem(actions, new Inventory(), new SeededRandom(7));
        professions.ActionCompleted += outcome =>
        {
            if (outcome.DiscoveredOpportunity is { } opportunity)
                offered.Add(opportunity.Id);
        };

        AwayProgress.Resolve(professions, ChopOak, elapsedRealSeconds: 4 * 3600);
        Assert.Empty(offered);

        // The same action, actively, offers it on the first attempt.
        professions.Execute(ChopOak, performance: 1.0, isActive: true);
        Assert.Single(offered);
    }

    /// <summary>
    /// An absence is safe and predictable: the same save and the same elapsed time pay the same
    /// thing. Idle progress a player cannot predict is idle progress they cannot plan around.
    /// </summary>
    [Fact]
    public void TheSameAbsencePaysTheSameThingEveryTime()
    {
        static long LogsAfterAnHour()
        {
            var session = NewSession();
            AwayProgress.Resolve(session.Professions, ChopOak, elapsedRealSeconds: 3600);
            return session.Stash.GetQuantity("material.oak_log");
        }

        Assert.Equal(LogsAfterAnHour(), LogsAfterAnHour());
    }
}
