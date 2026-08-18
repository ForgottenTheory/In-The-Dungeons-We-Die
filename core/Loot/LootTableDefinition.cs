using Dungeons.Content;

namespace Dungeons.Loot;

/// <summary>
/// Whether an entry may drop here at all. The vocabulary is deliberately tiny — depth, and
/// tags present on the <see cref="LootContext"/> — because everything else a designer might
/// want to condition on can be expressed as a tag the caller puts in the context
/// (<c>elite</c>, <c>boss</c>, <c>active</c>, <c>realm.dark_forest</c>, <c>source:beast</c>).
/// A larger condition language would duplicate the rule engine's for no gain.
/// </summary>
public sealed class LootCondition
{
    /// <summary>Realm depth floor. 0 means "no floor", which is also the Hideout's depth.</summary>
    public int MinDepth { get; init; }

    /// <summary>Realm depth ceiling. Null means "no ceiling".</summary>
    public int? MaxDepth { get; init; }

    /// <summary>Every one of these must be on the context.</summary>
    public IReadOnlyList<string> RequiresTags { get; init; } = Array.Empty<string>();

    /// <summary>None of these may be on the context.</summary>
    public IReadOnlyList<string> ExcludesTags { get; init; } = Array.Empty<string>();

    public bool IsSatisfiedBy(LootContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Depth < MinDepth)
            return false;
        if (MaxDepth is { } ceiling && context.Depth > ceiling)
            return false;

        foreach (var required in RequiresTags)
            if (!context.HasTag(required))
                return false;

        foreach (var excluded in ExcludesTags)
            if (context.HasTag(excluded))
                return false;

        return true;
    }
}

/// <summary>
/// One line of a loot table: an item, a nested table, or a deliberate miss. Exactly one of
/// <see cref="ItemId"/> / <see cref="TableId"/> / <see cref="DropsNothing"/> is set, which
/// validation enforces.
///
/// <para>The same type serves all three drop rules on a table. <see cref="Chance"/> is read
/// only inside <c>chanceDrops</c>; <see cref="Weight"/> only inside a
/// <see cref="LootDrawDefinition"/>; neither is read for a guaranteed drop. Sharing one entry
/// shape means a designer moving a line between the three lists never has to rewrite it.</para>
/// </summary>
public sealed class LootEntryDefinition
{
    /// <summary>The item this entry yields — a material, consumable or technique id.</summary>
    public string? ItemId { get; init; }

    /// <summary>A nested table rolled in full when this entry is selected. This is how shared
    /// tables compose: a goblin's table draws from the same <c>loot.shared.salvage_light</c>
    /// every other scavenger does.</summary>
    public string? TableId { get; init; }

    /// <summary>A weighted miss. Only meaningful inside a draw, where it is what makes
    /// "one pick from this table, and sometimes you get nothing" expressible.</summary>
    public bool DropsNothing { get; init; }

    /// <summary>Relative weight inside a <see cref="LootDrawDefinition"/>. Must be positive.</summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>Independent drop probability in [0, 1], read only for <c>chanceDrops</c>.</summary>
    public double Chance { get; init; } = 1.0;

    /// <summary>Quantity range, inclusive. Both default to 1, so a plain entry drops one.</summary>
    public int MinQuantity { get; init; } = 1;
    public int MaxQuantity { get; init; } = 1;

    /// <summary>When this entry is allowed to drop at all. Null = always.</summary>
    public LootCondition? When { get; init; }

    /// <summary>
    /// Rarity for an item that carries no <c>rarity:</c> tag of its own — techniques,
    /// schematics, consumables. Declaring one for a material is a validation error: the
    /// material's tag is the single source of truth.
    /// </summary>
    public LootRarity? Rarity { get; init; }
}

/// <summary>
/// A weighted draw: pick <see cref="Picks"/> entries from <see cref="Entries"/>, each pick
/// independent and weighted. This is the "one drop from this pool" shape — the thing that makes
/// a table produce *variety* rather than a checklist.
/// </summary>
public sealed class LootDrawDefinition
{
    /// <summary>How many entries are drawn. Each pick rolls the whole weighted set again, so a
    /// two-pick draw can select the same entry twice — quantities merge in the result.</summary>
    public int Picks { get; init; } = 1;

    public IReadOnlyList<LootEntryDefinition> Entries { get; init; } = Array.Empty<LootEntryDefinition>();

    /// <summary>When this draw happens at all. Null = always.</summary>
    public LootCondition? When { get; init; }
}

/// <summary>Coin carried by a source. Rolled once per table.</summary>
public sealed class GoldDropDefinition
{
    public int MinAmount { get; init; }
    public int MaxAmount { get; init; }

    /// <summary>Probability in [0, 1] that any coin is carried at all.</summary>
    public double Chance { get; init; } = 1.0;

    /// <summary>When coin drops at all. Null = always.</summary>
    public LootCondition? When { get; init; }
}

/// <summary>
/// A data-driven drop table — the one shape every loot source in the game shares, whether the
/// source is a defeated enemy, a gathering node, a chest or a profession action
/// (docs/realms.md, DECISIONS D28).
///
/// <para><b>The three drop rules are separate lists on purpose.</b> "Always", "each rolls its
/// own chance" and "pick N by weight" are genuinely different mechanics, and naming them in the
/// JSON is what lets a table be read at a glance instead of decoded from a kind field.</para>
///
/// <para><b>Composition, not duplication.</b> An entry may point at another table
/// (<see cref="LootEntryDefinition.TableId"/>), so shared libraries — creature remains, light
/// salvage, forest reagents — are authored once and reached from everywhere. An enemy that does
/// not exist yet becomes lootable by pointing one <c>lootTableId</c> at the shared tables that
/// already ship; no code changes, and none of its drops need to be re-authored.</para>
///
/// <para><b>Source identity.</b> <see cref="Tags"/> are added to the <see cref="LootContext"/>
/// for the duration of this table and everything nested beneath it, so a shared table can ask
/// where it was reached from (<c>source:beast</c> unlocks anatomy that <c>source:construct</c>
/// must not yield).</para>
/// </summary>
public sealed class LootTableDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>Identity this table contributes to the context while it is being rolled.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>Entries that always drop when this table is rolled (quantity still rolls).</summary>
    public IReadOnlyList<LootEntryDefinition> AlwaysDrops { get; init; } = Array.Empty<LootEntryDefinition>();

    /// <summary>Entries that each roll their own <see cref="LootEntryDefinition.Chance"/>,
    /// independently of one another.</summary>
    public IReadOnlyList<LootEntryDefinition> ChanceDrops { get; init; } = Array.Empty<LootEntryDefinition>();

    /// <summary>Weighted draws — the variety layer.</summary>
    public IReadOnlyList<LootDrawDefinition> WeightedDraws { get; init; } = Array.Empty<LootDrawDefinition>();

    /// <summary>Coin, if this source carries any.</summary>
    public GoldDropDefinition? Gold { get; init; }
}
