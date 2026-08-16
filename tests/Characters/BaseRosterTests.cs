using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Content;
using Xunit;

namespace Dungeons.Tests.Characters;

/// <summary>
/// The Base roster (docs/classes.md §3).
///
/// <para>Two properties carry the design and are tested hard: <b>every Base gains the same
/// total growth</b> (so Base choice is a trade, not a menu with strictly-larger options), and
/// <b>every Base has a distinct engine</b> (so the roster isn't fifteen flavours of three
/// ideas). The rest is content coherence.</para>
/// </summary>
public class BaseRosterTests
{
    private static DataStore<BaseClassDefinition> Bases() =>
        TestPaths.LoadStore<BaseClassDefinition>("classes");

    [Fact]
    public void AllFifteenBasesShip()
    {
        var bases = Bases();

        foreach (var id in new[]
        {
            "class.fighter", "class.juggernaut", "class.operative", "class.outlander",
            "class.kineticist", "class.vitalist", "class.wizard", "class.invoker",
            "class.druid", "class.bastion", "class.bard", "class.necromancer",
            "class.artificer", "class.warlock", "class.vanguard",
        })
        {
            Assert.True(bases.Contains(id), $"missing Base '{id}'.");
        }

        Assert.Equal(15, bases.Count);
    }

    // ---- The growth budget -------------------------------------------------------------------

    /// <summary>
    /// The load-bearing rule. No Base may be simply "more" than another — three attributes
    /// pushed hard has to mean four left behind.
    /// </summary>
    [Fact]
    public void EveryBaseDistributesExactlyTheSameGrowthBudget()
    {
        foreach (var @base in Bases().GetAll())
        {
            var total = AttributeGrowth.PerLevel(@base.Growth).Values.Sum();
            Assert.Equal(AttributeGrowth.BudgetPerLevel, total, 6);
        }
    }

    [Fact]
    public void UnlistedAttributesReceiveTheLeftoverBudgetEvenly()
    {
        var growth = AttributeGrowth.PerLevel(new Dictionary<string, double>
        {
            ["Strength"] = 1.5, ["Constitution"] = 1.2, ["Endurance"] = 0.8,
        });

        Assert.Equal(1.5, growth[AttributeType.Strength], 6);
        Assert.Equal(0.125, growth[AttributeType.Dexterity], 6);   // (4.0 − 3.5) ÷ 4 unlisted
        Assert.Equal(0.125, growth[AttributeType.Luck], 6);
    }

    /// <summary>Fractional weights have to accumulate rather than round away, or a 0.8 secondary
    /// would never grow at all.</summary>
    [Fact]
    public void FractionalGrowthAccumulatesAcrossLevels()
    {
        var weights = new Dictionary<string, double> { ["Strength"] = 1.5, ["Dexterity"] = 1.2, ["Endurance"] = 0.8 };

        Assert.Equal(0, AttributeGrowth.AtLevel(weights, 1)[AttributeType.Strength]);
        Assert.Equal(1, AttributeGrowth.AtLevel(weights, 2)[AttributeType.Strength]);   // 1.5 × 1
        Assert.Equal(15, AttributeGrowth.AtLevel(weights, 11)[AttributeType.Strength]); // 1.5 × 10
        Assert.Equal(8, AttributeGrowth.AtLevel(weights, 11)[AttributeType.Endurance]); // 0.8 × 10
        Assert.Equal(1, AttributeGrowth.AtLevel(weights, 11)[AttributeType.Luck]);      // 0.125 × 10
    }

    /// <summary>Two Bases growing differently must actually diverge, or the budget rule has
    /// produced sameness rather than trade-offs.</summary>
    [Fact]
    public void DifferentBasesDivergeMeaningfullyByLevelTwentyOne()
    {
        var bases = Bases();
        var juggernaut = AttributeGrowth.AtLevel(bases.GetById("class.juggernaut").Growth, 21);
        var wizard = AttributeGrowth.AtLevel(bases.GetById("class.wizard").Growth, 21);

        Assert.True(juggernaut[AttributeType.Strength] > wizard[AttributeType.Strength] * 3);
        Assert.True(wizard[AttributeType.Intelligence] > juggernaut[AttributeType.Intelligence] * 3);
        Assert.Equal(juggernaut.Values.Sum(), wizard.Values.Sum());
    }

    // ---- Distinctness -------------------------------------------------------------------------

    /// <summary>No two Bases may share a growth shape — identical spreads would be the same
    /// chassis wearing two names.</summary>
    [Fact]
    public void NoTwoBasesShareAGrowthShape()
    {
        var shapes = Bases().GetAll().ToDictionary(
            b => b.Id,
            b => string.Join(",", AttributeGrowth.PerLevel(b.Growth)
                .OrderBy(p => p.Key)
                .Select(p => $"{p.Key}:{p.Value:0.###}")));

        var duplicates = shapes.GroupBy(s => s.Value).Where(g => g.Count() > 1).ToList();

        Assert.True(duplicates.Count == 0,
            "identical growth shapes: " + string.Join(" | ", duplicates.Select(g => string.Join(" == ", g.Select(s => s.Key)))));
    }

    [Fact]
    public void EveryBaseStatesItsEngineAndItsCost()
    {
        foreach (var @base in Bases().GetAll())
        {
            Assert.False(string.IsNullOrWhiteSpace(@base.Engine), $"{@base.Id} has no engine.");
            Assert.False(string.IsNullOrWhiteSpace(@base.Weakness), $"{@base.Id} has no stated weakness.");
        }
    }

    // ---- Gauges ----------------------------------------------------------------------------------

    /// <summary>
    /// "Some Bases may have gauges, others may not" — giving everyone a bar would flatten the
    /// distinctions the roster exists to create. This asserts the mix is real in both directions.
    /// </summary>
    [Fact]
    public void SomeBasesRunWithoutAGaugeAndSomeWithOne()
    {
        var bases = Bases().GetAll().ToList();

        Assert.True(bases.Count(b => b.Gauge is null) >= 5, "too few gaugeless Bases — the roster is homogenising.");
        Assert.True(bases.Count(b => b.Gauge is not null) >= 5, "too few gauged Bases.");

        // The four called out in design as deliberately gaugeless.
        foreach (var id in new[] { "class.fighter", "class.vitalist", "class.necromancer", "class.bard" })
            Assert.Null(bases.Single(b => b.Id == id).Gauge);
    }

    [Fact]
    public void GaugeBehavioursSpanTheTaxonomy()
    {
        var behaviours = Bases().GetAll()
            .Where(b => b.Gauge is not null)
            .Select(b => b.Gauge!.Behaviour)
            .ToHashSet();

        Assert.True(behaviours.Count >= 4, $"only {behaviours.Count} gauge shapes in use — they are converging.");
    }

    /// <summary>Gauge feeds are ordinary trigger rules, so a gauge needs no bespoke plumbing —
    /// it reads the same event bus everything else does.</summary>
    [Fact]
    public void GaugeFeedsAreOrdinaryEventHooks()
    {
        var momentum = Bases().GetById("class.juggernaut").Gauge!;

        Assert.Equal("Momentum", momentum.Name);
        Assert.Contains(momentum.Feeds, f => f.Event == Dungeons.Events.GameEvents.DamageDealt);
        Assert.Contains(momentum.Feeds, f => f.Event == Dungeons.Events.GameEvents.DamageTaken);
        Assert.Contains(momentum.Bands, b => b.Modifier == "combat.interrupt.immune");
    }

    // ---- Channels ------------------------------------------------------------------------------------

    /// <summary>All three expression channels must be represented, or a third of the Suffix
    /// design would have no Base defaulting to it.</summary>
    [Fact]
    public void EveryExpressionChannelIsSomeBasesDefault()
    {
        var channels = Bases().GetAll().Select(b => b.DefaultChannel).ToHashSet();

        foreach (var channel in Enum.GetValues<ExpressionChannel>())
            Assert.Contains(channel, channels);
    }

    [Fact]
    public void ChannelDefaultsMatchTheStatedEngines()
    {
        var bases = Bases();

        Assert.Equal(ExpressionChannel.Guard, bases.GetById("class.bastion").DefaultChannel);
        Assert.Equal(ExpressionChannel.Strike, bases.GetById("class.juggernaut").DefaultChannel);
        Assert.Equal(ExpressionChannel.Surge, bases.GetById("class.warlock").DefaultChannel);
    }
}
