namespace Dungeons.Items;

/// <summary>
/// A quantity of a stackable item, identified by its definition id. This is the single
/// item+quantity representation across the game — inventory contents, crafting inputs,
/// and profession action inputs/outputs all use it (see <see cref="ItemChance"/> for the
/// chance-based variant). <see cref="Quantity"/> defaults to 1 so content JSON may omit it.
/// </summary>
public readonly record struct ItemStack(string ItemId, int Quantity = 1);
