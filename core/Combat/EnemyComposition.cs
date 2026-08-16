using System.Text.Json.Serialization;
using Dungeons.Characters;
using Dungeons.Content;

namespace Dungeons.Combat;

/// <summary>
/// Physiology layer of the enemy framework (M2′c): what a creature IS — baseline attributes,
/// resource silhouette, biological resistances and Resolve. Never behaviour: that is the role's
/// job, so an Undead Brute and a Goblin Brute can share a brain without sharing a body.
/// </summary>
public sealed class EnemyFamilyDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public AttributeSet Attributes { get; init; }
    public ActorResources Resources { get; init; } = new();

    /// <summary>Per-lane resistance baselines, keyed by <see cref="DamageLanes"/>.</summary>
    public Dictionary<string, double> Resistances { get; init; } = new();

    /// <summary>Per-damage-type multipliers (D-02) where physiology demands them.</summary>
    public Dictionary<string, double> Vulnerable { get; init; } = new();

    public double? Armor { get; init; }
    public double? Resolve { get; init; }
}

/// <summary>
/// Combat-archetype layer: what a creature DOES — attribute/resource deltas over any family,
/// armour, the armoured-physique vulnerability pair, Resolve, and a default AI brain.
/// Family-agnostic on purpose: <c>role.brute</c> is one definition whether the body is goblin,
/// undead or construct.
/// </summary>
public sealed class CombatRoleDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("attribute_tweaks")]
    public AttributeSet AttributeTweaks { get; init; }

    [JsonPropertyName("resource_tweaks")]
    public ResourceDelta ResourceTweaks { get; init; } = new();

    public Dictionary<string, double> Resistances { get; init; } = new();
    public Dictionary<string, double> Vulnerable { get; init; } = new();

    public double? Armor { get; init; }
    public double? Resolve { get; init; }

    /// <summary>Default brain for this role; an actor's own <c>ai_profile</c> overrides it.</summary>
    [JsonPropertyName("ai_profile")]
    public string? AiProfile { get; init; }
}

/// <summary>
/// A named, reusable AI brain: weighted rules over the shared condition vocabulary, matching
/// moves by id or by tag. AI chooses intent only — the tick/action lifecycle resolves timing,
/// exactly as before.
/// </summary>
public sealed class AiProfileDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Weight multiplier applied to whichever move this enemy used last, in [0, 1]. 1 = no
    /// aversion; 0 = never twice in a row. Deterministic — it reshapes weights, not the roll.
    /// </summary>
    [JsonPropertyName("avoid_repeat_weight")]
    public double AvoidRepeatWeight { get; init; } = 1.0;

    public IReadOnlyList<AiRuleSpec> Rules { get; init; } = Array.Empty<AiRuleSpec>();
}
