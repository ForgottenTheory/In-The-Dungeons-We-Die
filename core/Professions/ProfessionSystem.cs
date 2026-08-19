using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Loot;
using Dungeons.Randomness;

namespace Dungeons.Professions;

/// <summary>Notification that a profession's level increased.</summary>
public readonly record struct ProfessionLevelUp(string ProfessionId, int OldLevel, int NewLevel);

/// <summary>
/// Rolls a profession action's drop table. A delegate rather than a <c>LootResolver</c> field
/// because the <em>circumstances</em> of the roll — how deep the party is, which Realm they are
/// in, whether this attempt was active — belong to the host, not to Core's profession rules.
/// Null wherever no loot system is wired, in which case an action yields only what it authors.
/// </summary>
public delegate LootResult RollActionDropTable(string lootTableId, bool wasActive);

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
    private readonly Func<Inventory> _inventoryProvider;
    private readonly IRandomSource _random;
    private readonly RollActionDropTable? _rollDropTable;
    private readonly Dictionary<string, ProfessionProgress> _progress = new(StringComparer.Ordinal);

    /// <summary>
    /// What the player's progress is currently worth to their work — the mastery ladder and the
    /// synergy table folded into one answer. Settable rather than constructor-only because the
    /// host builds the profession system before content validation finishes, and because a test
    /// that does not care about benefits should not have to supply a ladder.
    /// </summary>
    public ProfessionBenefits Benefits { get; set; } = ProfessionBenefits.None;

    public ProfessionSystem(
        DataStore<ProfessionActionDefinition> actions,
        Inventory inventory,
        IRandomSource random,
        IEnumerable<ProfessionProgress>? initialProgress = null)
        : this(actions, () => inventory, random, initialProgress)
    {
        ArgumentNullException.ThrowIfNull(inventory);
    }

    /// <summary>
    /// Provider overload: the target inventory is resolved per action, so gathering
    /// can deposit into the Hideout Stash or the current Realm run inventory.
    /// </summary>
    public ProfessionSystem(
        DataStore<ProfessionActionDefinition> actions,
        Func<Inventory> inventoryProvider,
        IRandomSource random,
        IEnumerable<ProfessionProgress>? initialProgress = null,
        RollActionDropTable? rollDropTable = null)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _inventoryProvider = inventoryProvider ?? throw new ArgumentNullException(nameof(inventoryProvider));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _rollDropTable = rollDropTable;

        if (initialProgress is not null)
        {
            foreach (var progress in initialProgress)
                _progress[progress.ProfessionId] = progress;
        }
    }

    /// <summary>Raised after any action attempt (success or failure).</summary>
    public event Action<ActionOutcome>? ActionCompleted;

    /// <summary>Raised after any pursued opportunity resolves.</summary>
    public event Action<OpportunityOutcome>? OpportunityResolved;

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

    /// <summary>All tracked profession progress, for persistence.</summary>
    public IReadOnlyCollection<ProfessionProgress> AllProgress => _progress.Values;

    /// <summary>Replaces all progression state (used when loading a save).</summary>
    public void RestoreProgress(IEnumerable<ProfessionProgress> progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        _progress.Clear();
        foreach (var p in progress)
            _progress[p.ProfessionId] = p;
    }

    public ProfessionActionDefinition GetAction(string actionId) => _actions.GetById(actionId);

    public int EffectiveIntervalTicks(string actionId)
    {
        var action = _actions.GetById(actionId);
        var mastery = GetProgress(action.ProfessionId).GetMastery(actionId);
        var reduction = Benefits.ValueOf(ProfessionBenefitKind.IntervalReduction, action.ProfessionId, mastery);
        return ProfessionTuning.EffectiveIntervalTicks(action.BaseIntervalTicks, reduction);
    }

    /// <summary>Mastery points banked in one action — what the ladder is measured against.</summary>
    public int MasteryOf(string actionId) =>
        _actions.TryGetById(actionId, out var action)
            ? GetProgress(action.ProfessionId).GetMastery(actionId)
            : 0;

    public ActionFailure CheckExecutable(string actionId)
    {
        var gate = CheckKnownAndUnlocked(actionId);
        if (gate != ActionFailure.None)
            return gate;

        var action = _actions.GetById(actionId);
        if (action.Inputs.Count > 0 && !_inventoryProvider().CanRemoveAll(action.Inputs))
            return ActionFailure.MissingInputs;
        return ActionFailure.None;
    }

    /// <summary>The half of the gate that does not depend on the bag: the action exists and
    /// the profession is high enough. Split out because a prepaid completion has already
    /// spent its inputs and must not be blocked for no longer holding them.</summary>
    private ActionFailure CheckKnownAndUnlocked(string actionId)
    {
        if (!_actions.TryGetById(actionId, out var action))
            return ActionFailure.UnknownAction;
        if (GetProgress(action.ProfessionId).Level < action.RequiredLevel)
            return ActionFailure.LevelTooLow;
        return ActionFailure.None;
    }

    public bool CanExecute(string actionId) => CheckExecutable(actionId) == ActionFailure.None;

    /// <summary>Whether an execution still owes the action's inputs.</summary>
    private enum InputHandling
    {
        /// <summary>The normal case: take the inputs as part of this completion.</summary>
        ConsumeNow,

        /// <summary>The inputs were taken when the work was started — a Farming plot pays for
        /// its seed at planting time and the crop arrives much later.</summary>
        AlreadyPaid,
    }

    /// <summary>
    /// Attempts one completion of <paramref name="actionId"/>. <paramref name="performance"/>
    /// is the active-play score in [0, 1] (0 for passive). Returns the authoritative outcome.
    /// </summary>
    public ActionOutcome Execute(string actionId, double performance = 0.0, bool isActive = false) =>
        Execute(actionId, performance, isActive, InputHandling.ConsumeNow);

    /// <summary>
    /// Completes an action whose inputs were already consumed when it was started. Used by
    /// Farming, where planting takes the seed and harvesting — much later — produces the crop.
    /// Everything else about the completion is identical, so the two can never drift.
    /// </summary>
    public ActionOutcome CompletePrepaidAction(string actionId) =>
        Execute(actionId, performance: 0.0, isActive: false, InputHandling.AlreadyPaid);

    private ActionOutcome Execute(string actionId, double performance, bool isActive, InputHandling inputHandling)
    {
        var failure = inputHandling == InputHandling.AlreadyPaid
            ? CheckKnownAndUnlocked(actionId)
            : CheckExecutable(actionId);
        if (failure != ActionFailure.None)
            return ActionOutcome.Failed(actionId, failure);

        var action = _actions.GetById(actionId);
        var progress = GetProgress(action.ProfessionId);

        var bag = _inventoryProvider();
        var mastery = progress.GetMastery(actionId);
        var yield = ActionResolver.Resolve(action, mastery, performance, _random, isActive, Benefits);

        // Preservation is spent here rather than inside the resolver, because whether the inputs
        // were owed at all is this layer's question: a Farming harvest paid for its seed at
        // planting time and has nothing left to preserve.
        var preserved = yield.InputsPreserved && inputHandling == InputHandling.ConsumeNow && action.Inputs.Count > 0;
        if (inputHandling == InputHandling.ConsumeNow && action.Inputs.Count > 0 && !preserved)
            bag.TryRemoveAll(action.Inputs); // guaranteed by CheckExecutable above

        foreach (var stack in yield.Produced)
            bag.Add(stack);

        // The drop table is what the work turns up rather than what it produces, so it only
        // rolls on an attempt that actually landed — a bolted deer leaves nothing behind.
        var produced = yield.Produced;
        if (yield.Landed && action.LootTableId is { Length: > 0 } dropTableId && _rollDropTable is not null)
        {
            var found = _rollDropTable(dropTableId, isActive);
            if (!found.IsEmpty)
            {
                found.DepositInto(bag);
                produced = produced.Concat(found.Drops.Select(drop => drop.Stack)).ToList();
            }
        }

        // A missed attempt still teaches the hand, but it does not deepen the craft.
        var masteryGained = yield.Landed ? ProfessionTuning.MasteryPerAction : 0;

        var oldLevel = progress.Level;
        progress.AddXp(yield.Xp);
        progress.AddMastery(actionId, masteryGained);
        var newLevel = progress.Level;

        var outcome = new ActionOutcome
        {
            ActionId = actionId,
            Success = true,
            AttemptMissed = !yield.Landed,
            Consumed = inputHandling == InputHandling.ConsumeNow && !preserved ? action.Inputs : Array.Empty<ItemStack>(),
            Produced = produced,
            InputsPreserved = preserved,
            OutputsDoubled = yield.OutputsDoubled,
            XpGained = yield.Xp,
            MasteryGained = masteryGained,
            Performance = performance,
            WasActive = isActive,
            RealmKnowledgeGained = yield.Landed ? action.RealmKnowledgeGain : null,
            DiscoveredOpportunity = yield.Discovered,
        };

        ActionCompleted?.Invoke(outcome);
        if (newLevel > oldLevel)
            LeveledUp?.Invoke(new ProfessionLevelUp(action.ProfessionId, oldLevel, newLevel));

        return outcome;
    }

    /// <summary>
    /// Looks up an opportunity by id on the action that could surface it. Returns false for
    /// an unknown pair, so the client can validate a pending offer before showing it.
    /// </summary>
    public bool TryGetOpportunity(string actionId, string opportunityId, out ProfessionOpportunityDefinition opportunity)
    {
        opportunity = null!;
        if (!_actions.TryGetById(actionId, out var action))
            return false;

        foreach (var candidate in action.Opportunities)
        {
            if (!string.Equals(candidate.Id, opportunityId, StringComparison.Ordinal))
                continue;
            opportunity = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Takes a discovered opportunity: consumes any extra inputs, rolls its risk, and banks
    /// the payoff. The <em>time</em> it costs is the client's to spend — Core resolves the
    /// gamble, the client decides when the result arrives (docs/code-map.md §10.14).
    /// Declining is simply never calling this.
    /// </summary>
    public OpportunityOutcome PursueOpportunity(string actionId, string opportunityId)
    {
        if (!_actions.TryGetById(actionId, out var action))
            return OpportunityOutcome.Failed(actionId, opportunityId, OpportunityFailure.UnknownAction);
        if (!TryGetOpportunity(actionId, opportunityId, out var opportunity))
            return OpportunityOutcome.Failed(actionId, opportunityId, OpportunityFailure.UnknownOpportunity);

        var bag = _inventoryProvider();
        if (opportunity.Inputs.Count > 0 && !bag.CanRemoveAll(opportunity.Inputs))
            return OpportunityOutcome.Failed(actionId, opportunityId, OpportunityFailure.MissingInputs);

        if (opportunity.Inputs.Count > 0)
            bag.TryRemoveAll(opportunity.Inputs);

        var progress = GetProgress(action.ProfessionId);
        var mastery = progress.GetMastery(actionId);
        var yield = ActionResolver.ResolvePursuit(opportunity, mastery, _random, action.ProfessionId, Benefits);
        foreach (var stack in yield.Produced)
            bag.Add(stack);

        var oldLevel = progress.Level;
        progress.AddXp(yield.Xp);
        var newLevel = progress.Level;

        var outcome = new OpportunityOutcome
        {
            ActionId = actionId,
            OpportunityId = opportunityId,
            Success = yield.Landed,
            Failure = yield.Landed ? OpportunityFailure.None : OpportunityFailure.RiskRealised,
            Consumed = opportunity.Inputs,
            Produced = yield.Produced,
            XpGained = yield.Xp,
        };

        OpportunityResolved?.Invoke(outcome);
        if (newLevel > oldLevel)
            LeveledUp?.Invoke(new ProfessionLevelUp(action.ProfessionId, oldLevel, newLevel));

        return outcome;
    }
}
