using System.Text.Json.Serialization;
using Dungeons.Content;

namespace Dungeons.Professions;

/// <summary>
/// The six quantities profession work can be improved in. A closed vocabulary, like
/// <c>RealmLocationType</c> — each member has exactly one consumer in the execution path, and
/// adding one means writing that consumer.
///
/// <para><b>Not "what mastery buys" — what anything buys.</b> Mastery was the first source
/// (<see cref="MasteryBenefitDefinition"/>) and is no longer the only one: Phase 10 added
/// cross-profession and global <see cref="ProfessionSynergyDefinition"/>s that pay into the
/// same six. <see cref="ProfessionBenefits"/> is where the sources meet, which is why nothing
/// downstream had to learn a second vocabulary.</para>
///
/// <para><b>These names match the <c>profession.*</c> modifier keys</b>
/// (<c>profession.interval.mult</c>, <c>preserve.chance</c>, <c>double.chance</c>, …) on purpose.
/// Those keys are declared and unread; they belong to E6's yield pipeline, where worn tools will
/// feed the same six quantities. Naming them the same thing now is what lets E6 merge tools in
/// as a third source instead of renaming a vocabulary the save and the content already carry.
/// <b>The member names are persistent identifiers</b> — they are the JSON <c>kind</c> values.</para>
/// </summary>
public enum ProfessionBenefitKind
{
    /// <summary>Fraction taken off the action's interval. The work gets quicker.</summary>
    IntervalReduction,

    /// <summary>Chance the action's inputs survive being used. The hand that wastes nothing.</summary>
    InputPreservation,

    /// <summary>Chance each primary output comes out twice.</summary>
    OutputDoubling,

    /// <summary>Added to every bonus-output chance the action authors — the rare-find lever.</summary>
    BonusOutputChance,

    /// <summary>Added to an opportunity's chance of being noticed at all (active play only).</summary>
    OpportunityChance,

    /// <summary>Fraction of a pursued opportunity's risk that experience talks down.</summary>
    OpportunityRisk,
}

/// <summary>
/// One rung of the mastery ladder: what a benefit is worth per mastery level, where it starts,
/// and how far it goes.
///
/// <para><b>One shared ladder, not per-action authoring.</b> 659 actions ship; a mastery block on
/// each would be 659 places for a balance pass to miss one. A row with no
/// <see cref="ProfessionId"/> applies to every profession, and a row naming one <b>replaces</b>
/// the general row for that profession — the same "later layer wins per key" rule the enemy fold
/// uses, so a future pass can make Mining preserve better than Fishing without a code change.</para>
/// </summary>
public sealed class MasteryBenefitDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;

    public ProfessionBenefitKind Kind { get; init; }

    /// <summary>The profession this row is scoped to, or null for every profession.</summary>
    [JsonPropertyName("profession")]
    public string? ProfessionId { get; init; }

    /// <summary>
    /// The mastery level this benefit switches on at. Below it the benefit is worth <b>nothing</b>
    /// — which is what makes preservation and doubling <em>unlocks</em> rather than yet another
    /// number creeping up from zero.
    /// </summary>
    [JsonPropertyName("unlock_level")]
    public int UnlockLevel { get; init; } = 1;

    /// <summary>Value gained per mastery level at or above <see cref="UnlockLevel"/>.</summary>
    [JsonPropertyName("per_level")]
    public double PerLevel { get; init; }

    /// <summary>The ceiling. Reached before level 99 for most rows, which is deliberate: the last
    /// levels of an action should be about having mastered it, not about the last 2%.</summary>
    public double Max { get; init; }

    /// <summary>What this rung means, for the ladder the player reads.</summary>
    public string Description { get; init; } = string.Empty;
}
