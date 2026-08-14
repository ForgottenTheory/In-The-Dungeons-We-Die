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

/// <summary>
/// The authoritative result of attempting a profession action. Passive and active
/// execution both produce this; the UI reads it for the event log and feedback.
/// </summary>
public sealed class ActionOutcome
{
    public required string ActionId { get; init; }
    public required bool Success { get; init; }
    public ActionFailure Failure { get; init; } = ActionFailure.None;

    public IReadOnlyList<ItemStack> Consumed { get; init; } = Array.Empty<ItemStack>();
    public IReadOnlyList<ItemStack> Produced { get; init; } = Array.Empty<ItemStack>();

    public long XpGained { get; init; }
    public int MasteryGained { get; init; }
    public double Performance { get; init; }
    public bool WasActive { get; init; }

    public static ActionOutcome Failed(string actionId, ActionFailure failure) =>
        new() { ActionId = actionId, Success = false, Failure = failure };
}
