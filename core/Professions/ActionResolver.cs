using Dungeons.Items;
using Dungeons.Randomness;

namespace Dungeons.Professions;

/// <summary>Items produced and XP earned by resolving one action attempt.</summary>
public sealed class ResolvedYield
{
    public required IReadOnlyList<ItemStack> Produced { get; init; }
    public required long Xp { get; init; }
}

/// <summary>
/// Pure resolution of a single action attempt: computes guaranteed outputs, rolls
/// bonus outputs, and scales XP. Higher mastery and higher active performance both
/// improve bonus-output chance; performance also boosts XP. This is the shared core
/// of passive (performance = 0) and active (performance in (0, 1]) execution, so the
/// two can never drift into separate balance models (docs/architecture.md §20,
/// docs/professions.md §4).
/// </summary>
public static class ActionResolver
{
    public static ResolvedYield Resolve(ProfessionActionDefinition action, int mastery, double performance, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(rng);
        performance = Math.Clamp(performance, 0.0, 1.0);

        var produced = new List<ItemStack>();
        foreach (var output in action.Outputs)
            produced.Add(output.ToStack());

        var masteryBonus = ProfessionTuning.MasteryBonusChance(mastery);
        var activeBonus = performance * ProfessionTuning.ActiveBonusChanceAtFullPerformance;
        foreach (var bonus in action.BonusOutputs)
        {
            var chance = bonus.Chance + masteryBonus + activeBonus;
            if (rng.NextDouble() < chance)
                produced.Add(bonus.ToStack());
        }

        var xp = (long)Math.Round(action.Experience * (1.0 + (performance * ProfessionTuning.ActiveXpBonusAtFullPerformance)));

        return new ResolvedYield { Produced = produced, Xp = xp };
    }
}
