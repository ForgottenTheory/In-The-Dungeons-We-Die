using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Rules;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// Validation for auto-combat brains — one failing-content test per rule.
///
/// <para>The rule these are really here for is the reaction floor. D-07 says automation is
/// disadvantaged by <em>reaction latency</em> and never by a damage penalty, which only holds
/// while the latency is genuinely longer than the tight windows. A profile authored with a
/// 2-tick reaction would parry, and the "active play earns its advantage by being present"
/// argument would quietly stop being true — with nothing anywhere reporting it. So it is a load
/// error, not a comment.</para>
/// </summary>
public class AutoCombatContentTests
{
    private static DataStore<MoveDefinition> Moves()
    {
        var store = new DataStore<MoveDefinition>();
        store.Add(new MoveDefinition
        {
            Id = "move.strike",
            Name = "Strike",
            Kind = MoveKind.Attack,
            Tags = new[] { "action:attack", "delivery:melee" },
        });
        return store;
    }

    private static AutoCombatProfileDefinition Brain(
        int reactionTicks = AutoCombatTuning.DefaultReactionTicks,
        double avoidRepeatWeight = 0.5,
        string description = "For tests.",
        IReadOnlyList<AiRuleSpec>? rules = null,
        IReadOnlyList<DefenceRuleSpec>? defence = null) => new()
    {
        Id = "auto.test",
        Name = "Test Brain",
        Description = description,
        ReactionTicks = reactionTicks,
        AvoidRepeatWeight = avoidRepeatWeight,
        Rules = rules ?? new[] { new AiRuleSpec { MoveTag = "action:attack", Weight = 1 } },
        Defence = defence ?? new[] { new DefenceRuleSpec { Stance = DefensiveStance.Block, Weight = 1 } },
    };

    private static IReadOnlyList<ContentProblem> ProblemsFor(params AutoCombatProfileDefinition[] profiles)
    {
        var store = new DataStore<AutoCombatProfileDefinition>();
        foreach (var profile in profiles)
            store.Add(profile);

        return ContentValidator.Validate(new ContentBundle { AutoCombatProfiles = store, Moves = Moves() })
            .Where(problem => problem.Category == "auto_combat")
            .ToList();
    }

    [Fact]
    public void ACoherentBrainIsAccepted() => Assert.Empty(ProblemsFor(Brain()));

    /// <summary>A brain that never defends is a real playstyle, not an authoring mistake.</summary>
    [Fact]
    public void ABrainWithNoDefenceRulesIsAccepted() =>
        Assert.Empty(ProblemsFor(Brain(defence: Array.Empty<DefenceRuleSpec>())));

    /// <summary>The D-07 fence.</summary>
    [Fact]
    public void ABrainQuickEnoughToParryIsRejected() =>
        Assert.Contains(
            ProblemsFor(Brain(reactionTicks: CombatTuning.ParryWindowTicks)),
            problem => problem.Message.Contains("D-07"));

    [Fact]
    public void ABrainQuickEnoughToPerfectBlockIsRejected() =>
        Assert.Contains(
            ProblemsFor(Brain(reactionTicks: CombatTuning.PerfectBlockWindowTicks)),
            problem => problem.Message.Contains("D-07"));

    [Fact]
    public void ABrainWithNoOffensiveRulesIsRejected() =>
        Assert.Contains(
            ProblemsFor(Brain(rules: Array.Empty<AiRuleSpec>())),
            problem => problem.Message.Contains("no offensive rules"));

    [Fact]
    public void ARuleNamingAnUnknownMoveIsRejected() =>
        Assert.Contains(
            ProblemsFor(Brain(rules: new[] { new AiRuleSpec { Move = "move.imaginary", Weight = 1 } })),
            problem => problem.Message.Contains("unknown move"));

    [Fact]
    public void ARuleSettingBothMoveAndTagIsRejected() =>
        Assert.Contains(
            ProblemsFor(Brain(rules: new[] { new AiRuleSpec { Move = "move.strike", MoveTag = "action:attack", Weight = 1 } })),
            problem => problem.Message.Contains("exactly one"));

    [Fact]
    public void ADefenceRuleWithAnUnknownConditionIsRejected() =>
        Assert.Contains(
            ProblemsFor(Brain(defence: new[]
            {
                new DefenceRuleSpec
                {
                    Stance = DefensiveStance.Block,
                    Weight = 1,
                    When = new[] { new ConditionSpec { Kind = "whenItFeelsRight" } },
                },
            })),
            problem => problem.Message.Contains("unknown condition"));

    [Fact]
    public void ADefenceRuleWithNoWeightIsRejected() =>
        Assert.Contains(
            ProblemsFor(Brain(defence: new[] { new DefenceRuleSpec { Stance = DefensiveStance.Dodge, Weight = 0 } })),
            problem => problem.Message.Contains("non-positive weight"));

    [Fact]
    public void AnAvoidRepeatWeightOutsideItsRangeIsRejected() =>
        Assert.Contains(ProblemsFor(Brain(avoidRepeatWeight: 1.5)), problem => problem.Message.Contains("outside 0–1"));

    [Fact]
    public void ABrainWithNoDescriptionIsRejected() =>
        Assert.Contains(ProblemsFor(Brain(description: "")), problem => problem.Message.Contains("no description"));

    [Fact]
    public void AnEmptyStoreIsLeftAlone() => Assert.Empty(ProblemsFor());
}
