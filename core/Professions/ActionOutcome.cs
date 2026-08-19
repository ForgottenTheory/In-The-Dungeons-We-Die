using Dungeons.Items;

namespace Dungeons.Professions;

/// <summary>Why an action could not be executed.</summary>
public enum ActionFailure
{
    None,
    UnknownAction,
    LevelTooLow,
    MissingInputs,
}

/// <summary>Why a pursued opportunity paid nothing.</summary>
public enum OpportunityFailure
{
    None,
    UnknownAction,
    UnknownOpportunity,
    MissingInputs,

    /// <summary>The gamble was taken and lost: the time was spent, the payoff was not earned.</summary>
    RiskRealised,
}

/// <summary>
/// The authoritative result of attempting a profession action. Passive and active
/// execution both produce this; the UI reads it for the event log and feedback.
/// </summary>
public sealed class ActionOutcome
{
    public required string ActionId { get; init; }

    /// <summary>Whether the attempt was legal and ran at all. An attempt that ran but missed
    /// its success roll is still a success here — see <see cref="AttemptMissed"/>.</summary>
    public required bool Success { get; init; }

    public ActionFailure Failure { get; init; } = ActionFailure.None;

    /// <summary>True when the attempt ran and its success roll missed: the prey bolted, the
    /// mark looked up. Inputs are still consumed and reduced XP is still granted.</summary>
    public bool AttemptMissed { get; init; }

    public IReadOnlyList<ItemStack> Consumed { get; init; } = Array.Empty<ItemStack>();
    public IReadOnlyList<ItemStack> Produced { get; init; } = Array.Empty<ItemStack>();

    /// <summary>True when mastery saved the inputs: <see cref="Consumed"/> is empty and the
    /// materials are still in the bag. Reported so the log can say what mastery just did —
    /// a benefit the player never sees fire is a benefit they do not believe in.</summary>
    public bool InputsPreserved { get; init; }

    /// <summary>How many primary outputs mastery made come out twice.</summary>
    public int OutputsDoubled { get; init; }

    public long XpGained { get; init; }
    public int MasteryGained { get; init; }
    public double Performance { get; init; }
    public bool WasActive { get; init; }

    /// <summary>Realm Knowledge this action taught, if it is a survey action (Cartography).
    /// Core reports it; the client applies it, because Core has no realm-progress state.</summary>
    public RealmKnowledgeGain? RealmKnowledgeGained { get; init; }

    /// <summary>An opportunity the attempt surfaced, awaiting a pursue/ignore decision.
    /// Null on every passive attempt, by construction.</summary>
    public ProfessionOpportunityDefinition? DiscoveredOpportunity { get; init; }

    public static ActionOutcome Failed(string actionId, ActionFailure failure) =>
        new() { ActionId = actionId, Success = false, Failure = failure };
}

/// <summary>The authoritative result of pursuing a discovered opportunity.</summary>
public sealed class OpportunityOutcome
{
    public required string ActionId { get; init; }
    public required string OpportunityId { get; init; }
    public required bool Success { get; init; }
    public OpportunityFailure Failure { get; init; } = OpportunityFailure.None;

    public IReadOnlyList<ItemStack> Consumed { get; init; } = Array.Empty<ItemStack>();
    public IReadOnlyList<ItemStack> Produced { get; init; } = Array.Empty<ItemStack>();
    public long XpGained { get; init; }

    public static OpportunityOutcome Failed(string actionId, string opportunityId, OpportunityFailure failure) =>
        new() { ActionId = actionId, OpportunityId = opportunityId, Success = false, Failure = failure };
}
