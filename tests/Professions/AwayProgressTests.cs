using Dungeons.Items;
using Dungeons.Presentation;
using Dungeons.Professions;
using Xunit;
using static Dungeons.Tests.Professions.ProfessionsTestData;

namespace Dungeons.Tests.Professions;

/// <summary>
/// The return summary (Phase 10). The payout itself is <see cref="OfflineProgressTests"/>'
/// subject; what these hold is the thing the player actually experiences — coming back and being
/// told, in one readable place, what happened.
///
/// <para>The rule worth stating: <b>this layer aggregates, it never resolves.</b> Every number in
/// an <see cref="AwayReport"/> came out of <see cref="ProfessionSystem.Execute"/>, the same path
/// live passive play uses. There is no offline simulator to disagree with it.</para>
/// </summary>
public class AwayProgressTests
{
    private const string ChopOak = "action.chop_oak";
    private const string Forestry = "profession.forestry";

    private static ProfessionActionDefinition Chopping() => new()
    {
        Id = ChopOak,
        ProfessionId = Forestry,
        Name = "Chop Oak",
        BaseIntervalTicks = 100,
        Experience = 10,
        Outputs = new[] { Amount("material.oak_log") },
    };

    private static ProfessionActionDefinition Sawing() => new()
    {
        Id = "action.saw_oak_planks",
        ProfessionId = Forestry,
        Name = "Saw Planks",
        BaseIntervalTicks = 100,
        Experience = 15,
        Inputs = new[] { Amount("material.oak_log") },
        Outputs = new[] { Amount("material.oak_plank", 2) },
    };

    private static double SecondsFor(int completions) => completions * 100.0 / ProfessionTuning.TicksPerSecond;

    [Fact]
    public void AnAbsenceIsReportedAsCompletionsItemsAndExperience()
    {
        var inventory = new Inventory();
        var system = new ProfessionSystem(Store(Chopping()), inventory, new FakeRandom());

        var report = AwayProgress.Resolve(system, ChopOak, SecondsFor(12));

        Assert.True(report.EarnedAnything);
        Assert.Equal(12, report.PassiveWork!.CompletedActions);
        Assert.Equal(120, report.XpGained);
        Assert.Equal(12, report.MasteryGained);
        Assert.Equal(new ItemStack("material.oak_log", 12), Assert.Single(report.Produced));
    }

    /// <summary>Levelling up while away is the single most interesting thing that can happen in
    /// an absence, and it was the one thing the old three-line log never mentioned.</summary>
    [Fact]
    public void LevelsGainedWhileAwayAreReported()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());

        // 100 XP per level at level 1; 30 completions × 10 XP clears several.
        var report = AwayProgress.Resolve(system, ChopOak, SecondsFor(30));

        var gain = Assert.Single(report.LevelsGained);
        Assert.Equal(Forestry, gain.ProfessionId);
        Assert.Equal(1, gain.LevelBefore);
        Assert.True(gain.LevelAfter > 1);
        Assert.Equal(gain.LevelAfter - 1, gain.LevelsGained);
    }

    /// <summary>A profession that was never touched starts at level 1, not level 0 — reading it
    /// as 0 would report a phantom level-up the first time one is used.</summary>
    [Fact]
    public void AProfessionTouchedForTheFirstTimeIsNotReportedAsLevellingFromZero()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());

        var report = AwayProgress.Resolve(system, ChopOak, SecondsFor(1)); // 10 XP: not a level

        Assert.Empty(report.LevelsGained);
    }

    [Fact]
    public void NothingSelectedIsAnHonestEmptyReport()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());

        var report = AwayProgress.Resolve(system, selectedActionId: null, SecondsFor(50));

        Assert.False(report.EarnedAnything);
        Assert.Null(report.PassiveWork);
        Assert.Empty(report.Produced);
    }

    [Fact]
    public void RunningOutOfMaterialsIsReportedAsSuchRatherThanAsATinyAbsence()
    {
        var inventory = new Inventory();
        inventory.Add("material.oak_log", 3);
        var system = new ProfessionSystem(Store(Sawing()), inventory, new FakeRandom());

        var report = AwayProgress.Resolve(system, "action.saw_oak_planks", SecondsFor(50));

        Assert.Equal(3, report.PassiveWork!.CompletedActions);
        Assert.Equal(OfflineStopReason.InputsExhausted, report.StopReason);
    }

    /// <summary>An absence past the cap must say so. Silently paying twelve hours for a week is
    /// how a player concludes the game lost their progress.</summary>
    [Fact]
    public void AnAbsencePastTheCapIsReportedAsCapped()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());

        var report = AwayProgress.Resolve(system, ChopOak, elapsedRealSeconds: 7 * 24 * 3600);

        Assert.True(report.WasTimeCapped);
        Assert.Contains("as far as unattended work goes", AwayReadout.StopNote(report, offlineCapHours: 12));
    }

    // --- The readout ----------------------------------------------------------

    [Fact]
    public void TheSummarySpeaksInCompletionsAndItemsRatherThanTicks()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());
        var report = AwayProgress.Resolve(system, ChopOak, SecondsFor(12));

        var lines = AwayReadout.Lines(report, id => "Chop Oak", id => "Forestry", id => "Oak Log", offlineCapHours: 12);

        Assert.Contains(lines, line => line.Detail.Contains("finished 12 times"));
        Assert.Contains(lines, line => line.Detail.Contains("Oak Log ×12"));
        Assert.DoesNotContain(lines, line => line.Detail.Contains("tick"));
    }

    [Fact]
    public void AnEmptyAbsenceRendersNoLinesAtAll()
    {
        var system = new ProfessionSystem(Store(Chopping()), new Inventory(), new FakeRandom());
        var report = AwayProgress.Resolve(system, selectedActionId: null, SecondsFor(50));

        Assert.Empty(AwayReadout.Lines(report, id => id, id => id, id => id, offlineCapHours: 12));
        Assert.Contains("nothing was left running", AwayReadout.Headline(report));
    }

    [Theory]
    [InlineData(45, "45s")]
    [InlineData(600, "10m")]
    [InlineData(9000, "2.5h")]
    [InlineData(180_000, "2.1 days")]
    public void DurationsReadAsDurations(long seconds, string expected) =>
        Assert.Equal(expected, AwayReadout.Duration(seconds));
}
