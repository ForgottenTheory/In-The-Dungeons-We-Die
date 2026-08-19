using Dungeons.Items;
using Dungeons.Professions;
using Xunit;
using static Dungeons.Tests.Professions.ProfessionsTestData;

namespace Dungeons.Tests.Professions;

/// <summary>
/// Offline progression. The design rule is blunt — normal profession levelling must never
/// require staying online — so these tests care most that an absence pays out through the same
/// execute path as live passive play, at the same rate, and that it is bounded at both ends.
/// </summary>
public class OfflineProgressTests
{
    private const string ChopOak = "action.chop_oak";

    private static ProfessionActionDefinition Chopping(int intervalTicks = 100) => new()
    {
        Id = ChopOak,
        ProfessionId = "profession.forestry",
        BaseIntervalTicks = intervalTicks,
        Experience = 10,
        Outputs = new[] { Amount("material.oak_log") },
    };

    private static ProfessionActionDefinition Sawing() => new()
    {
        Id = "action.saw_oak_planks",
        ProfessionId = "profession.forestry",
        BaseIntervalTicks = 100,
        Experience = 15,
        Inputs = new[] { Amount("material.oak_log") },
        Outputs = new[] { Amount("material.oak_plank", 2) },
    };

    /// <summary>Seconds of absence that buy exactly <paramref name="completions"/> completions
    /// of a 100-tick action at 20 ticks per second.</summary>
    private static double SecondsFor(int completions) => completions * 100.0 / ProfessionTuning.TicksPerSecond;

    [Fact]
    public void AnAbsenceConvertsIntoCompletions()
    {
        var inventory = new Inventory();
        var system = new ProfessionSystem(Store(Chopping()), inventory, new FakeRandom());

        var report = OfflineProgressCalculator.Apply(system, ChopOak, SecondsFor(10));

        Assert.Equal(10, report.CompletedActions);
        Assert.Equal(100, report.XpGained);
        Assert.Equal(10, inventory.GetQuantity("material.oak_log"));
        Assert.Equal(OfflineStopReason.TimeConsumed, report.StopReason);
    }

    /// <summary>One line per item, not one per completion — the player reads a summary.</summary>
    [Fact]
    public void ProducedOutputIsAggregatedPerItem()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());

        var report = OfflineProgressCalculator.Apply(system, ChopOak, SecondsFor(7));

        var line = Assert.Single(report.Produced);
        Assert.Equal("material.oak_log", line.ItemId);
        Assert.Equal(7, line.Quantity);
    }

    /// <summary>
    /// The rate guarantee: a completion earned while away is worth exactly a completion earned
    /// at the keyboard passively — same XP, same items, no active bonus either way. If these
    /// ever diverge, offline stops being a parallel path and becomes a penalty (or an exploit).
    /// </summary>
    [Fact]
    public void AnOfflineCompletionPaysExactlyWhatALivePassiveOneDoes()
    {
        var offlineInventory = new Inventory();
        var offline = new ProfessionSystem(Store(Chopping()), offlineInventory, new FakeRandom());
        var offlineReport = OfflineProgressCalculator.Apply(offline, ChopOak, SecondsFor(25));

        var liveInventory = new Inventory();
        var live = new ProfessionSystem(Store(Chopping()), liveInventory, new FakeRandom());
        long liveXp = 0;
        for (var i = 0; i < offlineReport.CompletedActions; i++)
            liveXp += live.Execute(ChopOak, performance: 0.0, isActive: false).XpGained;

        Assert.Equal(liveXp, offlineReport.XpGained);
        Assert.Equal(liveInventory.GetQuantity("material.oak_log"), offlineInventory.GetQuantity("material.oak_log"));
        Assert.Equal(
            live.GetProgress("profession.forestry").GetMastery(ChopOak),
            offline.GetProgress("profession.forestry").GetMastery(ChopOak));
    }

    /// <summary>
    /// Mastery earned partway through an absence shortens the completions still to come, which
    /// is what a live passive runner does when it re-reads the interval each cycle. So a long
    /// absence fits *more* completions than the naive time ÷ base-interval figure — that is the
    /// behaviour, not a rounding accident.
    /// </summary>
    [Fact]
    public void MasteryEarnedWhileAwayShortensTheRemainingIntervals()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom())
        {
            MasteryBenefits = TestPaths.ShippedMasteryLadder(),
        };
        var oneHour = 60 * 60.0;
        var naiveCompletions = (int)(oneHour * ProfessionTuning.TicksPerSecond / 100);

        var report = OfflineProgressCalculator.Apply(system, ChopOak, oneHour);

        Assert.True(report.CompletedActions > naiveCompletions,
            $"expected mastery to buy more than the naive {naiveCompletions}, got {report.CompletedActions}");
    }

    [Fact]
    public void MasteryAndXpAccrueWhileAway()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());

        var report = OfflineProgressCalculator.Apply(system, ChopOak, SecondsFor(12));

        Assert.Equal(12, report.MasteryGained);
        Assert.Equal(12, system.GetProgress("profession.forestry").GetMastery(ChopOak));
        Assert.Equal(120, system.GetProgress("profession.forestry").Xp);
    }

    [Fact]
    public void RunningOutOfInputsStopsThePayoutEarly()
    {
        var inventory = new Inventory();
        inventory.Add("material.oak_log", 3);
        var system = new ProfessionSystem(Store(Sawing()), inventory, new FakeRandom());

        var report = OfflineProgressCalculator.Apply(system, "action.saw_oak_planks", SecondsFor(50));

        Assert.Equal(3, report.CompletedActions);
        Assert.Equal(OfflineStopReason.InputsExhausted, report.StopReason);
        Assert.Equal(6, inventory.GetQuantity("material.oak_plank"));
    }

    [Fact]
    public void ALongAbsenceIsCappedAndSaysSo()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());
        var aWeek = 7 * 24 * 60 * 60.0;

        var report = OfflineProgressCalculator.Apply(system, ChopOak, aWeek);

        Assert.Equal(OfflineStopReason.TimeCapped, report.StopReason);
        Assert.True(report.ElapsedTicks <= ProfessionTuning.MaxOfflineTicks,
            $"paid out {report.ElapsedTicks} ticks against a cap of {ProfessionTuning.MaxOfflineTicks}");
        Assert.True(report.CompletedActions <= ProfessionTuning.MaxOfflineCompletions);
    }

    [Fact]
    public void NoTimeAwayEarnsNothing()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());

        var report = OfflineProgressCalculator.Apply(system, ChopOak, elapsedRealSeconds: 0);

        Assert.False(report.EarnedAnything);
        Assert.Equal(0, report.CompletedActions);
    }

    /// <summary>An absence shorter than one completion buys nothing — no partial credit, and no
    /// crediting a completion that did not happen.</summary>
    [Fact]
    public void PartialIntervalsAreNotPaidOut()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());

        var report = OfflineProgressCalculator.Apply(system, ChopOak, elapsedRealSeconds: 4.9); // 98 ticks

        Assert.Equal(0, report.CompletedActions);
    }

    [Fact]
    public void AnUnknownOrLockedActionEarnsNothing()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());

        Assert.False(OfflineProgressCalculator.Apply(system, "action.nonexistent", SecondsFor(10)).EarnedAnything);
    }

    /// <summary>
    /// Offline is passive, and passive never rolls opportunities — so no amount of time away
    /// can surface one. This is the same guarantee as
    /// <see cref="OpportunityTests.PassiveNeverDiscoversAnything"/>, checked through the
    /// offline path because that is where a player would most want it to leak.
    /// </summary>
    [Fact]
    public void TimeAwayNeverSurfacesOpportunities()
    {
        var action = new ProfessionActionDefinition
        {
            Id = ChopOak,
            ProfessionId = "profession.forestry",
            BaseIntervalTicks = 100,
            Experience = 10,
            Outputs = new[] { Amount("material.oak_log") },
            Opportunities = new[]
            {
                new ProfessionOpportunityDefinition
                {
                    Id = "opportunity.burl",
                    Prompt = "A burl.",
                    DiscoveryChance = 1.0,
                    Outputs = new[] { Amount("material.oak_bark", 5) },
                    Experience = 30,
                },
            },
        };

        var inventory = new Inventory();
        var system = new ProfessionSystem(Store(action), inventory, new FakeRandom(@default: 0.0));

        OfflineProgressCalculator.Apply(system, ChopOak, SecondsFor(20));

        Assert.Equal(0, inventory.GetQuantity("material.oak_bark"));
    }
}
