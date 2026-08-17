namespace Dungeons.Simulation;

/// <summary>
/// A unit of work registered with the <see cref="TickEngine"/> to resolve at a
/// specific simulation tick. This is the low-level scheduling primitive only — it
/// knows nothing about combat, and deliberately never will. Higher-level concepts
/// (telegraph/windup/recovery, actor, action kind) live in
/// <c>Dungeons.Combat.ActionInFlight</c>, which schedules onto this.
/// </summary>
public sealed class ScheduledAction
{
    internal ScheduledAction(long id, long resolveTick, long sequence, Action callback)
    {
        Id = id;
        ResolveTick = resolveTick;
        Sequence = sequence;
        Callback = callback;
    }

    /// <summary>Stable identifier used to cancel this action.</summary>
    public long Id { get; }

    /// <summary>Absolute tick at which the action resolves.</summary>
    public long ResolveTick { get; }

    /// <summary>
    /// Monotonic schedule order. Used to break ties deterministically when
    /// multiple actions resolve on the same tick.
    /// </summary>
    public long Sequence { get; }

    /// <summary>The work performed when the action resolves.</summary>
    public Action Callback { get; }
}
