using Dungeons.Content;

namespace ContentStudio.Analysis;

/// <summary>
/// Non-destructive outlier detection across the whole library. Every finding is a suggestion
/// for a human to look at — nothing here ever changes a value, by design.
/// </summary>
public static class BalanceWarningAggregator
{
    public sealed record BalanceWarning(string Area, string Message, string? RecordId, string? TypeId);

    /// <summary>How many standard deviations from the cohort mean counts as "worth a look".</summary>
    private const double OutlierZScore = 2.2;

    /// <summary>Minimum cohort size before z-scores mean anything.</summary>
    private const int MinimumCohortSize = 8;

    public static List<BalanceWarning> Collect(ContentBundle bundle)
    {
        var warnings = new List<BalanceWarning>();
        CollectEnemyWarnings(bundle, warnings);
        CollectMoveWarnings(bundle, warnings);
        CollectMaterialWarnings(bundle, warnings);
        CollectProfessionWarnings(bundle, warnings);
        CollectLootWarnings(bundle, warnings);
        return warnings;
    }

    private static void CollectEnemyWarnings(ContentBundle bundle, List<BalanceWarning> warnings)
    {
        var rows = EnemyAnalysis.BuildTable(bundle);
        if (rows.Count < MinimumCohortSize)
            return;

        // Compare within rank cohorts — a boss towering over normals is design, not a bug.
        foreach (var cohort in rows.GroupBy(row => row.Rank))
        {
            var cohortRows = cohort.ToList();
            if (cohortRows.Count < MinimumCohortSize)
                continue;
            FlagOutliers(cohortRows, row => row.Health, (row, z) => warnings.Add(new BalanceWarning(
                "enemies", $"{row.Name} ({row.Id}) has unusual health for a {cohort.Key} enemy: {row.Health} ({Deviation(z)}).",
                row.Id, "actors")));
            FlagOutliers(cohortRows, row => AverageEhp(row), (row, z) => warnings.Add(new BalanceWarning(
                "enemies", $"{row.Name} ({row.Id}) has unusual effective durability for a {cohort.Key} enemy ({Deviation(z)}).",
                row.Id, "actors")));
        }
    }

    private static double AverageEhp(EnemyAnalysis.EnemyRow row) =>
        row.EffectiveHp.Values.Where(double.IsFinite).DefaultIfEmpty(row.Health).Average();

    private static void CollectMoveWarnings(ContentBundle bundle, List<BalanceWarning> warnings)
    {
        var rows = MoveAnalysis.BuildTable(bundle).Where(row => row.TotalDamage > 0).ToList();
        if (rows.Count >= MinimumCohortSize)
        {
            FlagOutliers(rows, row => row.DamagePerSecondOfCycle, (row, z) => warnings.Add(new BalanceWarning(
                "moves", $"{row.Name} ({row.Id}) deals {row.DamagePerSecondOfCycle:0.##} damage per second of cycle ({Deviation(z)}). " +
                         "Check whether telegraph/utility justifies it.", row.Id, "moves")));

            var withStamina = rows.Where(row => row.DamagePerStamina is not null).ToList();
            if (withStamina.Count >= MinimumCohortSize)
            {
                FlagOutliers(withStamina, row => row.DamagePerStamina!.Value, (row, z) => warnings.Add(new BalanceWarning(
                    "moves", $"{row.Name} ({row.Id}) deals {row.DamagePerStamina:0.##} damage per stamina ({Deviation(z)}).",
                    row.Id, "moves")));
            }
        }
    }

    private static void CollectMaterialWarnings(ContentBundle bundle, List<BalanceWarning> warnings)
    {
        var materials = bundle.Materials.GetAll().ToList();
        foreach (var property in bundle.Properties.GetAll())
        {
            var values = materials
                .Where(material => material.Properties.ContainsKey(property.Id))
                .Select(material => (material.Id, Value: material.Properties[property.Id]))
                .ToList();
            if (values.Count < 30)
                continue;

            var bottomShare = values.Count(pair => pair.Value <= 20) / (double)values.Count;
            if (bottomShare >= 0.75)
            {
                warnings.Add(new BalanceWarning("materials",
                    $"{bottomShare:P0} of the {values.Count} materials with {property.Id} sit in the bottom 20% of its range — " +
                    "the property may have little differentiation.", null, "materials"));
            }

            var mean = values.Average(pair => pair.Value);
            var standardDeviation = StandardDeviation(values.Select(pair => pair.Value), mean);
            if (standardDeviation < 0.001)
                continue;
            foreach (var (id, value) in values)
            {
                var z = (value - mean) / standardDeviation;
                if (Math.Abs(z) >= 3.5)
                {
                    warnings.Add(new BalanceWarning("materials",
                        $"{id} has an extreme {property.Id} of {value:0.#} (library mean {mean:0.#}).", id, "materials"));
                }
            }
        }
    }

    private static void CollectProfessionWarnings(ContentBundle bundle, List<BalanceWarning> warnings)
    {
        var actions = ProfessionAnalysis.BuildActionTable(bundle);
        foreach (var professionGroup in actions.GroupBy(row => row.ProfessionId))
        {
            var rows = professionGroup.Where(row => row.XpPerHourPassive > 0).ToList();
            if (rows.Count < 4)
                continue;
            var median = Median(rows.Select(row => row.XpPerHourPassive));
            foreach (var row in rows)
            {
                if (median > 0 && row.XpPerHourPassive / median >= 3.0)
                {
                    warnings.Add(new BalanceWarning("professions",
                        $"{row.Name} ({row.Id}) yields {row.XpPerHourPassive:0} XP/h — {row.XpPerHourPassive / median:0.#}× its profession's median. " +
                        "Deliberate capstone or accidental best-in-slot?", row.Id, "profession_actions"));
                }
            }
        }
    }

    private static void CollectLootWarnings(ContentBundle bundle, List<BalanceWarning> warnings)
    {
        var overview = LootAnalysis.BuildOverview(bundle);
        foreach (var orphan in overview.OrphanTableIds)
        {
            warnings.Add(new BalanceWarning("loot",
                $"{orphan} is reachable from no enemy, realm location or profession action — orphaned loot.", orphan, "loot_tables"));
        }
        foreach (var empty in overview.EmptyPayoutTableIds)
        {
            warnings.Add(new BalanceWarning("loot",
                $"{empty} pays nothing even under the most generous context (deep, active, boss).", empty, "loot_tables"));
        }
    }

    // ── Small statistics helpers ────────────────────────────────────────────────────────────

    private static void FlagOutliers<T>(IReadOnlyList<T> rows, Func<T, double> metric, Action<T, double> onOutlier)
    {
        var values = rows.Select(metric).Where(double.IsFinite).ToList();
        if (values.Count < MinimumCohortSize)
            return;
        var mean = values.Average();
        var standardDeviation = StandardDeviation(values, mean);
        if (standardDeviation < 0.0001)
            return;
        foreach (var row in rows)
        {
            var value = metric(row);
            if (!double.IsFinite(value))
                continue;
            var z = (value - mean) / standardDeviation;
            if (Math.Abs(z) >= OutlierZScore)
                onOutlier(row, z);
        }
    }

    private static double StandardDeviation(IEnumerable<double> values, double mean)
    {
        var list = values as IReadOnlyCollection<double> ?? values.ToList();
        return Math.Sqrt(list.Sum(value => (value - mean) * (value - mean)) / Math.Max(1, list.Count - 1));
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToList();
        if (sorted.Count == 0)
            return 0;
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    private static string Deviation(double z) => $"{(z >= 0 ? "+" : "")}{z:0.0}σ vs cohort";
}
