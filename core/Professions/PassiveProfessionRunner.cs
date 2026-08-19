using Dungeons.Simulation;

namespace Dungeons.Professions;

/// <summary>What the Hideout's standing training selection is doing right now.</summary>
public enum PassiveTrainingState
{
    /// <summary>Nothing selected. The player has not chosen anything to train.</summary>
    Idle,

    /// <summary>The selected action is running: a completion is scheduled.</summary>
    Working,

    /// <summary>
    /// Selected, but it cannot run at this moment — almost always because the inputs ran out.
    /// The selection is <b>kept</b> and re-checked; the moment the materials are back (a crop
    /// lifted, a run extracted, an offline payout) it resumes on its own.
    /// </summary>
    Waiting,
}

/// <summary>
/// Drives the Hideout's one standing training selection on the <see cref="TickEngine"/>: it
/// schedules a completion at the effective interval, applies it through the shared
/// <see cref="ProfessionSystem.Execute"/> path, and reschedules. Only one action runs at a time,
/// modelling the Hideout's single current activity.
///
/// <para><b>The selection outlives the run (Phase 10, auto-repeat).</b> Before this, an action
/// that ran out of materials stopped and was forgotten — so the one thing idle progression is
/// for, "leave it going and come back", ended the first time a chest of ore ran dry. Now the
/// choice is a <em>standing</em> one: <see cref="SelectedActionId"/> survives a stall, the runner
/// sits in <see cref="PassiveTrainingState.Waiting"/>, re-checks every
/// <see cref="ProfessionTuning.PassiveRetryIntervalTicks"/>, and resumes by itself. Only
/// <see cref="Stop"/> — the player changing their mind — clears the selection.</para>
///
/// <para><b>Temporary problems wait; permanent ones refuse.</b> <see cref="Start"/> accepts an
/// action that is merely missing its inputs and waits for them, but refuses one that is unknown
/// or above the player's level, because no amount of waiting fixes those.</para>
/// </summary>
public sealed class PassiveProfessionRunner
{
    private readonly TickEngine _tick;
    private readonly ProfessionSystem _system;

    private ScheduledAction? _pending;
    private long _intervalStartTick;
    private long _intervalEndTick;

    public PassiveProfessionRunner(TickEngine tick, ProfessionSystem system)
    {
        _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        _system = system ?? throw new ArgumentNullException(nameof(system));
    }

    /// <summary>The standing training selection. Null only when the player has chosen nothing.</summary>
    public string? SelectedActionId { get; private set; }

    public PassiveTrainingState State { get; private set; } = PassiveTrainingState.Idle;

    /// <summary>The action actually being worked right now — null while waiting for materials.</summary>
    public string? CurrentActionId => State == PassiveTrainingState.Working ? SelectedActionId : null;

    public bool IsRunning => State == PassiveTrainingState.Working;

    /// <summary>True when a selection is held but cannot proceed. The UI says so rather than
    /// silently showing nothing, because a benefit the player never sees is a benefit they do
    /// not believe in — and the same is true of a stall.</summary>
    public bool IsWaiting => State == PassiveTrainingState.Waiting;

    public event Action<string>? Started;

    /// <summary>The selected action could not complete. It is <b>not</b> forgotten — the runner
    /// is now waiting on it.</summary>
    public event Action<ActionOutcome>? Stalled;

    /// <summary>A waiting selection became runnable again and picked itself back up.</summary>
    public event Action<string>? Resumed;

    public event Action? Stopped;

    /// <summary>
    /// Makes <paramref name="actionId"/> the standing selection. Returns false only for a
    /// problem waiting cannot fix (the action does not exist, or the profession is too low).
    /// </summary>
    public bool Start(string actionId)
    {
        Stop();

        var failure = _system.CheckExecutable(actionId);
        if (failure is ActionFailure.UnknownAction or ActionFailure.LevelTooLow)
            return false;

        SelectedActionId = actionId;

        if (failure == ActionFailure.None)
        {
            State = PassiveTrainingState.Working;
            ScheduleNext();
        }
        else
        {
            EnterWaiting();
        }

        Started?.Invoke(actionId);
        return true;
    }

    /// <summary>Clears the standing selection. The only thing that does.</summary>
    public void Stop()
    {
        CancelPending();

        if (SelectedActionId is null)
            return;

        SelectedActionId = null;
        State = PassiveTrainingState.Idle;
        Stopped?.Invoke();
    }

    /// <summary>Progress toward the next completion in [0, 1], for UI. Zero while waiting —
    /// there is no interval to be partway through.</summary>
    public double Progress()
    {
        if (!IsRunning)
            return 0.0;
        var span = _intervalEndTick - _intervalStartTick;
        if (span <= 0)
            return 0.0;
        return Math.Clamp((double)(_tick.CurrentTick - _intervalStartTick) / span, 0.0, 1.0);
    }

    private void CancelPending()
    {
        if (_pending is null)
            return;
        _tick.Cancel(_pending.Id);
        _pending = null;
    }

    private void ScheduleNext()
    {
        var interval = _system.EffectiveIntervalTicks(SelectedActionId!);
        _intervalStartTick = _tick.CurrentTick;
        _intervalEndTick = _tick.CurrentTick + interval;
        _pending = _tick.Schedule(interval, OnComplete);
    }

    private void EnterWaiting()
    {
        State = PassiveTrainingState.Waiting;
        _pending = _tick.Schedule(ProfessionTuning.PassiveRetryIntervalTicks, OnRetry);
    }

    private void OnComplete()
    {
        _pending = null;
        var actionId = SelectedActionId;
        if (actionId is null)
            return;

        var outcome = _system.Execute(actionId, performance: 0.0, isActive: false);
        if (!outcome.Success)
        {
            EnterWaiting();
            Stalled?.Invoke(outcome);
            return;
        }

        ScheduleNext();
    }

    private void OnRetry()
    {
        _pending = null;
        var actionId = SelectedActionId;
        if (actionId is null)
            return;

        if (!_system.CanExecute(actionId))
        {
            EnterWaiting();
            return;
        }

        State = PassiveTrainingState.Working;
        ScheduleNext();
        Resumed?.Invoke(actionId);
    }
}
