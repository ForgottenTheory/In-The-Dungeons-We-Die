using Dungeons.Characters.Rules;

namespace Dungeons.Characters.Composition;

/// <summary>
/// The fully-resolved, immutable result of composing a <see cref="CharacterBuild"/>:
/// static attributes, derived resource maxima, aggregated tags/abilities, the
/// primary resource, and the resolved rule hooks. A runtime <see cref="Character"/>
/// is created from a blueprint (definitions vs runtime state — architecture.md §8).
/// </summary>
public sealed class CharacterBlueprint
{
    public required CharacterBuild Build { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>Static attributes before any dynamic rule bonuses are applied.</summary>
    public required AttributeSet BaseAttributes { get; init; }

    public required int MaxHealth { get; init; }
    public required int MaxMana { get; init; }
    public required int MaxStamina { get; init; }

    public required ResourceType PrimaryResource { get; init; }

    public required IReadOnlySet<string> Tags { get; init; }

    /// <summary>Move grants from every component, each with its provenance (E4).</summary>
    public required IReadOnlyList<Dungeons.Combat.MoveGrant> MoveGrants { get; init; }

    /// <summary>Move-modifier ids from every component, with provenance.</summary>
    public required IReadOnlyList<(string ModifierId, string Source)> MoveModifierGrants { get; init; }

    public required IReadOnlyList<ICharacterRule> Rules { get; init; }
}
