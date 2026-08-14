using System.Linq;
using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Randomness;

namespace Dungeons.Professions;

/// <summary>Notification that a profession's level increased.</summary>
public readonly record struct ProfessionLevelUp(string ProfessionId, int OldLevel, int NewLevel);

/// <summary>
/// Coordinates profession execution: validates an action, consumes inputs, produces
/// outputs into the inventory, and applies XP and mastery. This single
/// <see cref="Execute"/> path serves both passive and active play, so they can never
/// diverge into separate balance models (docs/architecture.md §20). It holds no
/// gameplay formulas beyond wiring — those live in <see cref="ActionResolver"/> and
/// <see cref="ProfessionTuning"/>.
/// </summary>
public sealed class ProfessionSystem
{
    private readonly DataStore<ProfessionActionDefinition> _actions;
    private readonly Inventory _inventory;
    private readonly IRandomSource _rng;
    private readonly Dictionary<string, ProfessionProgress> _progress = new(StringComparer.Ordinal);

    public ProfessionSystem(
        DataStore<ProfessionActionDefinition> actions,
        Inventory inventory,
        IRandomSource rng,
        IEnumerable<ProfessionProgress>? initialProgress = null)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        if (initialProgress is not null)
        {
            foreach (var progress in initialProgress)
                _progress[progress.ProfessionId] = progress;
        }
    }

    /// <summary>Raised after any action attempt (success or failure).</summary>
    public event Action<ActionOutcome>? ActionCompleted;

    /// <summary>Raised when an action pushes its profession to a new level.</summary>
    public event Action<ProfessionLevelUp>? LeveledUp;

    public ProfessionProgress GetProgress(string professionId)
    {
        if (!_progress.TryGetValue(professionId, out var progress))
        {
            progress = new ProfessionProgress(professionId);
            _progress[professionId] = progress;
        }

        return progress;
    }

    public ProfessionActionDefinition GetAction(string actionId) => _actions.GetById(actionId);

    public int EffectiveIntervalTicks(string actionId)
    {
        var action = _actions.GetById(actionId);
        var mastery = GetProgress(action.ProfessionId).GetMastery(actionId);
        return ProfessionTuning.EffectiveIntervalTicks(action.BaseIntervalTicks, mastery);
    }

    public ActionFailure CheckExecutable(string actionId)
    {
        if (!_actions.TryGetById(actionId, out var action))
            return ActionFailure.UnknownAction;
        if (GetProgress(action.ProfessionId).Level < action.RequiredLevel)
            return ActionFailure.LevelTooLow;
        if (action.Inputs.Count > 0 && !_inventory.CanRemoveAll(action.Inputs.Select(i => i.ToStack()).ToList()))
            return ActionFailure.MissingInputs;
        return ActionFailure.None;
    }

    public bool CanExecute(string actionId) => CheckExecutable(actionId) == ActionFailure.None;

    /// <summary>
    /// Attempts one completion of <paramref name="actionId"/>. <paramref name="performance"/>
    /// is the active-play score in [0, 1] (0 for passive). Returns the authoritative outcome.
    /// </summary>
    public ActionOutcome Execute(string actionId, double performance = 0.0, bool isActive = false)
    {
        var failure = CheckExecutable(actionId);
        if (failure != ActionFailure.None)
            return ActionOutcome.Failed(actionId, failure);

        var action = _actions.GetById(actionId);
        var progress = GetProgress(action.ProfessionId);

        var inputs = action.Inputs.Select(i => i.ToStack()).ToList();
        if (inputs.Count > 0)
            _inventory.TryRemoveAll(inputs); // guaranteed by CheckExecutable above

        var mastery = progress.GetMastery(actionId);
        var yield = ActionResolver.Resolve(action, mastery, performance, _rng);
        foreach (var stack in yield.Produced)
            _inventory.Add(stack);

        var oldLevel = progress.Level;
        progress.AddXp(yield.Xp);
        progress.AddMastery(actionId, ProfessionTuning.MasteryPerAction);
        var newLevel = progress.Level;

        var outcome = new ActionOutcome
        {
            ActionId = actionId,
            Success = true,
            Consumed = inputs,
            Produced = yield.Produced,
            XpGained = yield.Xp,
            MasteryGained = ProfessionTuning.MasteryPerAction,
            Performance = performance,
            WasActive = isActive,
        };

        ActionCompleted?.Invoke(outcome);
        if (newLevel > oldLevel)
            LeveledUp?.Invoke(new ProfessionLevelUp(action.ProfessionId, oldLevel, newLevel));

        return outcome;
    }
}
