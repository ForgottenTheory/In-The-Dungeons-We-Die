using Dungeons.Combat;
using Dungeons.Content;

namespace ContentStudio.Analysis;

/// <summary>
/// Per-move numbers for the Balance screen. Deliberately more than damage-per-second:
/// telegraphs, control, riders and costs are all part of what a move is worth, so the table
/// carries them side by side and leaves the judgement to the designer.
/// </summary>
public static class MoveAnalysis
{
    private const double TicksPerSecond = 20.0;

    public sealed record MoveRow(
        string Id, string Name, string Kind, IReadOnlyList<string> Tags,
        double TotalDamage,
        IReadOnlyDictionary<string, double> DamageByType,
        IReadOnlyDictionary<string, double> DamageByAspect,
        int TelegraphTicks, int WindupTicks, int RecoveryTicks, int TimeToImpactTicks, int CycleTicks,
        int CooldownTicks, double StaggerPower, bool Interruptible, string Targeting, int MaxTargets,
        IReadOnlyDictionary<string, double> Costs,
        double DamagePerSecondOfCycle, double? DamagePerStamina, double? DamagePerMana,
        IReadOnlyList<string> EffectSummaries, IReadOnlyList<string> RequirementSummaries);

    public static List<MoveRow> BuildTable(ContentBundle bundle)
    {
        var rows = new List<MoveRow>();
        foreach (var move in bundle.Moves.GetAll().OrderBy(move => move.Id, StringComparer.Ordinal))
        {
            var totalDamage = move.Packets.Sum(packet => packet.Amount);
            var byType = move.Packets
                .GroupBy(packet => packet.Type.ToString())
                .ToDictionary(group => group.Key, group => group.Sum(packet => packet.Amount));
            var byAspect = move.Packets
                .Where(packet => packet.Aspect is not null)
                .GroupBy(packet => packet.Aspect!)
                .ToDictionary(group => group.Key, group => group.Sum(packet => packet.Amount));

            var costs = move.Costs
                .GroupBy(cost => cost.Resource, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key.ToLowerInvariant(), group => group.Sum(cost => cost.Amount));

            var cycleTicks = move.Timing.TelegraphTicks + move.Timing.WindupTicks + move.Timing.RecoveryTicks;
            var effectiveCycleTicks = Math.Max(cycleTicks, move.CooldownTicks);
            var damagePerSecond = effectiveCycleTicks > 0 ? totalDamage / (effectiveCycleTicks / TicksPerSecond) : 0;

            rows.Add(new MoveRow(
                move.Id, move.Name, move.Kind.ToString(), move.Tags,
                totalDamage, byType, byAspect,
                move.Timing.TelegraphTicks, move.Timing.WindupTicks, move.Timing.RecoveryTicks,
                move.Timing.TelegraphTicks + move.Timing.WindupTicks, cycleTicks,
                move.CooldownTicks, move.StaggerPower, move.Interruptible, move.Targeting.ToString(), move.MaxTargets,
                costs,
                Math.Round(damagePerSecond, 2),
                PerCost(totalDamage, costs, "stamina"),
                PerCost(totalDamage, costs, "mana"),
                move.Effects.Select(Summarize).ToList(),
                move.Requires.Select(condition => $"{condition.Kind} {condition.Text}".Trim()).ToList()));
        }
        return rows;
    }

    private static double? PerCost(double totalDamage, IReadOnlyDictionary<string, double> costs, string resource) =>
        costs.TryGetValue(resource, out var amount) && amount > 0 ? Math.Round(totalDamage / amount, 2) : null;

    private static string Summarize(Dungeons.Rules.EffectSpec effect)
    {
        var parts = new List<string> { effect.Kind };
        if (!string.IsNullOrEmpty(effect.Text))
            parts.Add(effect.Text);
        if (effect.Amount != 0)
            parts.Add(effect.Amount.ToString("0.##"));
        if (effect.Chance < 1.0)
            parts.Add($"@{effect.Chance:P0}");
        return string.Join(' ', parts);
    }
}
