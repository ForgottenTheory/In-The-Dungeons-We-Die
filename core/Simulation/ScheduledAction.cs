namespace Dungeons.Simulation;

/// <summary>
/// A unit of work registered with the <see cref="TickEngine"/> to resolve at a
/// specific simulation tick. This is the low-level scheduling primitive only —
/// combat-specific concepts (telegraph/windup/recovery, actor, action type) are
/// layered on top in later milestones and deliberately not modelled here yet.
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
