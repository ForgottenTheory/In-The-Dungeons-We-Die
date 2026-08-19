using Dungeons.Content;
using Dungeons.Professions;

namespace ContentStudio.Analysis;

/// <summary>
/// Profession economics computed from the shipped tuning: XP/hour, throughput per hour and the
/// levelling timeline. Mirrors the game's formulas (ActionResolver + ProfessionTuning) rather
/// than inventing its own — the one XP/hour formula lives here and nowhere else.
/// </summary>
public static class ProfessionAnalysis
{
    private const double TicksPerHour = 3600.0 * 20.0;

    public sealed record ActionRow(
        string Id, string Name, string ProfessionId, int RequiredLevel,
        int BaseIntervalTicks, double IntervalSeconds, long Experience, double SuccessChance,
        double XpPerHourPassive, double XpPerHourActive,
        IReadOnlyDictionary<string, double> InputsPerHour,
        IReadOnlyDictionary<string, double> OutputsPerHourPassive,
        IReadOnlyDictionary<string, double> BonusOutputsPerHourPassive,
        string? LootTableId, int OpportunityCount);

    public sealed record ProfessionSummary(
        string Id, string Name, string Category, int ActionCount,
        int LowestActionLevel, int HighestActionLevel,
        IReadOnlyList<int> LevelsWithNewActions,
        double EstimatedHoursTo99Passive,
        IReadOnlyList<TimelinePoint> Timeline);

    /// <summary>The best available XP/hour from a given level onward (passive, mastery 0).</summary>
    public sealed record TimelinePoint(int Level, string BestActionId, double XpPerHourPassive);

    public static List<ActionRow> BuildActionTable(ContentBundle bundle)
    {
        var rows = new List<ActionRow>();
        foreach (var action in bundle.Actions.GetAll().OrderBy(action => action.ProfessionId, StringComparer.Ordinal)
                                                      .ThenBy(action => action.RequiredLevel))
        {
            var completionsPerHour = TicksPerHour / Math.Max(1, action.BaseIntervalTicks);
            rows.Add(new ActionRow(
                action.Id, action.Name, action.ProfessionId, action.RequiredLevel,
                action.BaseIntervalTicks, Math.Round(action.BaseIntervalTicks / 20.0, 2),
                action.Experience, action.SuccessChance,
                Math.Round(XpPerHour(action, performance: 0.0), 1),
                Math.Round(XpPerHour(action, performance: 1.0), 1),
                PerHour(action.Inputs.Select(stack => (stack.ItemId, (double)stack.Quantity)), completionsPerHour),
                PerHour(action.Outputs.Select(stack => (stack.ItemId, stack.Quantity * action.SuccessChance)), completionsPerHour),
                PerHour(action.BonusOutputs.Select(bonus => (bonus.ItemId, bonus.Quantity * bonus.Chance * action.SuccessChance)), completionsPerHour),
                action.LootTableId, action.Opportunities.Count));
        }
        return rows;
    }

    /// <summary>
    /// completions/hour × expected XP per attempt. A miss still pays
    /// <see cref="ProfessionTuning.MissedAttemptXpFraction"/>; active play adds the timing
    /// bonus at full performance. Mastery interval reduction is not applied (mastery 0 view).
    /// </summary>
    private static double XpPerHour(ProfessionActionDefinition action, double performance)
    {
        var completionsPerHour = TicksPerHour / Math.Max(1, action.BaseIntervalTicks);
        var xpOnLand = Math.Round(action.Experience * (1.0 + performance * ProfessionTuning.ActiveXpBonusAtFullPerformance));
        var expectedXp = action.SuccessChance * xpOnLand
                         + (1.0 - action.SuccessChance) * Math.Round(xpOnLand * ProfessionTuning.MissedAttemptXpFraction);
        return completionsPerHour * expectedXp;
    }

    private static Dictionary<string, double> PerHour(IEnumerable<(string ItemId, double PerCompletion)> amounts, double completionsPerHour)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (itemId, perCompletion) in amounts)
        {
            result.TryGetValue(itemId, out var existing);
            result[itemId] = Math.Round(existing + perCompletion * completionsPerHour, 2);
        }
        return result;
    }

    public static List<ProfessionSummary> BuildProfessionSummaries(ContentBundle bundle, List<ActionRow> actionRows)
    {
        var summaries = new List<ProfessionSummary>();
        foreach (var profession in bundle.Professions.GetAll().OrderBy(profession => profession.Id, StringComparer.Ordinal))
        {
            var actions = actionRows.Where(row => row.ProfessionId == profession.Id).ToList();
            if (actions.Count == 0)
            {
                summaries.Add(new ProfessionSummary(profession.Id, profession.Name, profession.Category.ToString(),
                    0, 0, 0, Array.Empty<int>(), 0, Array.Empty<TimelinePoint>()));
                continue;
            }

            var timeline = new List<TimelinePoint>();
            var hoursTo99 = 0.0;
            string? previousBest = null;
            for (var level = 1; level < ProfessionLeveling.MaxLevel; level++)
            {
                var best = actions.Where(row => row.RequiredLevel <= level)
                    .OrderByDescending(row => row.XpPerHourPassive)
                    .FirstOrDefault();
                if (best is null)
                    continue;
                if (best.Id != previousBest)
                {
                    timeline.Add(new TimelinePoint(level, best.Id, best.XpPerHourPassive));
                    previousBest = best.Id;
                }
                // XP from this level to the next is 100 × level (the shipped triangular curve).
                var xpToNextLevel = 100.0 * level;
                if (best.XpPerHourPassive > 0)
                    hoursTo99 += xpToNextLevel / best.XpPerHourPassive;
            }

            summaries.Add(new ProfessionSummary(
                profession.Id, profession.Name, profession.Category.ToString(),
                actions.Count,
                actions.Min(row => row.RequiredLevel),
                actions.Max(row => row.RequiredLevel),
                actions.Select(row => row.RequiredLevel).Distinct().OrderBy(level => level).ToList(),
                Math.Round(hoursTo99, 1),
                timeline));
        }
        return summaries;
    }
}
