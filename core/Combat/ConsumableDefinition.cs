using Dungeons.Content;

namespace Dungeons.Combat;

/// <summary>
/// A data-driven usable item. Milestone 9 supports a single Heal effect — enough to
/// make gathered/crafted supplies matter for Realm survival (docs/vertical-slice.md
/// §13). The <see cref="Id"/> matches the inventory item id it is consumed from.
/// </summary>
public sealed class ConsumableDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int HealAmount { get; init; }
}
