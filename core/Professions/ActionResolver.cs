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

    /// <summary>True when mastery saved the action's inputs — they were not consumed at all.</summary>
    public bool InputsPreserved { get; init; }

    /// <summary>How many primary outputs came out twice. Reported rather than folded silently
    /// into <see cref="Produced"/>, so the log can say what mastery just did.</summary>
    public int OutputsDoubled { get; init; }

    /// <summary>An opportunity this attempt noticed, awaiting the player's pursue/ignore
    /// decision. Always null for passive attempts.</summary>
    public ProfessionOpportunityDefinition? Discovered { get; init; }
}

/// <summary>
/// Pure resolution of a single action attempt: rolls the success chance, computes guaranteed
/// outputs, rolls doubling and bonus outputs, scales XP, decides whether the inputs survived,
/// and — on the active path only — rolls for an opportunity to offer.
///
/// <para>This is the shared core of passive (performance = 0, <c>isActive</c> false) and active
/// execution, so the two can never drift into separate balance models (docs/architecture.md §20,
/// docs/professions.md §4).</para>
///
/// <para><b>Every benefit magnitude arrives through <see cref="ProfessionBenefits"/></b> rather
/// than as a constant here — that is what makes the mastery ladder and the synergy table JSON
/// edits, and what let Phase 10 add cross-profession and global bonuses without touching a line
/// of this file.</para>
/// </summary>
public static class ActionResolver
{
    public static ResolvedYield Resolve(
        ProfessionActionDefinition action,
        int mastery,
        double performance,
        IRandomSource random,
        bool isActive = false,
        ProfessionBenefits? professionBenefits = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(random);
        performance = Math.Clamp(performance, 0.0, 1.0);
        var benefits = professionBenefits ?? ProfessionBenefits.None;

        // Rolled before anything else so the RNG stream does not shift depending on whether
        // the attempt landed — seeded tests would otherwise be impossible to reason about.
        var landed = action.SuccessChance >= 1.0 || random.NextDouble() < action.SuccessChance;

        var produced = new List<ItemStack>();
        var extraBonusOutputChance = benefits.ValueOf(ProfessionBenefitKind.BonusOutputChance, action.ProfessionId, mastery);
        var doublingChance = benefits.ValueOf(ProfessionBenefitKind.OutputDoubling, action.ProfessionId, mastery);
        var activeBonus = performance * ProfessionTuning.ActiveBonusChanceAtFullPerformance;
        var doubled = 0;

        if (landed)
        {
            // Doubling is rolled PER OUTPUT rather than once for the attempt: an action that
            // makes three things should be able to double one of them, which is what makes a
            // doubled result feel like a lucky moment rather than an occasional double payday.
            foreach (var output in action.Outputs)
            {
                produced.Add(output);
                if (doublingChance > 0 && random.NextDouble() < doublingChance)
                {
                    produced.Add(output);
                    doubled++;
                }
            }

            foreach (var bonus in action.BonusOutputs)
            {
                var chance = bonus.Chance + extraBonusOutputChance + activeBonus;
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
            InputsPreserved = RollPreservation(action, mastery, random, benefits),
            OutputsDoubled = doubled,
            Discovered = isActive ? DiscoverOpportunity(action, mastery, performance, random, benefits) : null,
        };
    }

    /// <summary>
    /// Whether the hand that has done this a thousand times gets to keep its materials.
    ///
    /// <para>Rolled even on an attempt that missed, and even for an action with no inputs, so the
    /// RNG stream does not shift with the shape of the action — the same discipline the success
    /// roll follows. <see cref="ProfessionSystem"/> is what decides the roll matters.</para>
    /// </summary>
    private static bool RollPreservation(
        ProfessionActionDefinition action,
        int mastery,
        IRandomSource random,
        ProfessionBenefits benefits)
    {
        var chance = benefits.ValueOf(ProfessionBenefitKind.InputPreservation, action.ProfessionId, mastery);
        return chance > 0 && random.NextDouble() < chance;
    }

    /// <summary>
    /// Rolls each of the action's opportunities in authored order and returns the first that
    /// fires. One at a time on purpose: two simultaneous offers would turn a decision into a
    /// menu, and the point is the decision.
    ///
    /// <para>An opportunity above the party's mastery is not rolled at all — deep experience in
    /// one action is what surfaces offers a novice never sees.</para>
    /// </summary>
    private static ProfessionOpportunityDefinition? DiscoverOpportunity(
        ProfessionActionDefinition action,
        int mastery,
        double performance,
        IRandomSource random,
        ProfessionBenefits benefits)
    {
        var extraDiscoveryChance = benefits.ValueOf(ProfessionBenefitKind.OpportunityChance, action.ProfessionId, mastery);

        foreach (var opportunity in action.Opportunities)
        {
            if (MasteryLeveling.LevelFor(mastery) < opportunity.RequiredMasteryLevel)
                continue;

            var chance = ProfessionTuning.OpportunityDiscoveryChance(opportunity.DiscoveryChance, extraDiscoveryChance, performance);
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
        IRandomSource random,
        string? professionId = null,
        ProfessionBenefits? professionBenefits = null)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        ArgumentNullException.ThrowIfNull(random);
        var benefits = professionBenefits ?? ProfessionBenefits.None;

        var riskReduction = benefits.ValueOf(ProfessionBenefitKind.OpportunityRisk, professionId, mastery);
        var risk = ProfessionTuning.EffectiveRisk(opportunity.RiskWeight, riskReduction);
        var landed = risk <= 0.0 || random.NextDouble() >= risk;

        var produced = new List<ItemStack>();
        if (landed)
        {
            foreach (var output in opportunity.Outputs)
                produced.Add(output);

            var extraBonusOutputChance = benefits.ValueOf(ProfessionBenefitKind.BonusOutputChance, professionId, mastery);
            foreach (var bonus in opportunity.BonusOutputs)
            {
                if (random.NextDouble() < bonus.Chance + extraBonusOutputChance)
                    produced.Add(bonus.Stack);
            }
        }

        var xp = landed
            ? opportunity.Experience
            : (long)Math.Round(opportunity.Experience * ProfessionTuning.MissedAttemptXpFraction);

        return new ResolvedYield { Produced = produced, Xp = xp, Landed = landed };
    }
}
