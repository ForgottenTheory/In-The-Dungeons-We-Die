using Dungeons.Content;

namespace Dungeons.Combat;

/// <summary>
/// A learnable technique item — a grimoire, manual or scroll that teaches one move into the
/// character's learned list (M2′ acquisition v1; DECISIONS D25: moves are universal, and
/// acquisition is how a build reaches the library). The <see cref="Id"/> matches the stackable
/// inventory item id it is consumed from, like <see cref="ConsumableDefinition"/>.
/// </summary>
public sealed class TechniqueDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>The move this technique teaches. Must resolve to a shipped move at load.</summary>
    public string Teaches { get; init; } = string.Empty;
}
