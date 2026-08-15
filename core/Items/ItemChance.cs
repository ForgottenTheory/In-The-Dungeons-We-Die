namespace Dungeons.Items;

/// <summary>
/// A chance-gated <see cref="ItemStack"/>: the same item+quantity plus the probability
/// [0,1] that it drops. Built on <see cref="ItemStack"/> so there is one item+quantity
/// shape rather than a separate parallel type — expose <see cref="Stack"/> to get the
/// plain quantity. Used for profession action bonus outputs; the flat JSON shape is
/// <c>{ "itemId": ..., "chance": ..., "quantity": ... }</c> (quantity optional).
/// </summary>
public readonly record struct ItemChance(string ItemId, double Chance, int Quantity = 1)
{
    /// <summary>The item+quantity this chance yields when it rolls.</summary>
    public ItemStack Stack => new(ItemId, Quantity);
}
