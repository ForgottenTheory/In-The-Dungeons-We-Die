using Dungeons.Characters;
using Dungeons.Content;

namespace Dungeons.Professions;

public enum ProfessionCategory
{
    Gathering,
    Crafting,
    Utility,
}

/// <summary>
/// Data-driven definition of a profession (docs/json-schema.md §8). Progression
/// state (level, xp, mastery) is runtime data and lives in
/// <see cref="ProfessionProgress"/>, never here.
/// </summary>
public sealed class ProfessionDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ProfessionCategory Category { get; init; } = ProfessionCategory.Gathering;
    public IReadOnlyList<AttributeType> PrimaryAttributes { get; init; } = Array.Empty<AttributeType>();
}
