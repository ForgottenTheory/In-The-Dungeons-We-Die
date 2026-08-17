using Dungeons.Characters;
using Dungeons.Content;

namespace Dungeons.Professions;

/// <summary>
/// The three shelves the roster sorts onto. <c>Processing</c> is deliberately not called
/// "Crafting": that word already names the material-transformation bench
/// (<c>Dungeons.Crafting</c>), and one name per concept is a project rule.
/// </summary>
public enum ProfessionCategory
{
    /// <summary>Pulls raw material out of the world.</summary>
    Gathering,

    /// <summary>Turns what was gathered into something another profession can use.</summary>
    Processing,

    /// <summary>Pays out in knowledge, access or opportunity rather than material.</summary>
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

    /// <summary>One line naming what this profession is <em>for</em>. Twenty XP bars are only
    /// distinguishable if each one can say what it does that its neighbours do not — Hunting
    /// finds the creature, Beast Lore reads it.</summary>
    public string Description { get; init; } = string.Empty;
}
