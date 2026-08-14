using Dungeons.Items;
using Dungeons.Professions;
using Xunit;
using static Dungeons.Tests.Professions.ProfessionsTestData;

namespace Dungeons.Tests.Professions;

public class ProfessionSystemTests
{
    private static ProfessionActionDefinition Chop => new()
    {
        Id = "action.chop_oak",
        ProfessionId = "profession.forestry",
        BaseIntervalTicks = 100,
        Experience = 10,
        Outputs = new[] { Amount("material.oak_log") },
    };

    private static ProfessionActionDefinition Smelt => new()
    {
        Id = "action.smelt_iron",
        ProfessionId = "profession.smithing",
        BaseIntervalTicks = 120,
        Experience = 15,
        Inputs = new[] { Amount("material.iron_ore", 2) },
        Outputs = new[] { Amount("material.iron_ingot") },
    };

    private static (ProfessionSystem system, Inventory inv) Build(params ProfessionActionDefinition[] actions)
    {
        var inv = new Inventory();
        var system = new ProfessionSystem(Store(actions), inv, new FakeRandom());
        return (system, inv);
    }

    [Fact]
    public void Execute_ProducesOutputsAndGrantsXpAndMastery()
    {
        var (system, inv) = Build(Chop);

        var outcome = system.Execute("action.chop_oak");

        Assert.True(outcome.Success);
        Assert.Equal(1, inv.GetQuantity("material.oak_log"));
        Assert.Equal(10, outcome.XpGained);
        Assert.Equal(10, system.GetProgress("profession.forestry").Xp);
        Assert.Equal(1, system.GetProgress("profession.forestry").GetMastery("action.chop_oak"));
    }

    [Fact]
    public void Execute_ConsumesInputs()
    {
        var (system, inv) = Build(Smelt);
        inv.Add("material.iron_ore", 2);

        var outcome = system.Execute("action.smelt_iron");

        Assert.True(outcome.Success);
        Assert.Equal(0, inv.GetQuantity("material.iron_ore"));
        Assert.Equal(1, inv.GetQuantity("material.iron_ingot"));
    }

    [Fact]
    public void Execute_MissingInputs_FailsAndChangesNothing()
    {
        var (system, inv) = Build(Smelt);
        inv.Add("material.iron_ore", 1); // need 2

        var outcome = system.Execute("action.smelt_iron");

        Assert.False(outcome.Success);
        Assert.Equal(ActionFailure.MissingInputs, outcome.Failure);
        Assert.Equal(1, inv.GetQuantity("material.iron_ore")); // untouched
        Assert.Equal(0, inv.GetQuantity("material.iron_ingot"));
    }

    [Fact]
    public void Execute_LevelTooLow_Fails()
    {
        var gated = new ProfessionActionDefinition
        {
            Id = "action.gated",
            ProfessionId = "profession.forestry",
            RequiredLevel = 5,
            Experience = 1,
            Outputs = new[] { Amount("x") },
        };
        var (system, _) = Build(gated);

        Assert.Equal(ActionFailure.LevelTooLow, system.Execute("action.gated").Failure);
    }

    [Fact]
    public void Execute_UnknownAction_Fails()
    {
        var (system, _) = Build(Chop);
        Assert.Equal(ActionFailure.UnknownAction, system.Execute("action.nope").Failure);
    }

    [Fact]
    public void Execute_RaisesLevelUpWhenCrossingThreshold()
    {
        var big = new ProfessionActionDefinition
        {
            Id = "action.big",
            ProfessionId = "profession.forestry",
            Experience = 150, // crosses level 2 (needs 100)
            Outputs = new[] { Amount("x") },
        };
        var (system, _) = Build(big);

        ProfessionLevelUp? captured = null;
        system.LeveledUp += up => captured = up;

        system.Execute("action.big");

        Assert.NotNull(captured);
        Assert.Equal(1, captured!.Value.OldLevel);
        Assert.Equal(2, captured.Value.NewLevel);
    }

    [Fact]
    public void EffectiveInterval_ReflectsMastery()
    {
        var (system, _) = Build(Chop);
        Assert.Equal(100, system.EffectiveIntervalTicks("action.chop_oak"));

        system.GetProgress("profession.forestry").AddMastery("action.chop_oak", 20);
        Assert.Equal(90, system.EffectiveIntervalTicks("action.chop_oak"));
    }
}
