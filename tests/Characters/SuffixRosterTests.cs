using Dungeons.Characters.Composition;
using Dungeons.Content;
using Dungeons.Events;
using Dungeons.Randomness;
using Dungeons.Rules;
using Xunit;

namespace Dungeons.Tests.Characters;

/// <summary>
/// The Suffix roster (docs/classes.md §6).
///
/// <para>The design claim under test: <b>the same Suffix fantasy is usable by every build</b>,
/// because each one carries an expression per channel. A Wizard must never look at the modifier
/// list and see mechanics obviously meant for a Fighter.</para>
/// </summary>
public class SuffixRosterTests
{
    private static DataStore<SuffixDefinition> Suffixes() =>
        TestPaths.LoadStore<SuffixDefinition>("suffixes");

    private static readonly string[] FullyExpressed =
    {
        "suffix.exploding_kneecaps", "suffix.improper_safety_procedures", "suffix.the_last_laugh",
        "suffix.questionable_ethics", "suffix.mandatory_overtime", "suffix.unlicensed_surgery",
        "suffix.the_emergency_exit", "suffix.personal_liability", "suffix.terminal_curiosity",
        "suffix.absolutely_no_refunds",
    };

    [Fact]
    public void TheFullFiftyAreInTheRoster()
    {
        Assert.Equal(50, Suffixes().Count);
    }

    [Fact]
    public void TenRepresentativeSuffixesAreFullyExpressed()
    {
        var suffixes = Suffixes();

        foreach (var id in FullyExpressed)
        {
            Assert.True(suffixes.Contains(id), $"missing {id}.");
            Assert.True(suffixes.GetById(id).IsFullyExpressed, $"{id} is not fully expressed.");
        }

        Assert.Equal(FullyExpressed.Length, suffixes.GetAll().Count(s => s.IsFullyExpressed));
    }

    // ---- The three-expression contract -----------------------------------------------------------

    /// <summary>
    /// The whole point. A partially-expressed Suffix is worse than an unexpressed one — it looks
    /// usable and then turns out to be meant for someone else's build.
    /// </summary>
    [Fact]
    public void AnySuffixWithExpressionsHasExactlyOnePerChannel()
    {
        foreach (var suffix in Suffixes().GetAll().Where(s => s.Expressions.Count > 0))
        {
            foreach (var channel in Enum.GetValues<ExpressionChannel>())
            {
                Assert.True(
                    suffix.Expressions.Count(e => e.Channel == channel) == 1,
                    $"{suffix.Id} does not have exactly one {channel} expression.");
            }
        }
    }

    /// <summary>Every expression must cost something. Without drawbacks, discovery collapses
    /// into a tier list (docs/classes.md §10.3, applied to suffixes).</summary>
    [Fact]
    public void EveryExpressionStatesADrawback()
    {
        foreach (var suffix in Suffixes().GetAll())
        foreach (var expression in suffix.Expressions)
            Assert.False(string.IsNullOrWhiteSpace(expression.Drawback),
                $"{suffix.Id} {expression.Channel} has no drawback.");
    }

    /// <summary>
    /// The three expressions must be mechanically distinct, not the same hook relabelled —
    /// otherwise the channel model is decoration.
    ///
    /// <para>Distinct in <i>trigger or payoff</i>, not necessarily both. Some fantasies have one
    /// inherent trigger: The Last Laugh fires on death whatever your build, and the channels
    /// differ in what death <i>does</i> — a final blow, a corpse that blocks, or everything you
    /// banked going off at once.</para>
    /// </summary>
    [Fact]
    public void TheThreeExpressionsAreMechanicallyDistinct()
    {
        foreach (var suffix in Suffixes().GetAll().Where(s => s.IsFullyExpressed))
        {
            var shapes = suffix.Expressions
                .Select(e => string.Join("|",
                    e.Rule.Event,
                    string.Join(",", e.Rule.When.Select(c => c.Kind + c.Text)),
                    e.Rule.Effect.Kind,
                    e.Rule.Effect.Text))
                .ToList();

            Assert.True(shapes.Distinct().Count() == 3,
                $"{suffix.Id} repeats an expression across channels: {string.Join(" / ", shapes)}");
        }
    }

    /// <summary>Surge expressions must actually key off resource activity, or the channel is a
    /// dumping ground for whatever didn't fit the other two.</summary>
    [Fact]
    public void SurgeExpressionsKeyOffResourceOrGaugeActivity()
    {
        var resourceish = new[] { GameEvents.ResourceSpent, GameEvents.ResourceGenerated, GameEvents.Defeated, GameEvents.Killed };

        foreach (var suffix in Suffixes().GetAll().Where(s => s.IsFullyExpressed))
        {
            var surge = suffix.For(ExpressionChannel.Surge)!;
            var keysOffResource = resourceish.Contains(surge.Rule.Event)
                || surge.Rule.When.Any(c => c.Kind == RuleVocabulary.GaugeAtLeast);

            Assert.True(keysOffResource, $"{suffix.Id} Surge expression hooks '{surge.Rule.Event}' with no resource condition.");
        }
    }

    // ---- Presentation stays separate from mechanics ---------------------------------------------------

    /// <summary>Format is presentation. If it ever leaked into behaviour, changing how a name
    /// reads would change how the character plays.</summary>
    [Fact]
    public void EverySuffixDeclaresAKnownFormatAndAFantasy()
    {
        foreach (var suffix in Suffixes().GetAll())
        {
            Assert.Contains(suffix.Format, ContentValidator.NameFormats);
            Assert.False(string.IsNullOrWhiteSpace(suffix.Fantasy), $"{suffix.Id} has no fantasy.");
        }
    }

    /// <summary>All nine styles should be in use, or the dynamic naming system is decorative.</summary>
    [Fact]
    public void AllNineNameFormatsAreUsed()
    {
        var used = Suffixes().GetAll().Select(s => s.Format).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var format in ContentValidator.NameFormats)
            Assert.Contains(format, used);
    }

    [Fact]
    public void OnlyStandoutPhrasingsOverrideTheirTemplate()
    {
        var overridden = Suffixes().GetAll().Where(s => !string.IsNullOrEmpty(s.CustomPhrase)).ToList();

        Assert.All(overridden, s => Assert.Equal("medical", s.Format));
        Assert.True(overridden.Count <= 5, "custom phrases are for the few too good to generalise.");
    }

    // ---- They actually fire ------------------------------------------------------------------------------

    /// <summary>
    /// Executed rather than asserted: the same Suffix fires for a heavy hitter, a blocker and a
    /// resource-dumper, through three different events.
    /// </summary>
    [Theory]
    [InlineData(ExpressionChannel.Strike, GameEvents.DamageDealt, "heavy")]
    [InlineData(ExpressionChannel.Guard, GameEvents.Blocked, "")]
    [InlineData(ExpressionChannel.Surge, GameEvents.ResourceSpent, "")]
    public void ExplodingKneecapsFiresForEveryKindOfBuild(
        ExpressionChannel channel, string eventKind, string tag)
    {
        var expression = Suffixes().GetById("suffix.exploding_kneecaps").For(channel)!;

        var bus = new GameEventBus();
        using var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => 0);
        engine.Attach(expression.Rule, "suffix.exploding_kneecaps");

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(tag))
            tags.Add(tag);

        bus.Publish(new GameEvent(eventKind, Source: "player", Amount: 40, Tags: tags));

        var fired = Assert.Single(engine.Fired);
        Assert.Equal(RuleVocabulary.AreaDamage, fired.Kind);
        Assert.True(fired.Magnitude > 0);
    }

    /// <summary>Suffixes are explicitly allowed outside combat — that is the DCC weirdness
    /// working. If they all lived in the damage pipeline the layer would be wasted.</summary>
    [Fact]
    public void ExpressedSuffixesReachSystemsBeyondCombat()
    {
        var effects = Suffixes().GetAll()
            .SelectMany(s => s.Expressions)
            .Select(e => e.Rule.Effect.Kind)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(RuleVocabulary.GrantItem, effects);    // harvesting
        Assert.Contains(RuleVocabulary.RevealInfo, effects);   // discovery
        Assert.Contains(RuleVocabulary.Reposition, effects);   // extraction/escape
        Assert.Contains(RuleVocabulary.SpawnEntity, effects);  // hazards
    }

    /// <summary>The deliberate anti-synergy: Absolutely No Refunds makes actions uncancellable,
    /// and The Trickster's entire mechanic is cancelling. Keeping it visible is the point.</summary>
    [Fact]
    public void NoRefundsAndTricksterGenuinelyConflict()
    {
        var refunds = Suffixes().GetById("suffix.absolutely_no_refunds").For(ExpressionChannel.Strike)!;
        var trickster = TestPaths.LoadStore<PrefixDefinition>("prefixes").GetById("prefix.trickster");

        Assert.Equal("combat.uncancellable", refunds.Rule.Effect.Text);
        Assert.Contains(trickster.Rules, r => r.Event == GameEvents.ActionTelegraphed);
    }
}
