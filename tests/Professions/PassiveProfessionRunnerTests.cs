using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Simulation;
using Xunit;
using static Dungeons.Tests.Professions.ProfessionsTestData;

namespace Dungeons.Tests.Professions;

public class PassiveProfessionRunnerTests
{
    private static ProfessionActionDefinition Chop => new()
    {
        Id = "action.chop_oak",
        ProfessionId = "profession.forestry",
        BaseIntervalTicks = 100,
        Experience = 5,
        Outputs = new[] { Amount("material.oak_log") },
    };

    private static ProfessionActionDefinition Smelt => new()
    {
        Id = "action.smelt_iron",
        ProfessionId = "profession.smithing",
        BaseIntervalTicks = 100,
        Experience = 5,
        Inputs = new[] { Amount("material.iron_ore", 2) },
        Outputs = new[] { Amount("material.iron_ingot") },
    };

    [Fact]
    public void PassiveGathering_ProducesEachInterval()
    {
        var tick = new TickEngine();
        var inv = new Inventory();
        var system = new ProfessionSystem(Store(Chop), inv, new FakeRandom());
        var runner = new PassiveProfessionRunner(tick, system);

        Assert.True(runner.Start("action.chop_oak"));
        Assert.True(runner.IsRunning);

        tick.Advance(99);
        Assert.Equal(0, inv.GetQuantity("material.oak_log")); // not yet

        tick.Advance(1); // reach tick 100
        Assert.Equal(1, inv.GetQuantity("material.oak_log"));

        tick.Advance(100); // second completion (~tick 200)
        Assert.Equal(2, inv.GetQuantity("material.oak_log"));
        Assert.True(runner.IsRunning);
    }

    [Fact]
    public void Stop_HaltsProduction()
    {
        var tick = new TickEngine();
        var inv = new Inventory();
        var system = new ProfessionSystem(Store(Chop), inv, new FakeRandom());
        var runner = new PassiveProfessionRunner(tick, system);

        runner.Start("action.chop_oak");
        runner.Stop();
        Assert.False(runner.IsRunning);

        tick.Advance(500);
        Assert.Equal(0, inv.GetQuantity("material.oak_log"));
    }

    [Fact]
    public void RunningOutOfInputs_Waits_AndKeepsTheSelection()
    {
        var tick = new TickEngine();
        var inv = new Inventory();
        var system = new ProfessionSystem(Store(Smelt), inv, new FakeRandom());
        var runner = new PassiveProfessionRunner(tick, system);

        inv.Add("material.iron_ore", 2); // enough for exactly one completion

        Assert.True(runner.Start("action.smelt_iron"));

        ActionOutcome? stalledWith = null;
        runner.Stalled += o => stalledWith = o;

        tick.Advance(100); // first completion consumes the ore
        Assert.Equal(1, inv.GetQuantity("material.iron_ingot"));
        Assert.True(runner.IsRunning);

        tick.Advance(100); // second attempt has no ore
        Assert.False(runner.IsRunning);
        Assert.True(runner.IsWaiting);
        Assert.Equal("action.smelt_iron", runner.SelectedActionId); // NOT forgotten
        Assert.NotNull(stalledWith);
        Assert.Equal(ActionFailure.MissingInputs, stalledWith!.Failure);
        Assert.Equal(1, inv.GetQuantity("material.iron_ingot")); // no extra produced
    }

    /// <summary>
    /// The auto-repeat rule (Phase 10). Idle progression is for leaving something running and
    /// coming back to it; before this, the first empty ore chest ended the session's training
    /// silently, and the player returned to a Hideout doing nothing.
    /// </summary>
    [Fact]
    public void AWaitingSelectionResumesByItselfWhenTheMaterialsComeBack()
    {
        var tick = new TickEngine();
        var inv = new Inventory();
        var system = new ProfessionSystem(Store(Smelt), inv, new FakeRandom());
        var runner = new PassiveProfessionRunner(tick, system);

        inv.Add("material.iron_ore", 2);
        runner.Start("action.smelt_iron");

        string? resumedWith = null;
        runner.Resumed += id => resumedWith = id;

        tick.Advance(200); // one completion, then the stall
        Assert.True(runner.IsWaiting);

        inv.Add("material.iron_ore", 2); // a crop lifted, a run extracted, an offline payout
        tick.Advance(ProfessionTuning.PassiveRetryIntervalTicks);

        Assert.True(runner.IsRunning);
        Assert.Equal("action.smelt_iron", resumedWith);

        tick.Advance(100);
        Assert.Equal(2, inv.GetQuantity("material.iron_ingot"));
    }

    /// <summary>
    /// Temporary problems wait; permanent ones refuse. Missing materials is a state the world
    /// can leave on its own, so selecting an action you cannot afford yet is a legal standing
    /// choice — the runner holds it and starts when it can.
    /// </summary>
    [Fact]
    public void SelectingAnActionWithNoMaterialsWaitsRatherThanRefusing()
    {
        var tick = new TickEngine();
        var inv = new Inventory();
        var system = new ProfessionSystem(Store(Smelt), inv, new FakeRandom());
        var runner = new PassiveProfessionRunner(tick, system);

        Assert.True(runner.Start("action.smelt_iron"));
        Assert.True(runner.IsWaiting);
        Assert.False(runner.IsRunning);

        inv.Add("material.iron_ore", 2);
        tick.Advance(ProfessionTuning.PassiveRetryIntervalTicks + 100);
        Assert.Equal(1, inv.GetQuantity("material.iron_ingot"));
    }

    /// <summary>No amount of waiting teaches a profession, so a level gate is still a refusal.</summary>
    [Fact]
    public void AnActionAboveTheProfessionLevelIsRefusedOutright()
    {
        var tick = new TickEngine();
        var inv = new Inventory();
        var locked = new ProfessionActionDefinition
        {
            Id = "action.forge_masterwork",
            ProfessionId = "profession.smithing",
            RequiredLevel = 50,
            BaseIntervalTicks = 100,
            Outputs = new[] { Amount("material.iron_ingot") },
        };
        var system = new ProfessionSystem(Store(locked), inv, new FakeRandom());
        var runner = new PassiveProfessionRunner(tick, system);

        Assert.False(runner.Start("action.forge_masterwork"));
        Assert.Null(runner.SelectedActionId);
        Assert.Equal(PassiveTrainingState.Idle, runner.State);
    }

    /// <summary>Stopping is the only thing that clears the standing selection.</summary>
    [Fact]
    public void StopClearsTheSelection()
    {
        var tick = new TickEngine();
        var inv = new Inventory();
        var system = new ProfessionSystem(Store(Smelt), inv, new FakeRandom());
        var runner = new PassiveProfessionRunner(tick, system);

        runner.Start("action.smelt_iron");
        Assert.Equal("action.smelt_iron", runner.SelectedActionId);

        runner.Stop();
        Assert.Null(runner.SelectedActionId);
        Assert.Equal(PassiveTrainingState.Idle, runner.State);

        inv.Add("material.iron_ore", 20);
        tick.Advance(1000);
        Assert.Equal(0, inv.GetQuantity("material.iron_ingot")); // and it never comes back on its own
    }
}
