namespace Dungeons.Simulation;

/// <summary>
/// Deterministic, engine-independent simulation clock. All authoritative game
/// timing is expressed in integer ticks; Godot converts ticks into seconds for
/// presentation. The engine advances one tick at a time, resolving any actions
/// due on the newly reached tick in a stable, reproducible order.
/// </summary>
/// <remarks>
/// This type contains no Godot dependency and is fully unit-testable. It is the
/// shared timing foundation for combat, gathering, crafting and idle progression
/// (see docs/architecture.md §11 and docs/combat-spec.md §3).
/// </remarks>
public sealed class TickEngine
{
    private readonly List<ScheduledAction> _pending = new();
    private long _nextId = 1;
    private long _nextSequence = 1;

    /// <summary>The current absolute simulation tick. Starts at zero.</summary>
    public long CurrentTick { get; private set; }

    /// <summary>Number of actions still waiting to resolve.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// Raised once per advanced tick, after any actions due on that tick have
    /// resolved. The argument is the new <see cref="CurrentTick"/> value.
    /// </summary>
    public event Action<long>? TickAdvanced;

    /// <summary>
    /// Registers <paramref name="callback"/> to run <paramref name="delayTicks"/>
    /// ticks in the future (relative to <see cref="CurrentTick"/>).
    /// </summary>
    /// <returns>The scheduled action; its <see cref="ScheduledAction.Id"/> can be passed to <see cref="Cancel"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="delayTicks"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException">If <paramref name="callback"/> is null.</exception>
    public ScheduledAction Schedule(long delayTicks, Action callback)
    {
        if (delayTicks < 1)
            throw new ArgumentOutOfRangeException(nameof(delayTicks), delayTicks, "Actions must be scheduled at least one tick into the future.");
        ArgumentNullException.ThrowIfNull(callback);

        var action = new ScheduledAction(_nextId++, CurrentTick + delayTicks, _nextSequence++, callback);
        _pending.Add(action);
        return action;
    }

    /// <summary>
    /// Cancels a pending action by id. Safe to call from within a resolving
    /// callback — note that actions already due on the current tick are resolved
    /// atomically, so a callback can only cancel actions on <em>later</em> ticks.
    /// Returns true if an action was removed.
    /// </summary>
    public bool Cancel(long id)
    {
        for (var i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Id == id)
            {
                _pending.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Advances the simulation by <paramref name="ticks"/> ticks, resolving due
    /// actions one tick at a time so ordering is deterministic and callbacks
    /// scheduled during resolution are honoured on their own future tick.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="ticks"/> is negative.</exception>
    public void Advance(long ticks = 1)
    {
        if (ticks < 0)
            throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "Cannot advance a negative number of ticks.");

        for (long step = 0; step < ticks; step++)
        {
            CurrentTick++;
            ResolveDueActions();
            TickAdvanced?.Invoke(CurrentTick);
        }
    }

    private void ResolveDueActions()
    {
        // Snapshot the due actions before invoking any callback so that
        // cancellations or new schedules performed inside a callback do not
        // disturb this tick's resolution set. Ties resolve in schedule order.
        List<ScheduledAction>? due = null;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].ResolveTick <= CurrentTick)
            {
                (due ??= new List<ScheduledAction>()).Add(_pending[i]);
                _pending.RemoveAt(i);
            }
        }

        if (due is null)
            return;

        due.Sort(static (a, b) => a.Sequence.CompareTo(b.Sequence));
        foreach (var action in due)
            action.Callback();
    }
}
