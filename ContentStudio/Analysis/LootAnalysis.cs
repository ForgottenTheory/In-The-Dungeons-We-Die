using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Loot;

namespace ContentStudio.Analysis;

/// <summary>
/// Expected-value analysis over the real loot definitions: what a table pays per roll, where
/// an item actually comes from, and which tables nothing reaches. Probabilities mirror
/// <c>LootResolver</c> semantics (independent chance drops, weight shares within the eligible
/// subset of a draw, nested tables, context-gated entries, per-table tag accumulation).
/// </summary>
public static class LootAnalysis
{
    /// <summary>The circumstances an expected-value question is asked under.</summary>
    public sealed record LootEvaluationContext(int Depth, IReadOnlySet<string> Tags)
    {
        public static LootEvaluationContext From(int depth, bool active, string? rank, IEnumerable<string>? extraTags)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { active ? "active" : "passive" };
            if (depth > 0)
                tags.Add("in_realm");
            if (!string.IsNullOrEmpty(rank) && !rank.Equals("normal", StringComparison.OrdinalIgnoreCase))
                tags.Add(rank.ToLowerInvariant());
            foreach (var tag in extraTags ?? Array.Empty<string>())
                tags.Add(tag);
            return new LootEvaluationContext(depth, tags);
        }

        public LootEvaluationContext WithTags(IReadOnlyList<string> moreTags)
        {
            if (moreTags.Count == 0)
                return this;
            var merged = new HashSet<string>(Tags, StringComparer.OrdinalIgnoreCase);
            foreach (var tag in moreTags)
                merged.Add(tag);
            return this with { Tags = merged };
        }
    }

    public sealed record ExpectedItem(string ItemId, double ExpectedPerRoll);

    public sealed record TableExpectation(
        string TableId,
        IReadOnlyList<ExpectedItem> Items,
        double ExpectedGold,
        IReadOnlyList<string> NestedTableIds);

    // ── Expected value per table ────────────────────────────────────────────────────────────

    public static TableExpectation ExpectedValueOf(ContentBundle bundle, string tableId, LootEvaluationContext context)
    {
        var items = new Dictionary<string, double>(StringComparer.Ordinal);
        var nested = new HashSet<string>(StringComparer.Ordinal);
        double gold = 0;
        AccumulateTable(bundle, tableId, context, weightOfThisRoll: 1.0, items, ref gold, nested, depth: 0);
        return new TableExpectation(
            tableId,
            items.OrderByDescending(pair => pair.Value)
                 .Select(pair => new ExpectedItem(pair.Key, Math.Round(pair.Value, 5))).ToList(),
            Math.Round(gold, 3),
            nested.OrderBy(id => id, StringComparer.Ordinal).ToList());
    }

    private static void AccumulateTable(ContentBundle bundle, string tableId, LootEvaluationContext outerContext,
        double weightOfThisRoll, Dictionary<string, double> items, ref double gold, HashSet<string> nestedSeen, int depth)
    {
        if (depth >= LootTuning.MaxNestingDepth || !bundle.LootTables.TryGetById(tableId, out var table))
            return;
        var context = outerContext.WithTags(table.Tags);

        foreach (var entry in table.AlwaysDrops)
        {
            if (Allowed(entry.When, context))
                AccumulateEntry(bundle, entry, context, weightOfThisRoll, items, ref gold, nestedSeen, depth);
        }
        foreach (var entry in table.ChanceDrops)
        {
            if (Allowed(entry.When, context))
                AccumulateEntry(bundle, entry, context, weightOfThisRoll * Math.Clamp(entry.Chance, 0, 1), items, ref gold, nestedSeen, depth);
        }
        foreach (var draw in table.WeightedDraws)
        {
            if (!Allowed(draw.When, context))
                continue;
            var eligible = draw.Entries.Where(entry => Allowed(entry.When, context)).ToList();
            var totalWeight = eligible.Sum(entry => Math.Max(0, entry.Weight));
            if (totalWeight <= 0)
                continue;
            var picks = Math.Clamp(draw.Picks, 0, LootTuning.MaxPicksPerDraw);
            foreach (var entry in eligible)
            {
                var share = Math.Max(0, entry.Weight) / totalWeight;
                AccumulateEntry(bundle, entry, context, weightOfThisRoll * share * picks, items, ref gold, nestedSeen, depth);
            }
        }
        if (table.Gold is { } coin && Allowed(coin.When, context))
        {
            var chance = Math.Clamp(coin.Chance, 0, 1);
            gold += weightOfThisRoll * chance * (coin.MinAmount + coin.MaxAmount) / 2.0;
        }
    }

    private static void AccumulateEntry(ContentBundle bundle, LootEntryDefinition entry, LootEvaluationContext context,
        double probability, Dictionary<string, double> items, ref double gold, HashSet<string> nestedSeen, int depth)
    {
        if (probability <= 0 || entry.DropsNothing)
            return;
        if (entry.TableId is not null)
        {
            nestedSeen.Add(entry.TableId);
            AccumulateTable(bundle, entry.TableId, context, probability, items, ref gold, nestedSeen, depth + 1);
            return;
        }
        if (entry.ItemId is null)
            return;
        var averageQuantity = (Math.Min(entry.MinQuantity, entry.MaxQuantity) + Math.Max(entry.MinQuantity, entry.MaxQuantity)) / 2.0;
        if (averageQuantity <= 0)
            return;
        items.TryGetValue(entry.ItemId, out var existing);
        items[entry.ItemId] = existing + probability * averageQuantity;
    }

    private static bool Allowed(LootCondition? condition, LootEvaluationContext context)
    {
        if (condition is null)
            return true;
        if (context.Depth < condition.MinDepth)
            return false;
        if (condition.MaxDepth is { } maxDepth && context.Depth > maxDepth)
            return false;
        foreach (var required in condition.RequiresTags)
        {
            if (!context.Tags.Contains(required))
                return false;
        }
        foreach (var excluded in condition.ExcludesTags)
        {
            if (context.Tags.Contains(excluded))
                return false;
        }
        return true;
    }

    // ── Sources: who in the world actually pays this table / item ───────────────────────────

    public sealed record LootSource(string SourceKind, string SourceId, string SourceName, IReadOnlyList<string> TableIds);

    /// <summary>Every root loot payer in the game: enemies (family+role+actor merged),
    /// realm locations and profession actions.</summary>
    public static List<LootSource> EnumerateRootSources(ContentBundle bundle)
    {
        var sources = new List<LootSource>();

        foreach (var actor in bundle.Actors.GetAll())
        {
            try
            {
                var resolved = ActorResolver.Resolve(actor, bundle.EnemyFamilies, bundle.EnemyRoles, bundle.AiProfiles);
                if (resolved.LootTableIds.Count > 0)
                    sources.Add(new LootSource("enemy", actor.Id, resolved.Name, resolved.LootTableIds));
            }
            catch (KeyNotFoundException)
            {
                // Broken composition is a validation problem, not a loot source.
            }
        }

        foreach (var realm in bundle.Realms.GetAll())
        {
            foreach (var location in realm.Locations)
            {
                if (location.LootTableId is not null)
                {
                    sources.Add(new LootSource("realm-location", $"{realm.Id}/{location.Id}",
                        $"{realm.Name} · {location.Name}", new[] { location.LootTableId }));
                }
            }
        }

        foreach (var action in bundle.Actions.GetAll())
        {
            if (action.LootTableId is not null)
                sources.Add(new LootSource("profession-action", action.Id, action.Name, new[] { action.LootTableId }));
        }

        return sources;
    }

    public sealed record ItemSourceRow(
        string SourceKind, string SourceId, string SourceName, double ExpectedPerEvent, IReadOnlyList<string> ContextNotes);

    /// <summary>"Where does Storm Core come from?" — every source whose loot can pay the item,
    /// with the expected amount per kill/gather/event under the given context.</summary>
    public static List<ItemSourceRow> SourcesOfItem(ContentBundle bundle, string itemId, LootEvaluationContext context)
    {
        var rows = new List<ItemSourceRow>();
        foreach (var source in EnumerateRootSources(bundle))
        {
            var sourceContext = source.SourceKind == "enemy"
                ? EnemyContext(bundle, source.SourceId, context)
                : context;

            double expected = 0;
            foreach (var tableId in source.TableIds)
            {
                var expectation = ExpectedValueOf(bundle, tableId, sourceContext);
                expected += expectation.Items.FirstOrDefault(item => item.ItemId == itemId)?.ExpectedPerRoll ?? 0;
            }
            if (expected > 0)
                rows.Add(new ItemSourceRow(source.SourceKind, source.SourceId, source.SourceName,
                    Math.Round(expected, 5), sourceContext.Tags.OrderBy(tag => tag).ToList()));
        }
        return rows.OrderByDescending(row => row.ExpectedPerEvent).ToList();
    }

    /// <summary>Enemy kills add the enemy's own identity tags (elite/boss/family:*) to the context.</summary>
    private static LootEvaluationContext EnemyContext(ContentBundle bundle, string actorId, LootEvaluationContext context)
    {
        if (!bundle.Actors.TryGetById(actorId, out var actor))
            return context;
        try
        {
            var resolved = ActorResolver.Resolve(actor, bundle.EnemyFamilies, bundle.EnemyRoles, bundle.AiProfiles);
            return context.WithTags(resolved.Tags.ToList());
        }
        catch (KeyNotFoundException)
        {
            return context;
        }
    }

    // ── Health of the loot library ──────────────────────────────────────────────────────────

    public sealed record LootOverview(
        int TableCount,
        IReadOnlyList<string> OrphanTableIds,
        IReadOnlyList<string> EmptyPayoutTableIds,
        IReadOnlyList<string> DroppableItemIdsWithNoSource);

    public static LootOverview BuildOverview(ContentBundle bundle)
    {
        var rootSources = EnumerateRootSources(bundle);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tableId in rootSources.SelectMany(source => source.TableIds))
        {
            foreach (var id in LootReachability.TablesReachableFrom(bundle.LootTables, tableId))
                reachable.Add(id);
        }

        var orphans = bundle.LootTables.GetAll()
            .Select(table => table.Id)
            .Where(id => !reachable.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        // Tables that pay literally nothing under the most generous context.
        var generous = LootEvaluationContext.From(depth: 9, active: true, rank: "boss",
            bundle.Realms.GetAll().SelectMany(realm => realm.Tags.Concat(new[] { realm.Id })));
        var emptyPayout = bundle.LootTables.GetAll()
            .Where(table =>
            {
                var expectation = ExpectedValueOf(bundle, table.Id, generous);
                return expectation.Items.Count == 0 && expectation.ExpectedGold <= 0;
            })
            .Select(table => table.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        return new LootOverview(bundle.LootTables.Count, orphans, emptyPayout, Array.Empty<string>());
    }
}
