using System.Text.Json.Serialization;
using Dungeons.Content;
using Dungeons.Rules;

namespace Dungeons.Combat;

/// <summary>
/// One weighted defensive rule: when the conditions pass, this stance is a candidate at this
/// weight.
///
/// <para>The offensive half of an auto-combat brain is an <see cref="AiRuleSpec"/> — the same
/// type enemies use, deliberately. Defence needs its own shape only because block and dodge are
/// <em>commands</em> rather than moves, so there is no move id to weight. Everything else about
/// it — the <see cref="ConditionSpec"/> vocabulary, the weighting, the deterministic pick — is
/// the same machinery.</para>
/// </summary>
public sealed class DefenceRuleSpec
{
    /// <summary>All must pass, in the shared condition vocabulary. Empty = always.</summary>
    public IReadOnlyList<ConditionSpec> When { get; init; } = Array.Empty<ConditionSpec>();

    /// <summary>Which stance this rule wants. Reuses <see cref="DefensiveStance"/> rather than
    /// declaring a parallel two-member enum — there is one concept here, not two.</summary>
    public DefensiveStance Stance { get; init; } = DefensiveStance.Block;

    public double Weight { get; init; } = 1.0;
}

/// <summary>
/// A brain that plays the player's side (GDD §5.7, docs/damage-and-defense.md §5.1.1).
///
/// <para><b>Auto-combat is the player driven by the same profile shape as an enemy.</b> There is
/// deliberately no second combat resolver: the brain issues the same commands a hand on the
/// keyboard would — <c>UseMove</c>, <c>Block</c>, <c>Dodge</c> — and the encounter resolves move
/// timing, telegraphs, costs, statuses, damage, defences, cooldowns and triggers exactly as it
/// always has.</para>
///
/// <para><b>Its whole disadvantage is <see cref="ReactionTicks"/>, and never a damage penalty</b>
/// (D-07). An agent whose hand is R ticks behind its eye must commit a stance R ticks before
/// impact, and R ticks early is outside every tight window: it blocks reliably (16-tick window),
/// dodges (10), and can never land a Perfect Block (4) or a Parry (3). That is exactly the
/// property GDD §5.3 demands — active play earns its advantage by being present, not by a hidden
/// damage bonus — and it falls out of one number.</para>
///
/// <para><b>Consequence for gear:</b> window-widening affixes are worth <em>more</em> to an
/// automated build than to a skilled player, because they pull tight windows into reach of a
/// slow reaction. A real, discoverable difference between playstyles that cost nothing to
/// create.</para>
/// </summary>
public sealed class AutoCombatProfileDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>How this brain plays, in the player's words. Shown next to the picker.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// How far behind the fight this brain is, in ticks. Validated to stay at or above
    /// <see cref="AutoCombatTuning.MinimumReactionTicks"/>, because a profile quick enough to
    /// parry would quietly repeal D-07.
    /// </summary>
    [JsonPropertyName("reaction_ticks")]
    public int ReactionTicks { get; init; } = AutoCombatTuning.DefaultReactionTicks;

    /// <summary>Weight multiplier on whichever move was used last, in [0, 1] — the same
    /// anti-repeat lever <see cref="AiProfileDefinition"/> carries.</summary>
    [JsonPropertyName("avoid_repeat_weight")]
    public double AvoidRepeatWeight { get; init; } = 1.0;

    /// <summary>What to attack with. The enemy rule type, unchanged.</summary>
    public IReadOnlyList<AiRuleSpec> Rules { get; init; } = Array.Empty<AiRuleSpec>();

    /// <summary>
    /// How to answer an incoming attack. An empty list is a brain that never defends — a legal
    /// and genuinely different playstyle, not an authoring mistake, so it is not a validator
    /// problem.
    /// </summary>
    public IReadOnlyList<DefenceRuleSpec> Defence { get; init; } = Array.Empty<DefenceRuleSpec>();
}

/// <summary>Balance constants for automated play. Small on purpose: the design's whole claim is
/// that automation needs exactly one number to be weaker in the right way.</summary>
public static class AutoCombatTuning
{
    /// <summary>
    /// The shipped reaction latency, in ticks (0.4s at 20 ticks/s). A skilled player reacts in
    /// roughly 2–5; this is deliberately slower than every tight window in the game.
    /// </summary>
    public const int DefaultReactionTicks = 8;

    /// <summary>
    /// The floor a profile's reaction may not go under: one tick clear of the widest tight
    /// window, so no authored brain can reach a Perfect Block or a Parry. Derived from the
    /// windows rather than typed, so retuning a window retunes this with it.
    /// </summary>
    public static readonly int MinimumReactionTicks =
        Math.Max(CombatTuning.PerfectBlockWindowTicks, CombatTuning.ParryWindowTicks) + 1;

    /// <summary>
    /// How often the pilot looks at the fight. It is a brain, not a reflex arc: everything it
    /// does happens on one of these polls, which is also what keeps it deterministic under a
    /// seed. One tick — the cost is a callback per tick, and a coarser poll would add a second,
    /// invisible latency on top of the one the design actually argues for.
    /// </summary>
    public const int DecisionPollTicks = 1;
}
