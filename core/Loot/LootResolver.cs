using Dungeons.Content;
using Dungeons.Randomness;

namespace Dungeons.Loot;

/// <summary>
/// Rolls loot tables. The single authority on what a source drops — enemies, gathering nodes,
/// chests and profession actions all come through here, so "how loot works" is one readable
/// method rather than a rule per source (docs/architecture.md §3 Loot).
///
/// <para>Pure and seeded: given the same table, context and <see cref="IRandomSource"/> it
/// produces the same result, which is what makes a drop reproducible in a test and would make
/// it reproducible on a server later.</para>
///
/// <para>Rarity is <em>read</em>, never authored twice: a dropped material's own
/// <c>rarity:</c> tag decides, and only items without one (techniques, schematics) fall back to
/// the entry's declared rarity.</para>
/// </summary>
public sealed class LootResolver
{
    private readonly DataStore<LootTableDefinition> _tables;
    private readonly DataStore<MaterialDefinition> _materials;
    private readonly IRandomSource _random;

    public LootResolver(ContentBundle content, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(content);
        _tables = content.LootTables;
        _materials = content.Materials;
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>True when a table id is authored. Callers hold ids from content that has
    /// already been validated, so this is for defensive wiring rather than normal flow.</summary>
    public bool HasTable(string tableId) => _tables.Contains(tableId);

    /// <summary>Rolls one table. Unknown ids yield nothing rather than throwing — a drop is
    /// never worth crashing a fight over, and content validation has already caught the typo.</summary>
    public LootResult Roll(string? tableId, LootContext context)
    {
        if (string.IsNullOrEmpty(tableId))
            return LootResult.Empty;
        return Roll(new[] { tableId }, context);
    }

    /// <summary>
    /// Rolls several tables into one result — the composed-enemy case, where a kill draws from
    /// its family's table, its role's table and its own, and the player sees a single haul.
    /// </summary>
    public LootResult Roll(IReadOnlyList<string> tableIds, LootContext context)
    {
        ArgumentNullException.ThrowIfNull(tableIds);
        ArgumentNullException.ThrowIfNull(context);

        var accumulated = new List<LootDrop>();
        long gold = 0;

        foreach (var tableId in tableIds)
        {
            if (string.IsNullOrEmpty(tableId) || !_tables.TryGetById(tableId, out var table))
                continue;
            RollTable(table, context, accumulated, ref gold, nesting: 0);
        }

        return new LootResult { Drops = MergeAndOrder(accumulated), Gold = gold };
    }

    private void RollTable(
        LootTableDefinition table,
        LootContext outerContext,
        List<LootDrop> accumulated,
        ref long gold,
        int nesting)
    {
        if (nesting >= LootTuning.MaxNestingDepth)
            return;

        // The table's own identity joins the circumstances, so anything nested below can ask
        // what reached it. This is what lets one shared anatomy table serve every creature.
        var context = table.Tags.Count == 0 ? outerContext : outerContext.With(table.Tags);

        foreach (var entry in table.AlwaysDrops)
        {
            if (IsAllowed(entry.When, context))
                Yield(entry, context, accumulated, ref gold, nesting);
        }

        foreach (var entry in table.ChanceDrops)
        {
            if (!IsAllowed(entry.When, context))
                continue;
            if (entry.Chance < 1.0 && _random.NextDouble() >= entry.Chance)
                continue;
            Yield(entry, context, accumulated, ref gold, nesting);
        }

        foreach (var draw in table.WeightedDraws)
        {
            if (!IsAllowed(draw.When, context))
                continue;

            var eligible = draw.Entries.Where(entry => IsAllowed(entry.When, context)).ToList();
            if (eligible.Count == 0)
                continue;

            var picks = Math.Clamp(draw.Picks, 0, LootTuning.MaxPicksPerDraw);
            for (var pick = 0; pick < picks; pick++)
            {
                var chosen = PickWeighted(eligible);
                if (chosen is not null)
                    Yield(chosen, context, accumulated, ref gold, nesting);
            }
        }

        if (table.Gold is { } coin && IsAllowed(coin.When, context))
        {
            if (coin.Chance >= 1.0 || _random.NextDouble() < coin.Chance)
                gold += RollAmount(coin.MinAmount, coin.MaxAmount);
        }
    }

    /// <summary>Turns one selected entry into either a drop or a descent into a nested table.</summary>
    private void Yield(
        LootEntryDefinition entry,
        LootContext context,
        List<LootDrop> accumulated,
        ref long gold,
        int nesting)
    {
        if (entry.DropsNothing)
            return;

        if (entry.TableId is { Length: > 0 } nestedId)
        {
            if (_tables.TryGetById(nestedId, out var nested))
                RollTable(nested, context, accumulated, ref gold, nesting + 1);
            return;
        }

        if (entry.ItemId is not { Length: > 0 } itemId)
            return;

        var quantity = RollAmount(entry.MinQuantity, entry.MaxQuantity);
        if (quantity <= 0)
            return;

        accumulated.Add(new LootDrop(itemId, quantity, RarityOf(itemId, entry)));
    }

    private static bool IsAllowed(LootCondition? condition, LootContext context) =>
        condition is null || condition.IsSatisfiedBy(context);

    private LootEntryDefinition? PickWeighted(IReadOnlyList<LootEntryDefinition> eligible)
    {
        var totalWeight = eligible.Sum(entry => Math.Max(0.0, entry.Weight));
        if (totalWeight <= 0)
            return null;

        var roll = _random.NextDouble() * totalWeight;
        foreach (var entry in eligible)
        {
            roll -= Math.Max(0.0, entry.Weight);
            if (roll < 0)
                return entry;
        }

        return eligible[^1]; // floating-point tail: the last eligible entry is the honest answer
    }

    private int RollAmount(int min, int max)
    {
        var low = Math.Min(min, max);
        var high = Math.Max(min, max);
        return low == high ? low : _random.NextInt(low, high + 1);
    }

    /// <summary>The material's own <c>rarity:</c> tag wins; an entry may only declare a rarity
    /// for items that have no tag to read (validation enforces the other direction).</summary>
    private LootRarity RarityOf(string itemId, LootEntryDefinition entry)
    {
        if (_materials.TryGetById(itemId, out var material))
        {
            foreach (var tag in material.Tags)
            {
                if (TagFamilies.TryParse(tag, out var family, out var value)
                    && string.Equals(family, TagFamilies.Rarity.Name, StringComparison.OrdinalIgnoreCase)
                    && LootRarities.TryParseTagValue(value, out var tagged))
                    return tagged;
            }
        }

        return entry.Rarity ?? LootRarity.Common;
    }

    /// <summary>Merges duplicate item ids and orders rarest first, then by id so the same roll
    /// always reads the same way.</summary>
    private static IReadOnlyList<LootDrop> MergeAndOrder(IReadOnlyList<LootDrop> drops)
    {
        if (drops.Count == 0)
            return Array.Empty<LootDrop>();

        var merged = new Dictionary<string, LootDrop>(StringComparer.Ordinal);
        foreach (var drop in drops)
        {
            merged[drop.ItemId] = merged.TryGetValue(drop.ItemId, out var existing)
                ? existing with { Quantity = existing.Quantity + drop.Quantity }
                : drop;
        }

        return merged.Values
            .OrderByDescending(drop => drop.Rarity)
            .ThenBy(drop => drop.ItemId, StringComparer.Ordinal)
            .ToList();
    }
}
