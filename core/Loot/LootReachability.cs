using Dungeons.Content;

namespace Dungeons.Loot;

/// <summary>
/// Walks a loot table graph without rolling it: which tables a table can reach, and which items
/// it can ever yield.
///
/// <para>This exists because several rules need to ask "what can this source <em>possibly</em>
/// drop?" rather than "what did it drop this time" — content validation (cycles, unknown items),
/// the D29.3 essence-source audit (a profession must not become an essence faucet through a
/// nested table), and the D28 check that no enemy table hands out finished equipment. Rolling a
/// table a thousand times would answer those questions probabilistically; walking it answers
/// them exactly.</para>
/// </summary>
public static class LootReachability
{
    /// <summary>
    /// Every table reachable from <paramref name="tableId"/>, including itself. Cycle-safe: a
    /// table that reaches itself appears once, which is also how a cycle is detected —
    /// <c>TablesReachableFrom(tables, id).Contains(id)</c> is only true for a real cycle when
    /// the starting table is excluded, so validation uses <see cref="FormsCycle"/> instead.
    /// </summary>
    public static IReadOnlySet<string> TablesReachableFrom(DataStore<LootTableDefinition> tables, string tableId)
    {
        ArgumentNullException.ThrowIfNull(tables);

        var reached = new HashSet<string>(StringComparer.Ordinal);
        Walk(tables, tableId, reached);
        return reached;
    }

    /// <summary>True when a table can reach itself through nested entries — an infinite roll.</summary>
    public static bool FormsCycle(DataStore<LootTableDefinition> tables, string tableId)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (!tables.TryGetById(tableId, out var table))
            return false;

        var reachedFromChildren = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nested in NestedTableIds(table))
            Walk(tables, nested, reachedFromChildren);

        return reachedFromChildren.Contains(tableId);
    }

    /// <summary>Every item id this table can ever yield, following nested tables.</summary>
    public static IReadOnlySet<string> ItemsReachableFrom(DataStore<LootTableDefinition> tables, string tableId)
    {
        ArgumentNullException.ThrowIfNull(tables);

        var items = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reachedId in TablesReachableFrom(tables, tableId))
        {
            if (!tables.TryGetById(reachedId, out var table))
                continue;
            foreach (var entry in AllEntries(table))
            {
                if (entry.ItemId is { Length: > 0 } itemId)
                    items.Add(itemId);
            }
        }

        return items;
    }

    /// <summary>True when any table in the graph below <paramref name="tableId"/> declares gold.</summary>
    public static bool YieldsGold(DataStore<LootTableDefinition> tables, string tableId)
    {
        ArgumentNullException.ThrowIfNull(tables);

        foreach (var reachedId in TablesReachableFrom(tables, tableId))
        {
            if (tables.TryGetById(reachedId, out var table) && table.Gold is not null)
                return true;
        }

        return false;
    }

    /// <summary>Every entry on a table, across all three drop rules and every draw. The one
    /// place that knows a table has three entry lists, so adding a fourth would break here
    /// loudly rather than silently skipping validation.</summary>
    public static IEnumerable<LootEntryDefinition> AllEntries(LootTableDefinition table)
    {
        ArgumentNullException.ThrowIfNull(table);

        foreach (var entry in table.AlwaysDrops)
            yield return entry;
        foreach (var entry in table.ChanceDrops)
            yield return entry;
        foreach (var draw in table.WeightedDraws)
            foreach (var entry in draw.Entries)
                yield return entry;
    }

    private static IEnumerable<string> NestedTableIds(LootTableDefinition table) =>
        AllEntries(table)
            .Select(entry => entry.TableId)
            .Where(id => !string.IsNullOrEmpty(id))!;

    private static void Walk(DataStore<LootTableDefinition> tables, string tableId, HashSet<string> reached)
    {
        if (string.IsNullOrEmpty(tableId) || !reached.Add(tableId))
            return;
        if (!tables.TryGetById(tableId, out var table))
            return;

        foreach (var nested in NestedTableIds(table))
            Walk(tables, nested, reached);
    }
}
