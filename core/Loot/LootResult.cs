using Dungeons.Items;

namespace Dungeons.Loot;

/// <summary>One thing that dropped, with the rarity the UI needs to make a rare find feel rare.</summary>
public readonly record struct LootDrop(string ItemId, int Quantity, LootRarity Rarity)
{
    /// <summary>The item+quantity, in the game's one item+quantity shape.</summary>
    public ItemStack Stack => new(ItemId, Quantity);
}

/// <summary>
/// Everything one roll of a loot table produced. Drops are merged by item id — three separate
/// entries yielding scrap iron arrive as one line, because a player reads "+5 Scrap Iron", not
/// three log entries — and ordered rarest first, so the interesting result is never buried.
/// </summary>
public sealed class LootResult
{
    public static readonly LootResult Empty = new()
    {
        Drops = Array.Empty<LootDrop>(),
        Gold = 0,
    };

    public required IReadOnlyList<LootDrop> Drops { get; init; }
    public required long Gold { get; init; }

    public bool IsEmpty => Drops.Count == 0 && Gold <= 0;

    /// <summary>The rarest thing in this result, or null if nothing dropped. What a "you found
    /// something good" flourish should key off.</summary>
    public LootRarity? Best => Drops.Count == 0 ? null : Drops.Max(d => d.Rarity);

    /// <summary>Banks this result into a bag: stacks plus coin, in one call, so every loot
    /// source deposits identically. Which bag it is — the Stash or the unsecured run
    /// inventory — is the caller's business and is what carries the extraction risk model.</summary>
    public void DepositInto(Inventory bag)
    {
        ArgumentNullException.ThrowIfNull(bag);
        foreach (var drop in Drops)
            bag.Add(drop.ItemId, drop.Quantity);
        if (Gold > 0)
            bag.AddGold(Gold);
    }
}
