using Dungeons.Characters.Modifiers;
using Dungeons.Content;

namespace Dungeons.Characters.Composition;

/// <summary>
/// Shared shape of the four data-driven identity components (Species, Base Class,
/// Prefix, Suffix). Each contributes numeric modifiers, descriptive tags, ability
/// ids, and rule-hook ids. Rule-breaking behaviour lives in code keyed by
/// <see cref="RuleIds"/>, not in the JSON (docs/json-schema.md §14–15).
/// </summary>
public abstract class CharacterComponentDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ModifierData> Modifiers { get; init; } = Array.Empty<ModifierData>();
    public IReadOnlyList<string> AbilityIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RuleIds { get; init; } = Array.Empty<string>();
}

/// <summary>Fundamental biological/metaphysical identity (docs/classes.md §2).</summary>
public sealed class SpeciesDefinition : CharacterComponentDefinition
{
}

/// <summary>Modifies how the base class operates (docs/classes.md §4).</summary>
public sealed class PrefixDefinition : CharacterComponentDefinition
{
}

/// <summary>Rule-breaking identity component (docs/classes.md §6).</summary>
public sealed class SuffixDefinition : CharacterComponentDefinition
{
}

/// <summary>The core combat chassis; also declares the primary resource it runs on (docs/classes.md §3).</summary>
public sealed class BaseClassDefinition : CharacterComponentDefinition
{
    public ResourceType PrimaryResource { get; init; } = ResourceType.Stamina;
}
