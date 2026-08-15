using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Professions;

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

    public IReadOnlyList<ItemStack> Inputs { get; init; } = Array.Empty<ItemStack>();
    public IReadOnlyList<ItemStack> Outputs { get; init; } = Array.Empty<ItemStack>();
    public IReadOnlyList<ItemChance> BonusOutputs { get; init; } = Array.Empty<ItemChance>();
}
