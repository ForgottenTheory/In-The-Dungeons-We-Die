using System.Text.Json.Serialization;
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

/// <summary>A resource adjustment layered over a baseline (M2′c). Distinct from
/// <see cref="ActorResources"/> because a delta's neutral value is 0, not 1 health.</summary>
public sealed class ResourceDelta
{
    public int Health { get; init; }
    public int Mana { get; init; }
    public int Stamina { get; init; }
}

/// <summary>
/// Data-driven enemy/actor definition (docs/json-schema.md §4). Runtime combat state
/// lives in <see cref="Combatant"/>, never here.
/// </summary>
/// <summary>
/// One weighted AI rule (docs/moves.md §5.2): when the conditions pass, this move is a
/// candidate at this weight. AI chooses intent; the tick engine resolves timing.
/// </summary>
public sealed class AiRuleSpec
{
    /// <summary>All must pass, in the same condition vocabulary rules use. Empty = always.</summary>
    public IReadOnlyList<Dungeons.Rules.ConditionSpec> When { get; init; } =
        Array.Empty<Dungeons.Rules.ConditionSpec>();

    public string Move { get; init; } = string.Empty;

    /// <summary>
    /// Alternative to <see cref="Move"/>: every moveset move carrying this tag is a candidate
    /// at this weight. This is what lets one brain serve many bodies — "prefer the big
    /// telegraphed hit" means <c>mech:stagger</c>, whatever moves this enemy actually has.
    /// Exactly one of <see cref="Move"/>/<see cref="MoveTag"/> must be set (validated).
    /// </summary>
    public string? MoveTag { get; init; }

    public double Weight { get; init; } = 1.0;
}

public sealed class ActorDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>Physiology layer ref (M2′c). Null = a standalone actor authored in full.</summary>
    public string? Family { get; init; }

    /// <summary>Combat-archetype layer ref (M2′c).</summary>
    public string? Role { get; init; }

    /// <summary>AI brain ref; overrides the role's default when set. Inline <see cref="Ai"/>
    /// rules are appended after the referenced profile's — actor-specific flourishes, not a
    /// replacement.</summary>
    [JsonPropertyName("ai_profile")]
    public string? AiProfile { get; init; }

    /// <summary>Absolute attributes — standalone actors only. A layered actor authors
    /// <see cref="AttributeTweaks"/> instead (validated).</summary>
    public AttributeSet Attributes { get; init; }

    /// <summary>Final per-actor attribute delta over family + role.</summary>
    [JsonPropertyName("attribute_tweaks")]
    public AttributeSet AttributeTweaks { get; init; }

    /// <summary>Absolute resources — standalone actors only.</summary>
    public ActorResources Resources { get; init; } = new();

    /// <summary>Final per-actor resource delta over family + role.</summary>
    [JsonPropertyName("resource_tweaks")]
    public ResourceDelta ResourceTweaks { get; init; } = new();

    /// <summary>Flat armour override; when null the role's, then the family's, applies.</summary>
    public double? Armor { get; init; }

    /// <summary>Identity tags, unioned with the family's and role's on resolve.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>The actor's moveset — the same system players use (docs/moves.md §5.2).</summary>
    public IReadOnlyList<MoveGrantSpec> Moves { get; init; } = Array.Empty<MoveGrantSpec>();

    /// <summary>
    /// Weighted move selection. Empty means uniform over <see cref="Moves"/> — which is exactly
    /// what the old <c>_rng.NextInt</c> did, so unprofiled actors behave as before.
    /// </summary>
    public IReadOnlyList<AiRuleSpec> Ai { get; init; } = Array.Empty<AiRuleSpec>();

    /// <summary>Milestone-5 placeholder: a single guaranteed drop on defeat (loot tables come later).</summary>
    public string? LootItemId { get; init; }

    /// <summary>
    /// Per-lane resistance, keyed by <see cref="Dungeons.Combat.DamageLanes"/>. Fractions.
    /// </summary>
    public Dictionary<string, double> Resistances { get; init; } = new();

    /// <summary>
    /// Per-damage-type multiplier: <c>{ "Crushing": 1.25, "Piercing": 0.80 }</c> — a skeleton
    /// takes 25% more crushing and 20% less piercing (D-02).
    ///
    /// <para>This is where "swap to the weapon that counters it" lives now that the three
    /// physical resistances collapsed into one lane. Putting it on the <b>enemy</b> rather than
    /// on player gear keeps the defensive stat count at 8 instead of 10, and makes the counter
    /// <i>discoverable</i> — which is what gives Realm Knowledge's "reveal enemy resistances"
    /// (GDD §11.4) something real to reveal.</para>
    /// </summary>
    public Dictionary<string, double> Vulnerable { get; init; } = new();

    /// <summary>
    /// Control threshold (D-08). Trash 20 / normal 50 / elite 120 / boss 300. One pool for every
    /// control, so a build cannot Stun-lock <b>and</b> Freeze-lock — and each landed control
    /// raises it 25% for the rest of the encounter, which is the fight's CC arc.
    /// Null defers to the role, then the family, then <see cref="CombatTuning.DefaultResolve"/>.
    /// </summary>
    public double? Resolve { get; init; }
}
