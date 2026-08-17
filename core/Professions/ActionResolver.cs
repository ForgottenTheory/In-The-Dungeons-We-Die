using Dungeons.Items;
using Dungeons.Randomness;

namespace Dungeons.Professions;

/// <summary>Items produced and XP earned by resolving one action attempt.</summary>
public sealed class ResolvedYield
{
    public required IReadOnlyList<ItemStack> Produced { get; init; }
    public required long Xp { get; init; }

    /// <summary>False when the attempt ran but its success roll missed (Hunting, Thieving).</summary>
    public bool Landed { get; init; } = true;

    /// <summary>An opportunity this attempt noticed, awaiting the player's pursue/ignore
    /// decision. Always null for passive attempts.</summary>
    public ProfessionOpportunityDefinition? Discovered { get; init; }
}

/// <summary>
/// Pure resolution of a single action attempt: rolls the success chance, computes
/// guaranteed outputs, rolls bonus outputs, scales XP, and — on the active path only —
/// rolls for an opportunity to offer. Higher mastery and higher active performance both
/// improve bonus-output chance; performance also boosts XP. This is the shared core
/// of passive (performance = 0, <paramref name="isActive"/> false) and active execution,
/// so the two can never drift into separate balance models (docs/architecture.md §20,
/// docs/professions.md §4).
/// </summary>
public static class ActionResolver
{
    public static ResolvedYield Resolve(
        ProfessionActionDefinition action,
        int mastery,
        double performance,
        IRandomSource random,
        bool isActive = false)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(random);
        performance = Math.Clamp(performance, 0.0, 1.0);

        // Rolled before anything else so the RNG stream does not shift depending on whether
        // the attempt landed — seeded tests would otherwise be impossible to reason about.
        var landed = action.SuccessChance >= 1.0 || random.NextDouble() < action.SuccessChance;

        var produced = new List<ItemStack>();
        var masteryBonus = ProfessionTuning.MasteryBonusChance(mastery);
        var activeBonus = performance * ProfessionTuning.ActiveBonusChanceAtFullPerformance;

        if (landed)
        {
            foreach (var output in action.Outputs)
                produced.Add(output);

            foreach (var bonus in action.BonusOutputs)
            {
                var chance = bonus.Chance + masteryBonus + activeBonus;
                if (random.NextDouble() < chance)
                    produced.Add(bonus.Stack);
            }
        }

        var xp = (long)Math.Round(action.Experience * (1.0 + (performance * ProfessionTuning.ActiveXpBonusAtFullPerformance)));
        if (!landed)
            xp = (long)Math.Round(xp * ProfessionTuning.MissedAttemptXpFraction);

        return new ResolvedYield
        {
            Produced = produced,
            Xp = xp,
            Landed = landed,
            Discovered = isActive ? DiscoverOpportunity(action, mastery, performance, random) : null,
        };
    }

    /// <summary>
    /// Rolls each of the action's opportunities in authored order and returns the first that
    /// fires. One at a time on purpose: two simultaneous offers would turn a decision into a
    /// menu, and the point is the decision.
    /// </summary>
    private static ProfessionOpportunityDefinition? DiscoverOpportunity(
        ProfessionActionDefinition action,
        int mastery,
        double performance,
        IRandomSource random)
    {
        foreach (var opportunity in action.Opportunities)
        {
            var chance = ProfessionTuning.OpportunityDiscoveryChance(opportunity.DiscoveryChance, mastery, performance);
            if (random.NextDouble() < chance)
                return opportunity;
        }

        return null;
    }

    /// <summary>
    /// Resolves a pursued opportunity: rolls its risk, then produces its payoff. Separate
    /// from <see cref="Resolve"/> because the player's decision sits between them.
    /// </summary>
    public static ResolvedYield ResolvePursuit(
        ProfessionOpportunityDefinition opportunity,
        int mastery,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        ArgumentNullException.ThrowIfNull(random);

        var risk = ProfessionTuning.EffectiveRisk(opportunity.RiskWeight, mastery);
        var landed = risk <= 0.0 || random.NextDouble() >= risk;

        var produced = new List<ItemStack>();
        if (landed)
        {
            foreach (var output in opportunity.Outputs)
                produced.Add(output);

            var masteryBonus = ProfessionTuning.MasteryBonusChance(mastery);
            foreach (var bonus in opportunity.BonusOutputs)
            {
                if (random.NextDouble() < bonus.Chance + masteryBonus)
                    produced.Add(bonus.Stack);
            }
        }

        var xp = landed
            ? opportunity.Experience
            : (long)Math.Round(opportunity.Experience * ProfessionTuning.MissedAttemptXpFraction);

        return new ResolvedYield { Produced = produced, Xp = xp, Landed = landed };
    }
}
