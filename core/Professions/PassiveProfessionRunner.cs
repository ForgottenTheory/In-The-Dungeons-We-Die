using Dungeons.Simulation;

namespace Dungeons.Professions;

/// <summary>
/// Drives one passive profession action on the <see cref="TickEngine"/>: it schedules
/// a completion at the effective interval, applies it through the shared
/// <see cref="ProfessionSystem.Execute"/> path, and reschedules — until stopped or the
/// action can no longer run (e.g. inputs exhausted). Only one action runs at a time,
/// modelling the Hideout's single current activity.
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

    public string? CurrentActionId { get; private set; }
    public bool IsRunning => CurrentActionId is not null;

    public event Action<string>? Started;
    public event Action<ActionOutcome>? Stalled;
    public event Action? Stopped;

    /// <summary>Begins passive execution. Returns false if the action cannot currently run.</summary>
    public bool Start(string actionId)
    {
        Stop();
        if (_system.CheckExecutable(actionId) != ActionFailure.None)
            return false;

        CurrentActionId = actionId;
        ScheduleNext();
        Started?.Invoke(actionId);
        return true;
    }

    public void Stop()
    {
        if (_pending is not null)
        {
            _tick.Cancel(_pending.Id);
            _pending = null;
        }

        if (CurrentActionId is null)
            return;

        CurrentActionId = null;
        Stopped?.Invoke();
    }

    /// <summary>Progress toward the next completion in [0, 1], for UI.</summary>
    public double Progress()
    {
        if (!IsRunning)
            return 0.0;
        var span = _intervalEndTick - _intervalStartTick;
        if (span <= 0)
            return 0.0;
        return Math.Clamp((double)(_tick.CurrentTick - _intervalStartTick) / span, 0.0, 1.0);
    }

    private void ScheduleNext()
    {
        var interval = _system.EffectiveIntervalTicks(CurrentActionId!);
        _intervalStartTick = _tick.CurrentTick;
        _intervalEndTick = _tick.CurrentTick + interval;
        _pending = _tick.Schedule(interval, OnComplete);
    }

    private void OnComplete()
    {
        _pending = null;
        var actionId = CurrentActionId;
        if (actionId is null)
            return;

        var outcome = _system.Execute(actionId, performance: 0.0, isActive: false);
        if (!outcome.Success)
        {
            CurrentActionId = null;
            Stalled?.Invoke(outcome);
            Stopped?.Invoke();
            return;
        }

        ScheduleNext();
    }
}
