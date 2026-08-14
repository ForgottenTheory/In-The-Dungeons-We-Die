using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Professions;

/// <summary>A fixed quantity of an item (an action input or guaranteed output).</summary>
public sealed class ItemAmountData
{
    public string ItemId { get; init; } = string.Empty;
    public int Quantity { get; init; } = 1;

    public ItemStack ToStack() => new(ItemId, Quantity);
}

/// <summary>A chance-based bonus output (docs/json-schema.md §9).</summary>
public sealed class ItemChanceData
{
    public string ItemId { get; init; } = string.Empty;
    public double Chance { get; init; }
    public int Quantity { get; init; } = 1;

    public ItemStack ToStack() => new(ItemId, Quantity);
}

/// <summary>
/// Data-driven profession activity: gathering (no inputs) or processing/crafting
/// (with inputs). Passive and active execution share this one definition
/// (docs/json-schema.md §9, docs/architecture.md §20).
/// </summary>
public sealed class ProfessionActionDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string ProfessionId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int RequiredLevel { get; init; } = 1;
    public int BaseIntervalTicks { get; init; } = 100;
    public long Experience { get; init; }

    public IReadOnlyList<ItemAmountData> Inputs { get; init; } = Array.Empty<ItemAmountData>();
    public IReadOnlyList<ItemAmountData> Outputs { get; init; } = Array.Empty<ItemAmountData>();
    public IReadOnlyList<ItemChanceData> BonusOutputs { get; init; } = Array.Empty<ItemChanceData>();
}
