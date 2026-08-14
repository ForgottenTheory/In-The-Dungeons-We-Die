using Dungeons.Characters;
using Dungeons.Content;

namespace Dungeons.Combat;

/// <summary>Base resource maxima for an enemy actor.</summary>
public sealed class ActorResources
{
    public int Health { get; init; } = 1;
    public int Mana { get; init; }
    public int Stamina { get; init; }
}

/// <summary>
/// Data-driven enemy/actor definition (docs/json-schema.md §4). Runtime combat state
/// lives in <see cref="Combatant"/>, never here.
/// </summary>
public sealed class ActorDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AttributeSet Attributes { get; init; }
    public ActorResources Resources { get; init; } = new();
    public IReadOnlyList<string> AbilityIds { get; init; } = Array.Empty<string>();

    /// <summary>Milestone-5 placeholder: a single guaranteed drop on defeat (loot tables come later).</summary>
    public string? LootItemId { get; init; }
}
