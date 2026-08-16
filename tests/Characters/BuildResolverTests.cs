using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Content;
using Dungeons.Events;
using Dungeons.Randomness;
using Dungeons.Rules;
using Xunit;

namespace Dungeons.Tests.Characters;

/// <summary>
/// Base + Prefix + Suffix composition (docs/classes.md §1).
///
/// <para>The claim: <b>17,500 builds exist and none of them were authored.</b> Every one is
/// derived — growth from the Base, a mechanic from the Prefix, and the one Suffix expression
/// matching the build's channel. These tests exercise that across the real roster rather than
/// on a fixture.</para>
/// </summary>
public class BuildResolverTests
{
    private static ContentBundle Content() => new()
    {
        Classes = TestPaths.LoadStore<BaseClassDefinition>("classes"),
        Prefixes = TestPaths.LoadStore<PrefixDefinition>("prefixes"),
        Suffixes = TestPaths.LoadStore<SuffixDefinition>("suffixes"),
        NameFormats = TestPaths.LoadStore<NameFormatDefinition>("name_formats"),
        ModifierKeys = TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
    };

    private static CharacterBuild Build(string @base, string prefix, string suffix) =>
        new(new SpeciesId("species.human"), new BaseClassId(@base), new PrefixId(prefix), new SuffixId(suffix));

    private static ResolvedBuild Resolve(string @base, string prefix, string suffix) =>
        new BuildResolver(Content()).Resolve(Build(@base, prefix, suffix));

    // ---- It composes ----------------------------------------------------------------------------

    [Fact]
    public void ABuildDrawsGrowthFromTheBaseAndMechanicsFromThePrefixAndSuffix()
    {
        var build = Resolve("class.juggernaut", "prefix.galvanic", "suffix.exploding_kneecaps");

        Assert.Equal("The Galvanic Juggernaut of Exploding Kneecaps", build.Name);
        Assert.Equal(1.5, build.GrowthPerLevel[AttributeType.Strength], 3);

        // Two gauges: the Base's Momentum and the Prefix's Charge.
        Assert.Equal(2, build.Gauges.Count);
        Assert.Contains(build.Gauges, g => g.Name == "Momentum");
        Assert.Contains(build.Gauges, g => g.Name == "Charge");

        Assert.Contains(build.Rules, r => r.Origin == "The Galvanic");
        Assert.Contains(build.Rules, r => r.Origin.StartsWith("Exploding Kneecaps"));
    }

    /// <summary>
    /// The mechanism that makes a Suffix usable by every Base: exactly one expression applies,
    /// chosen by the Base's channel. Same Suffix, three Bases, three different hooks.
    /// </summary>
    [Theory]
    [InlineData("class.juggernaut", ExpressionChannel.Strike, GameEvents.DamageDealt)]
    [InlineData("class.bastion", ExpressionChannel.Guard, GameEvents.Blocked)]
    [InlineData("class.warlock", ExpressionChannel.Surge, GameEvents.ResourceSpent)]
    public void TheSuffixExpressionFollowsTheBasesChannel(
        string baseId, ExpressionChannel expected, string expectedEvent)
    {
        var build = Resolve(baseId, "prefix.galvanic", "suffix.exploding_kneecaps");

        Assert.Equal(expected, build.Channel);

        var fromSuffix = Assert.Single(build.Rules, r => r.Source == "suffix.exploding_kneecaps");
        Assert.Equal(expectedEvent, fromSuffix.Rule.Event);
    }

    [Fact]
    public void ExactlyOneSuffixExpressionIsEverAttached()
    {
        foreach (var @base in Content().Classes.GetAll())
        {
            var build = Resolve(@base.Id, "prefix.galvanic", "suffix.mandatory_overtime");
            Assert.Single(build.Rules, r => r.Source == "suffix.mandatory_overtime");
        }
    }

    /// <summary>A roster-only Suffix contributes no hooks and must not break composition —
    /// forty of the fifty are in that state today.</summary>
    [Fact]
    public void AnUnexpressedSuffixComposesCleanlyAndContributesNothing()
    {
        var build = Resolve("class.wizard", "prefix.chrono", "suffix.the_fine_print");

        Assert.Equal("The Chrono Wizard (The Fine Print Accepted)", build.Name);
        Assert.DoesNotContain(build.Rules, r => r.Source == "suffix.the_fine_print");
    }

    // ---- Nothing is authored per-combination ------------------------------------------------------

    /// <summary>The whole roster, resolved. If any pairing needed special-casing this is where
    /// it would surface.</summary>
    [Fact]
    public void EveryBasePrefixSuffixCombinationResolves()
    {
        var content = Content();
        var resolver = new BuildResolver(content);
        var resolved = 0;

        foreach (var @base in content.Classes.GetAll())
        foreach (var prefix in content.Prefixes.GetAll())
        foreach (var suffix in content.Suffixes.GetAll())
        {
            var build = resolver.Resolve(Build(@base.Id, prefix.Id, suffix.Id));

            Assert.False(string.IsNullOrWhiteSpace(build.Name));
            Assert.Equal(AttributeGrowth.BudgetPerLevel, build.GrowthPerLevel.Values.Sum(), 6);
            Assert.InRange(build.Gauges.Count, 0, 2);
            resolved++;
        }

        Assert.Equal(15 * 25 * 50, resolved);
    }

    /// <summary>Two meters maximum. Three would stop being readable, which is the failure the
    /// single-Base structure exists to avoid.</summary>
    [Fact]
    public void NoBuildEverCarriesMoreThanTwoGauges()
    {
        var content = Content();
        var resolver = new BuildResolver(content);

        foreach (var @base in content.Classes.GetAll())
        foreach (var prefix in content.Prefixes.GetAll())
        {
            var build = resolver.Resolve(Build(@base.Id, prefix.Id, "suffix.the_last_laugh"));
            Assert.True(build.Gauges.Count <= 2, $"{@base.Id} + {prefix.Id} has {build.Gauges.Count} gauges.");
        }
    }

    // ---- The hooks actually fire together ------------------------------------------------------------

    /// <summary>
    /// End to end: a composed build's Base gauge, Prefix mechanic and Suffix expression all
    /// attach to one bus and respond to one event, with nobody having authored the combination.
    /// </summary>
    [Fact]
    public void AComposedBuildsHooksAllFireOnTheSameEvent()
    {
        var build = Resolve("class.juggernaut", "prefix.galvanic", "suffix.exploding_kneecaps");

        var bus = new GameEventBus();
        using var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => 0);
        foreach (var attached in build.Rules)
            engine.Attach(attached.Rule, attached.Source);

        bus.Publish(new GameEvent(
            GameEvents.DamageDealt, Source: "player", Amount: 50,
            Tags: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "heavy" },
            Values: new Dictionary<string, double> { ["gauge_fraction"] = 0.9 }));

        // Momentum feeds, Galvanic discharges, Exploding Kneecaps detonates — three sources.
        Assert.True(engine.Fired.Count >= 3, $"only {engine.Fired.Count} hooks fired.");
        Assert.Contains(engine.Fired, f => f.Source == "gauge");
        Assert.Contains(engine.Fired, f => f.Source == "prefix.galvanic");
        Assert.Contains(engine.Fired, f => f.Source == "suffix.exploding_kneecaps");
    }

    // ---- The diff ---------------------------------------------------------------------------------------

    /// <summary>"Swap any one component and immediately understand what changed" — the Character
    /// Lab's whole purpose, tested as a function rather than eyeballed in the UI.</summary>
    [Fact]
    public void SwappingTheBaseReportsGrowthChannelAndGaugeChanges()
    {
        var before = Resolve("class.juggernaut", "prefix.galvanic", "suffix.exploding_kneecaps");
        var after = Resolve("class.bastion", "prefix.galvanic", "suffix.exploding_kneecaps");

        var diff = BuildResolver.Diff(before, after);
        var text = string.Join("\n", diff);

        Assert.Contains("Name:", text);
        Assert.Contains("Channel: Strike → Guard", text);
        Assert.Contains("Lost gauge: Momentum", text);
        Assert.Contains("Gained gauge: Guard", text);
        Assert.Contains("Strength growth:", text);
    }

    [Fact]
    public void SwappingThePrefixReportsOnlyTheMechanicChange()
    {
        var before = Resolve("class.wizard", "prefix.galvanic", "suffix.the_last_laugh");
        var after = Resolve("class.wizard", "prefix.seismic", "suffix.the_last_laugh");

        var text = string.Join("\n", BuildResolver.Diff(before, after));

        Assert.Contains("Lost gauge: Charge", text);
        Assert.DoesNotContain("growth:", text);   // the Base did not change
        Assert.DoesNotContain("Channel:", text);
    }

    [Fact]
    public void AnIdenticalBuildReportsNoChange()
    {
        var build = Resolve("class.bard", "prefix.quantum", "suffix.terminal_curiosity");
        Assert.Equal(new[] { "(no mechanical change)" }, BuildResolver.Diff(build, build));
    }
}
