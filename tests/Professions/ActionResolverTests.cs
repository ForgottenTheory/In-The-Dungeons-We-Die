using Dungeons.Professions;
using Xunit;
using static Dungeons.Tests.Professions.ProfessionsTestData;

namespace Dungeons.Tests.Professions;

public class ActionResolverTests
{
    private static ProfessionActionDefinition ChopOak() => new()
    {
        Id = "action.chop_oak",
        ProfessionId = "profession.forestry",
        BaseIntervalTicks = 100,
        Experience = 10,
        Outputs = new[] { Amount("material.oak_log") },
        BonusOutputs = new[] { Chance("material.oak_bark", 0.2) },
    };

    [Fact]
    public void AlwaysProducesGuaranteedOutputs()
    {
        var yield = ActionResolver.Resolve(ChopOak(), mastery: 0, performance: 0, new FakeRandom(@default: 0.99));
        Assert.Contains(yield.Produced, s => s.ItemId == "material.oak_log");
        Assert.DoesNotContain(yield.Produced, s => s.ItemId == "material.oak_bark"); // 0.99 > 0.2
        Assert.Equal(10, yield.Xp);
    }

    [Fact]
    public void BonusOutput_RollsAgainstChance()
    {
        var yield = ActionResolver.Resolve(ChopOak(), mastery: 0, performance: 0, new FakeRandom(@default: 0.1));
        Assert.Contains(yield.Produced, s => s.ItemId == "material.oak_bark"); // 0.1 < 0.2
    }

    [Fact]
    public void ActivePerformance_BoostsXpAndBonusChance()
    {
        // base bonus chance 0.2 + active 0.3 at full performance = 0.5; roll 0.4 succeeds.
        var yield = ActionResolver.Resolve(ChopOak(), mastery: 0, performance: 1.0, new FakeRandom(@default: 0.4));
        Assert.Contains(yield.Produced, s => s.ItemId == "material.oak_bark");
        Assert.Equal(15, yield.Xp); // 10 * (1 + 0.5)
    }

    [Fact]
    public void Mastery_BoostsBonusChance()
    {
        // base 0.2 + mastery(40 * 0.0025 = 0.1) = 0.3; roll 0.25 succeeds where passive base 0.2 would fail.
        var yield = ActionResolver.Resolve(ChopOak(), mastery: 40, performance: 0, new FakeRandom(@default: 0.25));
        Assert.Contains(yield.Produced, s => s.ItemId == "material.oak_bark");
    }
}
