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

    public double Weight { get; init; } = 1.0;
}

public sealed class ActorDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AttributeSet Attributes { get; init; }
    public ActorResources Resources { get; init; } = new();

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
    /// </summary>
    public double Resolve { get; init; }
}
