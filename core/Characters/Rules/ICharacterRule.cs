namespace Dungeons.Characters.Rules;

/// <summary>A conditional bonus a rule contributes to one attribute.</summary>
public readonly record struct AttributeBonus(AttributeType Attribute, int Amount);

/// <summary>
/// A code-driven rule hook attached to a character by a prefix/suffix/species
/// (via its rule id). Unlike a static numeric modifier, a rule can inspect live
/// character state and change what it contributes as that state changes — this is
/// how suffixes act as "rule breakers" (docs/classes.md §6, docs/architecture.md §18).
/// Milestone 2 rules contribute conditional attribute bonuses; the hook surface
/// widens as combat and other systems arrive.
/// </summary>
public interface ICharacterRule
{
    /// <summary>Stable id matched against a component definition's rule ids.</summary>
    string RuleId { get; }

    /// <summary>Human-readable summary of what the rule does, for tooltips/logs.</summary>
    string Description { get; }

    /// <summary>Attribute bonuses this rule currently grants given <paramref name="snapshot"/>.</summary>
    IEnumerable<AttributeBonus> GetDynamicAttributeBonuses(CharacterSnapshot snapshot);
}
