namespace Dungeons.Items;

/// <summary>
/// A quantity of a stackable item, identified by its definition id. Used both for
/// inventory contents and for profession action inputs/outputs.
/// </summary>
public readonly record struct ItemStack(string ItemId, int Quantity);
