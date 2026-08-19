using System.Linq;
using Dungeons.Content;
using Dungeons.Events;
using Dungeons.Modifiers;
using Dungeons.Randomness;
using Dungeons.Rules;
using Dungeons.Simulation;

namespace Dungeons.Combat;

public enum CombatResult
{
    Victory,
    Defeat,
}

/// <summary>
/// Where an action is in its lifecycle (docs/moves.md §2.3).
///
/// <para><c>QUEUE → TELEGRAPH → WINDUP → EXECUTION → RECOVERY → READY</c>. Before E2 the first
/// two were collapsed into a single time-to-impact, which is why GDD §5.2 recorded
/// "interrupt during windup" as inexpressible.</para>
/// </summary>
public enum ActionPhase
{
    /// <summary>Intent is visible. The defender can read it and answer.</summary>
    Telegraph,

    /// <summary>Committed and swinging — the window where an interrupt lands.</summary>
    Windup,
}

/// <summary>
/// One action between commitment and execution, for either side.
///
/// <para>Player and enemy share this because the phases are the same for both — and since E4
/// the payload is the same too: both sides execute a <see cref="ResolvedMove"/>, and the only
/// divergence left is who chose it.</para>
/// </summary>
public sealed class ActionInFlight
{
    public required Combatant Actor { get; init; }
    public required Combatant Target { get; init; }
    public required ResolvedMove Move { get; init; }

    /// <summary>Event tags: the move's own plus the bare legacy aliases (see
    /// <c>CombatEncounter.MoveEventTags</c>).</summary>
    public required HashSet<string> Tags { get; init; }

    /// <summary>The chain this action belongs to, when a proc triggered it. Null for real actions.</summary>
    public Dungeons.Rules.EffectContext? Context { get; init; }

    public string Name => Move.Name;
    public Dungeons.Actions.ActionTiming Timing => Move.Timing;

    public ActionPhase Phase { get; internal set; } = ActionPhase.Telegraph;

    /// <summary>Tick the current phase ends. During Windup this is the moment of impact.</summary>
    public long PhaseEndsTick { get; internal set; }

    internal ScheduledAction? Scheduled { get; set; }
}

/// <summary>The two timed defensive stances the player can raise (docs/damage-and-defense.md §5).</summary>
public enum DefensiveStance
{
    /// <summary>Mitigates, and opens the Perfect Block window for its first few ticks.</summary>
    Block,

    /// <summary>Avoids outright while the stance is up, at the cost of action time.</summary>
    Dodge,
}

/// <summary>A pending enemy attack the player can see and react to.</summary>
public sealed class EnemyIntent
{
    public required Combatant Attacker { get; init; }
    public required ResolvedMove Move { get; init; }
    public required long ExecuteTick { get; init; }

    /// <summary>Telegraph is "something is coming"; Windup is "and it is too late to stop it gently".</summary>
    public required ActionPhase Phase { get; init; }
}

/// <summary>The result of a finished encounter, including who was defeated (for loot).</summary>
public sealed class CombatOutcome
{
    public required CombatResult Result { get; init; }
    public required IReadOnlyList<Combatant> DefeatedEnemies { get; init; }
}

/// <summary>
/// Authoritative tick-driven encounter (lifecycle: docs/moves.md §2.3; damage:
/// docs/damage-and-defense.md). Enemies run a
/// self-scheduling decide → telegraph → execute → recovery loop on the shared
/// <see cref="TickEngine"/>; the player issues commands that resolve on the same
/// clock. Blocking and dodging are timed stances that only matter if landed near an
/// incoming attack's execution tick — the core skill expression. The UI observes via
/// events and read-only queries; it never computes results.
/// </summary>
public sealed class CombatEncounter
{
    /// <summary>
    /// The player's identity in <see cref="GameEvent"/>s.
    /// <para>
    /// It is the literal string <c>"self"</c> because shipped prefix content already matches on
    /// it — `{ "kind": "sourceIsSelf", "text": "self" }` in `prefixes.json`. Enemies use their
    /// display name. <b>Known debt:</b> this does not survive allied NPCs or multiplayer; it
    /// becomes a real combatant id when something needs one.
    /// </para>
    /// </summary>
    public const string SelfId = "self";

    private readonly TickEngine _tick;
    private readonly HitPipeline _pipeline;
    private readonly DataStore<MoveDefinition> _moves;
    private readonly DataStore<MoveModifierDefinition>? _moveModifiers;
    private readonly IRandomSource _random;
    private readonly IGameEventBus _bus;

    /// <summary>Per-combatant, per-move cooldown bookkeeping.</summary>
    private readonly Dictionary<(Combatant, string), long> _cooldowns = new();

    /// <summary>Moves granted mid-encounter by `grantMove`, with expiry (0 = encounter).</summary>
    private readonly List<(Combatant Owner, ResolvedMove Move, long ExpiresTick)> _grantedMoves = new();

    /// <summary>Modifiers attached by `modifyMove`, applied at execution time — "the next
    /// attack is empowered" cannot be pre-baked into a cached moveset.</summary>
    private readonly List<(Combatant Owner, MoveModifierDefinition Definition, string Source, long ExpiresTick)> _timedMoveModifiers = new();

    /// <summary>Every action currently between commitment and impact, both sides.</summary>
    private readonly Dictionary<Combatant, ActionInFlight> _inFlight = new();

    /// <summary>Enemy recovery timers — separate because they exist *between* actions.</summary>
    private readonly Dictionary<Combatant, ScheduledAction> _recovery = new();

    /// <summary>Last move each enemy committed, for <see cref="Combatant.AvoidRepeatWeight"/>.</summary>
    private readonly Dictionary<Combatant, string> _lastMoveUsed = new();

    private readonly List<Combatant> _defeatedEnemies = new();
    private readonly Dictionary<string, int> _eventCounts = new(StringComparer.Ordinal);
    private ScheduledAction? _regenPending;
    private ScheduledAction? _statusPending;

    private Combatant _player = null!;
    private List<Combatant> _enemies = new();

    public CombatEncounter(
        TickEngine tick,
        HitPipeline pipeline,
        DataStore<MoveDefinition> moves,
        IRandomSource random,
        IGameEventBus bus,
        StatusController? statuses = null,
        Characters.GaugeController? gauges = null,
        CombatantModifiers? modifiers = null,
        DataStore<MoveModifierDefinition>? moveModifiers = null)
    {
        _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _moves = moves ?? throw new ArgumentNullException(nameof(moves));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _moveModifiers = moveModifiers;

        // Optional so combat-calculation tests need not construct a status store. Every path
        // that reads it null-checks; nothing silently degrades.
        Statuses = statuses;
        if (Statuses is not null)
            Statuses.Ticked += OnStatusTick;

        Gauges = gauges;
        Modifiers = modifiers;
    }

    /// <summary>Statuses on every combatant, and the Resolve pool gating controls (E2).</summary>
    public StatusController? Statuses { get; }

    /// <summary>
    /// The player build's gauges (E3c). Optional for the same reason <see cref="Statuses"/> is —
    /// combat-calculation tests need not construct one, and every path null-checks.
    /// </summary>
    public Characters.GaugeController? Gauges { get; }

    /// <summary>
    /// The assembled modifier read path (E3c-2) — build statics, status <c>while_active</c>,
    /// gauge bands and timed <c>grantModifier</c> grants. Optional like the others.
    /// </summary>
    public CombatantModifiers? Modifiers { get; }

    /// <summary>
    /// Where move riders go (E4) — the rule engine, wearing its <see cref="IEffectSink"/> hat.
    /// Set by <c>RegisterCombatHandlers</c>. Null leaves riders inert, which calculation tests
    /// rely on and running games must not.
    /// </summary>
    public IEffectSink? EffectSink { get; set; }

    /// <summary>World state for move <c>Requires</c> and AI conditions (E3c-3's vocabulary).</summary>
    public IConditionWorld? ConditionWorld { get; set; }

    /// <summary>The player's usable moves right now: the built moveset plus anything granted
    /// mid-encounter that has not expired.</summary>
    public IReadOnlyList<ResolvedMove> PlayerMoves =>
        _player.Moveset
            .Concat(_grantedMoves.Where(g => g.Owner == _player).Select(g => g.Move))
            .ToList();

    public event Action<string>? Logged;
    public event Action? StateChanged;
    public event Action<CombatOutcome>? Ended;

    /// <summary>
    /// The full stage-by-stage trace of every hit (docs/damage-and-defense.md §3.3). Required
    /// scope, not polish: a pipeline with this many multiplicative sources is unplayable if it
    /// cannot answer "why did that hit for 17?". The Combat Lab renders it; the narration in
    /// <see cref="Logged"/> stays a single readable sentence.
    /// </summary>
    public event Action<HitResult>? HitResolved;

    /// <summary>The most recent hit's trace, for the debug console.</summary>
    public HitResult? LastHit { get; private set; }

    public bool IsActive { get; private set; }
    public Combatant Player => _player;
    public IReadOnlyList<Combatant> Enemies => _enemies;
    public IReadOnlyList<Combatant> Combatants => _enemies.Prepend(_player).ToList();
    public IReadOnlyList<EnemyIntent> Intents => _inFlight.Values
        .Where(a => a.Actor.Team == CombatTeam.Enemy)
        .Select(a => new EnemyIntent
        {
            Attacker = a.Actor,
            Move = a.Move,
            ExecuteTick = a.Phase == ActionPhase.Windup
                ? a.PhaseEndsTick
                : a.PhaseEndsTick + Math.Max(1, a.Timing.WindupTicks),
            Phase = a.Phase,
        })
        .ToList();

    /// <summary>The action this combatant is currently committed to, if any.</summary>
    public ActionInFlight? ActionOf(Combatant combatant) =>
        _inFlight.TryGetValue(combatant, out var action) ? action : null;
    public long CurrentTick => _tick.CurrentTick;
    public bool PlayerReady => IsActive && _player.IsReady(_tick.CurrentTick);

    public void Start(Combatant player, IReadOnlyList<Combatant> enemies)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _enemies = enemies?.ToList() ?? throw new ArgumentNullException(nameof(enemies));
        _inFlight.Clear();
        _recovery.Clear();
        _lastMoveUsed.Clear();
        _defeatedEnemies.Clear();
        _eventCounts.Clear();
        IsActive = true;
        _player.ReadyTick = _tick.CurrentTick;

        // Gauges are per-encounter: Charge earned against the last pack does not carry into the
        // next fight, or the meter stops being something you build inside a fight.
        Gauges?.Reset(_tick.CurrentTick);
        Modifiers?.Timed.Clear();
        _cooldowns.Clear();
        _grantedMoves.Clear();
        _timedMoveModifiers.Clear();

        Log($"Combat started: {_player.Name} vs {string.Join(", ", _enemies.Select(e => e.Name))}.");
        Publish(GameEvents.EncounterStarted, source: SelfId, amount: _enemies.Count);
        ScheduleStaminaRegen();
        ScheduleStatusTick();
        foreach (var enemy in _enemies)
            BeginEnemyDecision(enemy);

        StateChanged?.Invoke();
    }

    // --- Player commands ----------------------------------------------------

    /// <summary>The default attack: the first `action:attack` move in the moveset — which is the
    /// weapon's, because weapon moves are granted first (the Fighter's whole identity).</summary>
    public bool Attack()
    {
        var move = PlayerMoves.FirstOrDefault(m => string.Equals(m.ActionKind, "attack", StringComparison.OrdinalIgnoreCase));
        if (move is null)
        {
            Log("You have no attack.");
            return false;
        }

        return UseMove(move.Id);
    }

    /// <summary>Uses a move from the player's moveset by id.</summary>
    public bool UseMove(string moveId)
    {
        if (!IsActive)
            return false;

        var move = PlayerMoves.FirstOrDefault(m => string.Equals(m.Id, moveId, StringComparison.Ordinal));
        if (move is null)
        {
            Log("You don't know that move.");
            return false;
        }

        if (!_player.IsReady(_tick.CurrentTick))
        {
            Log("You are still recovering.");
            return false;
        }

        if (!CanUse(_player, move, out var reason))
        {
            Log(reason);
            return false;
        }

        var target = move.Targeting == Targeting.Self ? _player : FirstAliveEnemy();
        if (target is null)
            return false;

        PayCosts(_player, move);
        StartCooldown(_player, move);
        _lastMoveUsed[_player] = move.Id; // feeds AvoidRepeatWeight when a pilot is choosing

        var executeIn = Math.Max(1, move.Timing.TimeToImpactTicks);
        _player.ReadyTick = _tick.CurrentTick + executeIn + move.Timing.RecoveryTicks;
        Log($"You ready {move.Name}.");

        Commit(new ActionInFlight
        {
            Actor = _player,
            Target = target,
            Move = move,
            Tags = MoveEventTags(move),
        });

        return true;
    }

    // --- Queue validation (docs/moves.md §2.3: the Queue phase) --------------

    /// <summary>Everything that gates a move at queue time, with a player-readable reason.</summary>
    private bool CanUse(Combatant actor, ResolvedMove move, out string reason)
    {
        if (!CanAct(actor, move.ActionKind))
        {
            reason = $"{actor.Name} cannot use {move.Name} right now.";
            return false;
        }

        if (_cooldowns.TryGetValue((actor, move.Id), out var readyAt) && _tick.CurrentTick < readyAt)
        {
            reason = $"{move.Name} is not ready.";
            return false;
        }

        foreach (var requirement in move.Requires)
        {
            if (!TriggerRuleEngine.Evaluate(requirement, RequirementEvent(actor), ConditionWorld))
            {
                reason = $"{move.Name}: requirement not met.";
                return false;
            }
        }

        foreach (var cost in move.Costs)
        {
            if (!CanAfford(actor, cost))
            {
                reason = $"Not enough {cost.Resource} for {move.Name}.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>The event a move requirement is evaluated against. `self_*` fractions are the
    /// <b>acting</b> combatant's — an enemy checking its own health reads its own bar.</summary>
    private GameEvent RequirementEvent(Combatant actor) => new(
        "MoveRequirement",
        Source: Id(actor),
        Target: Id(actor.Team == CombatTeam.Enemy ? _player : FirstAliveEnemy() ?? _player),
        Values: new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["self_health_fraction"] = Fraction(actor.Health),
            ["self_stamina_fraction"] = Fraction(actor.Stamina),
            ["gauge_fraction"] = Gauges?.HighestFraction ?? 0.0,
        },
        CanTrigger: false);

    private bool CanAfford(Combatant actor, Dungeons.Actions.ActionCost cost)
    {
        if (Gauges is not null && actor == _player && Gauges.Has(cost.Resource))
            return Gauges.Current(cost.Resource) >= cost.Amount;

        var pool = PoolNamed(actor, cost.Resource);
        if (pool is null)
            return false;

        // A health cost may wound but never self-kill — Vitalist-style casting costs blood,
        // not suicide.
        return pool.Type == Characters.ResourceType.Health
            ? pool.Current > cost.Amount
            : pool.Current >= cost.Amount;
    }

    private void PayCosts(Combatant actor, ResolvedMove move)
    {
        foreach (var cost in move.Costs)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                cost.Resource.ToLowerInvariant(), move.ActionKind,
            };

            if (Gauges is not null && actor == _player && Gauges.Has(cost.Resource))
            {
                var pool = Gauges.Find(cost.Resource)!;
                pool.Spend(cost.Amount);

                // Tagged `gauge` so gauge feeds can exclude their own spends — without this,
                // spending Charge would raise the ResourceSpent that refills Charge.
                tags.Add("gauge");
                Publish(GameEvents.ResourceSpent, source: Id(actor), amount: cost.Amount, tags: tags);
                continue;
            }

            var resource = PoolNamed(actor, cost.Resource);
            if (resource is null)
                continue;

            resource.Reduce((int)Math.Round(cost.Amount, MidpointRounding.AwayFromZero));
            Publish(GameEvents.ResourceSpent, source: Id(actor), amount: cost.Amount, tags: tags);
        }
    }

    private void StartCooldown(Combatant actor, ResolvedMove move)
    {
        if (move.CooldownTicks > 0)
            _cooldowns[(actor, move.Id)] = _tick.CurrentTick + move.CooldownTicks;
    }

    public void Block() => EnterStance(DefensiveStance.Block);

    public void Dodge() => EnterStance(DefensiveStance.Dodge);

    /// <summary>Gear-granted (D-26): the composition root sets this from the worn items' tags.
    /// Without a parrying form equipped, the command does not exist.</summary>
    public bool PlayerCanParry { get; set; }

    /// <summary>The 3-tick negate-and-punish window (R4c-2). The top of the skill ladder —
    /// deliberately unreachable for an 8-tick auto-combat reaction (§5.1.1).</summary>
    public void Parry()
    {
        if (!IsActive)
            return;
        if (!PlayerCanParry)
        {
            Log("Nothing you carry can parry.");
            return;
        }
        if (_player.Stamina.Current < CombatTuning.ParryStaminaCost)
        {
            Log("Not enough stamina to parry.");
            return;
        }

        _player.Stamina.Reduce(CombatTuning.ParryStaminaCost);
        _player.ParryUntilTick = _tick.CurrentTick + CombatTuning.ParryWindowTicks;
        Log("You angle to parry.");
        Publish(GameEvents.ResourceSpent, source: SelfId, amount: CombatTuning.ParryStaminaCost,
            tags: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "stamina", "defensive", "parry" });
        StateChanged?.Invoke();
    }

    public void Wait()
    {
        if (!IsActive)
            return;
        Log("You wait, watching for an opening.");
    }

    /// <summary>
    /// Uses a healing item: restores Health now, at the cost of attack tempo (you can
    /// still block/dodge). Consuming the item from inventory is the caller's job.
    /// Returns the amount actually healed, or 0 if it could not be used.
    /// </summary>
    public int UseHealingItem(string label, int healAmount)
    {
        if (!IsActive)
            return 0;

        var healed = _player.Health.Restore(healAmount);
        _player.ReadyTick = Math.Max(_player.ReadyTick, _tick.CurrentTick + CombatTuning.ItemUseRecoveryTicks);
        Log($"You use {label} and recover {healed} Health. [{_player.Name} {_player.Health.Current}/{_player.Health.Max}]");
        Publish(GameEvents.Healed, source: SelfId, target: SelfId, amount: healed,
            tags: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "consumable" });
        StateChanged?.Invoke();
        return healed;
    }

    // --- Enemy AI -----------------------------------------------------------

    private void BeginEnemyDecision(Combatant enemy)
    {
        if (!IsActive || !enemy.IsAlive || enemy.Moveset.Count == 0)
            return;

        _recovery.Remove(enemy);

        // Stunned or Frozen: nothing at all. Feared: it will not attack, and with no positioning
        // to flee to, hesitating is what Fear means (docs/statuses.md §3.3). Either way it
        // re-checks shortly rather than being dropped from the loop forever.
        if (!CanAct(enemy))
        {
            _recovery[enemy] = _tick.Schedule(CombatTuning.StatusTickIntervalTicks, () => BeginEnemyDecision(enemy));
            return;
        }

        var move = ChooseMove(enemy);
        if (move is null)
        {
            // Everything is gated or unaffordable. Hesitate and re-check rather than dropping
            // out of the loop — the same bargain the CanAct path strikes.
            _recovery[enemy] = _tick.Schedule(CombatTuning.StatusTickIntervalTicks, () => BeginEnemyDecision(enemy));
            return;
        }

        PayCosts(enemy, move);
        StartCooldown(enemy, move);
        _lastMoveUsed[enemy] = move.Id; // feeds AvoidRepeatWeight on the next decision (M2′c)

        Commit(new ActionInFlight
        {
            Actor = enemy,
            Target = move.Targeting == Targeting.Self ? enemy : _player,
            Move = move,
            Tags = MoveEventTags(move),
        });

        Log($"{enemy.Name} begins {move.Name}!");
    }

    /// <summary>
    /// Weighted move selection over the actor's AI profile (docs/moves.md §5.2), for whoever
    /// is not choosing by hand. Returns null when everything is gated, unaffordable or on
    /// cooldown — the caller decides whether that means hesitate or do something else.
    ///
    /// <para>Public because auto-combat is <b>the player run through this same method</b>
    /// (GDD §5.7): an engaged <see cref="AutoCombatPilot"/> puts rules on
    /// <see cref="Combatant.Ai"/> and asks. Choosing is all it gets to do — it then issues
    /// <see cref="UseMove"/> like any other command, and the encounter resolves timing, costs,
    /// telegraphs and damage exactly as it does for a keyboard.</para>
    /// </summary>
    public ResolvedMove? ChooseMoveFor(Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return ChooseMove(actor);
    }

    /// <summary>
    /// Weighted move selection over the AI profile (docs/moves.md §5.2). AI chooses intent; the
    /// tick engine resolves timing. An empty profile is uniform over the moveset — exactly what
    /// the single uniform random draw this replaced did.
    /// </summary>
    private ResolvedMove? ChooseMove(Combatant actor)
    {
        var rules = actor.Ai.Count > 0
            ? actor.Ai
            : actor.Moveset.Select(m => new AiRuleSpec { Move = m.Id, Weight = 1.0 }).ToList();

        var decision = RequirementEvent(actor);
        var candidates = new List<(ResolvedMove Move, double Weight)>();
        _lastMoveUsed.TryGetValue(actor, out var lastMoveId);

        foreach (var rule in rules)
        {
            if (rule.Weight <= 0)
                continue;

            if (!rule.When.All(condition => TriggerRuleEngine.Evaluate(condition, decision, ConditionWorld)))
                continue;

            // A rule names a move by id, or by tag — the tag form is what lets one authored
            // brain serve many bodies (M2′c). Moveset order keeps expansion deterministic.
            var matches = !string.IsNullOrEmpty(rule.MoveTag)
                ? actor.Moveset.Where(m => m.HasTag(rule.MoveTag!))
                : actor.Moveset.Where(m => string.Equals(m.Id, rule.Move, StringComparison.Ordinal));

            foreach (var move in matches)
            {
                if (!CanUse(actor, move, out _))
                    continue;

                var weight = string.Equals(move.Id, lastMoveId, StringComparison.Ordinal)
                    ? rule.Weight * actor.AvoidRepeatWeight
                    : rule.Weight;
                if (weight <= 0)
                    continue;

                candidates.Add((move, weight));
            }
        }

        if (candidates.Count == 0)
            return null;

        // Deterministic weighted pick under the seed — same state, same roll, same choice.
        var total = candidates.Sum(c => c.Weight);
        var roll = _random.NextDouble() * total;

        foreach (var (move, weight) in candidates)
        {
            roll -= weight;
            if (roll < 0)
                return move;
        }

        return candidates[^1].Move;
    }

    // --- The action lifecycle -----------------------------------------------
    //
    // QUEUE → TELEGRAPH → WINDUP → EXECUTION → RECOVERY. Telegraph and windup are separate
    // scheduler states rather than one collapsed time-to-impact, which is what makes
    // "interrupt during windup" expressible at all (GDD §5.2). Total time to impact is
    // unchanged: telegraph + windup, exactly as before.

    private void Commit(ActionInFlight action)
    {
        _inFlight[action.Actor] = action;

        Publish(GameEvents.ActionQueued, Id(action.Actor), Id(action.Target), tags: action.Tags);

        var telegraph = Math.Max(0, action.Timing.TelegraphTicks);
        if (telegraph == 0)
        {
            // No telegraph: the action is already committed and swinging. Ambushes and instant
            // moves land here, and they are correspondingly harder to answer.
            EnterWindup(action);
        }
        else
        {
            action.Phase = ActionPhase.Telegraph;
            action.PhaseEndsTick = _tick.CurrentTick + telegraph;
            action.Scheduled = _tick.Schedule(telegraph, () => EnterWindup(action));
            Publish(GameEvents.ActionTelegraphed, Id(action.Actor), Id(action.Target), tags: action.Tags);
        }

        StateChanged?.Invoke();
    }

    private void EnterWindup(ActionInFlight action)
    {
        if (!IsActive || !_inFlight.TryGetValue(action.Actor, out var current) || !ReferenceEquals(current, action))
            return;
        if (!action.Actor.IsAlive)
        {
            _inFlight.Remove(action.Actor);
            return;
        }

        // `combat.windup.mult` finally applies here. It is what Chill *is* — the status is
        // literally `{ key: combat.windup.mult, value: 1.25 }` — and until the read path existed
        // the whole slow half of the status roster was authored, validated and inert.
        var windup = Math.Max(1, (int)Math.Round(
            action.Timing.WindupTicks * ModifierOn(action.Actor, ModifierKeys.WindupMult),
            MidpointRounding.AwayFromZero));

        action.Phase = ActionPhase.Windup;
        action.PhaseEndsTick = _tick.CurrentTick + windup;
        action.Scheduled = _tick.Schedule(windup, () => Execute(action));

        StateChanged?.Invoke();
    }

    private void Execute(ActionInFlight action)
    {
        if (!IsActive || !_inFlight.TryGetValue(action.Actor, out var current) || !ReferenceEquals(current, action))
            return;

        _inFlight.Remove(action.Actor);

        var attacker = action.Actor;
        var target = action.Move.Targeting == Targeting.Self
            ? attacker
            : attacker.Team == CombatTeam.Enemy ? _player : FirstAliveEnemy();

        if (!attacker.IsAlive || target is null)
            return;

        if (target.IsAlive)
            ResolveMove(attacker, target, action.Move, action.Tags, action.Context);

        if (!IsActive)
            return;

        // Enemies self-schedule their next decision; the player's tempo is their ReadyTick.
        if (attacker.Team == CombatTeam.Enemy && attacker.IsAlive)
        {
            var recovery = Math.Max(1, action.Timing.RecoveryTicks);
            _recovery[attacker] = _tick.Schedule(recovery, () => BeginEnemyDecision(attacker));
        }
    }

    /// <summary>
    /// The Execution phase: packets through the pipeline, stagger against Resolve, riders
    /// through the effect sink, chains to further targets. One path for a queued action, a
    /// triggered move and a recalled one — the difference is only the context they carry.
    /// </summary>
    private void ResolveMove(
        Combatant attacker, Combatant target, ResolvedMove move, HashSet<string> tags, EffectContext? context)
    {
        // `modifyMove` grants apply here rather than at build, because "the next attack is
        // empowered" is a statement about execution time.
        move = WithTimedModifiers(attacker, move);

        HitResult? result = null;

        if (move.Packets.Count > 0)
        {
            result = _pipeline.Resolve(new Hit
            {
                Source = attacker,
                Target = target,
                Name = move.Name,
                Packets = move.Packets,
                Tags = tags,
                StaggerPower = move.StaggerPower,
                Untelegraphed = move.Timing.TelegraphTicks <= 0,
            }, _tick.CurrentTick);

            ApplyResult(attacker, target, move.Name, result, tags, context);

            if (!target.IsAlive && HandleDefeat(attacker, target, context))
                return;

            // Stagger is control buildup toward Stun, resolved against the target's Resolve pool
            // (D-08). It rides the hit, so an avoided hit staggers nothing.
            if (move.StaggerPower > 0 && !result.Avoided && target.IsAlive)
                ApplyStatus(target, StunStatusId, Id(attacker), move.StaggerPower, context: context);

            // Chains: each jump hits the next living enemy at falloff. No positioning yet, so
            // "next" is roster order — the same simplification area damage makes.
            if (move.ChainTargets > 0 && !result.Avoided)
                ResolveChains(attacker, target, move, tags, context);
        }

        ExecuteRiders(attacker, target, move, result, tags, context);
    }

    private const string StunStatusId = "status.stun";

    private void ResolveChains(
        Combatant attacker, Combatant primary, ResolvedMove move, HashSet<string> tags, EffectContext? context)
    {
        var chained = _enemies.Where(e => e.IsAlive && e != primary).Take(move.ChainTargets).ToList();
        var falloff = CombatTuning.ChainFalloff;

        foreach (var (enemy, index) in chained.Select((e, i) => (e, i)))
        {
            var factor = Math.Pow(falloff, index + 1);
            var chainTags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase) { "mech:chain" };

            var result = _pipeline.Resolve(new Hit
            {
                Source = attacker,
                Target = enemy,
                Name = $"{move.Name} (chain)",
                Packets = move.Packets.Select(p => p.WithAmount(p.Amount * factor)).ToList(),
                Tags = chainTags,
                Untelegraphed = move.Timing.TelegraphTicks <= 0,
            }, _tick.CurrentTick);

            ApplyResult(attacker, enemy, move.Name, result, chainTags, context);

            if (!enemy.IsAlive && HandleDefeat(attacker, enemy, context))
                return;
        }
    }

    /// <summary>
    /// A move's riders, through the same registered handlers every rule effect uses. Each rider
    /// rolls its own chance; magnitude may scale off the landed amount (`scales_with: "amount"`).
    /// </summary>
    private void ExecuteRiders(
        Combatant attacker, Combatant target, ResolvedMove move, HitResult? result,
        HashSet<string> tags, EffectContext? context)
    {
        if (move.Effects.Count == 0 || EffectSink is null)
            return;

        var trigger = new GameEvent(
            GameEvents.MoveExecuted, Id(attacker), Id(target), result?.Amount ?? 0, tags,
            ChainId: context?.ChainId, Depth: context?.Depth ?? 0);

        foreach (var effect in move.Effects)
        {
            if (effect.Chance < 1.0 && _random.NextDouble() >= effect.Chance)
                continue;

            // A rider is the move's own payload, so it starts a chain (depth 0) unless the move
            // itself was triggered by a proc — then it stays on that chain at that depth, which
            // is what keeps triggerMove's budget honest.
            var riderContext = context ?? EffectSink.NewChain(Id(attacker));

            EffectSink.Execute(new EffectInvocation(effect, effect.Magnitude(trigger), trigger, move.Id)
            {
                // An explicit per-effect target wins (Drain's heal names TriggerSource);
                // otherwise the rider inherits the move's own targeting.
                Target = effect.Target ?? DefaultRiderTarget(move.Targeting),
                Context = riderContext,
            });
        }
    }

    /// <summary>Where a move's rider lands when the effect does not name a target of its own.</summary>
    private static EffectTarget DefaultRiderTarget(Targeting targeting) => targeting switch
    {
        Targeting.Self => EffectTarget.Self,
        Targeting.AllEnemies => EffectTarget.AllEnemies,
        _ => EffectTarget.TriggerTarget,
    };

    /// <summary>Applies any active `modifyMove` grants matching this move, at execution time.</summary>
    private ResolvedMove WithTimedModifiers(Combatant actor, ResolvedMove move)
    {
        var matching = _timedMoveModifiers
            .Where(m => m.Owner == actor && m.Definition.Match.Matches(move.Source))
            .ToList();

        if (matching.Count == 0)
            return move;

        var ops = matching.SelectMany(m => m.Definition.Ops).ToList();
        var provenance = move.Provenance.Concat(matching.Select(m => $"{m.Source} ({m.Definition.Id}, timed)")).ToList();

        return MovesetBuilder.Apply(move.Snapshot(), ops, provenance);
    }

    // --- The move-granting effects (docs/moves.md §3.4) ----------------------

    /// <summary>
    /// Executes a move immediately — no telegraph, no windup, no cost, no cooldown — at the
    /// chain's next depth. What `triggerMove` and `recallMove` resolve through.
    /// </summary>
    public bool TriggerMove(Combatant caster, string moveId, EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(context);

        if (!IsActive)
            return false;

        // The caster's OWN resolved version first — a recalled Iron Slash is YOUR Iron Slash,
        // stormbrand conversion and all, not the store's unmodified one. The raw definition is
        // the fallback for moves the caster never had. (Found by reading a trace: the replayed
        // slash had quietly lost its charge packet.)
        var move = caster.Moveset
            .Concat(_grantedMoves.Where(g => g.Owner == caster).Select(g => g.Move))
            .FirstOrDefault(m => string.Equals(m.Id, moveId, StringComparison.Ordinal));

        if (move is null)
        {
            if (!_moves.TryGetById(moveId, out var definition))
                return false;
            move = MovesetBuilder.Apply(definition, Array.Empty<MoveOpSpec>(), new[] { $"triggered ({context.ImmediateSource})" });
        }

        var target = move.Targeting == Targeting.Self
            ? caster
            : caster.Team == CombatTeam.Enemy ? _player : FirstAliveEnemy();

        if (target is null || !target.IsAlive)
            return false;

        Log($"{caster.Name}'s {move.Name} triggers!");

        // "…at depth+1" (docs/moves.md §3.4): the triggered move's events sit one generation
        // deeper than the effect that triggered it, so its riders' procs hit the ceiling.
        ResolveMove(caster, target, move, MoveEventTags(move), context.Next(Id(caster)));
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>Replays the caster's stored move (Mnemonic), consuming the recall status.</summary>
    public bool RecallMove(Combatant caster, EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var stored = Statuses?.Find(caster, RecalledMoveStatusId);
        if (stored?.StoredMoveId is not { } moveId || string.IsNullOrEmpty(moveId))
            return false;

        Statuses!.Remove(caster, RecalledMoveStatusId);
        return TriggerMove(caster, moveId, context);
    }

    public const string RecalledMoveStatusId = "status.recalled_move";

    /// <summary>Adds a move to a combatant's usable set for a duration (0 = the encounter).</summary>
    public bool GrantMove(Combatant owner, string moveId, string source, int durationTicks, EffectContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (!IsActive || !_moves.TryGetById(moveId, out var definition))
            return false;

        var move = MovesetBuilder.Apply(definition, Array.Empty<MoveOpSpec>(), new[] { $"{source} (granted)" });
        var expires = durationTicks > 0 ? _tick.CurrentTick + durationTicks : long.MaxValue;
        _grantedMoves.Add((owner, move, expires));

        Log($"{owner.Name} gains {move.Name}.");
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>Attaches a move modifier to a combatant's future executions for a duration.</summary>
    public bool AttachMoveModifier(Combatant owner, string modifierId, string source, int durationTicks)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (!IsActive || _moveModifiers is null || !_moveModifiers.TryGetById(modifierId, out var definition))
            return false;

        var expires = durationTicks > 0 ? _tick.CurrentTick + durationTicks : long.MaxValue;
        _timedMoveModifiers.Add((owner, definition, source, expires));
        return true;
    }

    /// <summary>
    /// Books a defeat: raises the pair of events, and ends the encounter when that was the last
    /// enemy or the player.
    /// </summary>
    /// <returns>True if the encounter ended, so the caller stops.</returns>
    private bool HandleDefeat(Combatant attacker, Combatant target, EffectContext? context = null)
    {
        Publish(GameEvents.Killed, source: Id(attacker), target: Id(target), context: context);
        Publish(GameEvents.Defeated, source: Id(target), target: Id(attacker), context: context);

        if (target.Team == CombatTeam.Player)
        {
            EndCombat(CombatResult.Defeat);
            return true;
        }

        _defeatedEnemies.Add(target);
        Log($"{target.Name} is defeated!");
        CancelActionsOf(target);

        if (_enemies.All(e => !e.IsAlive))
        {
            EndCombat(CombatResult.Victory);
            return true;
        }

        return false;
    }

    // --- Effect entry points (E3c) ------------------------------------------
    //
    // What the registered effect handlers call. Each takes the EffectContext and hands it to
    // Publish, which is the only reason the depth budget, the once-per-chain rule and the fuse
    // mean anything at runtime.

    /// <summary>
    /// Damage dealt by an effect rather than by a swing.
    ///
    /// <para>It deliberately does <b>not</b> run the hit pipeline: "deal 12" is a flat number, not
    /// an attack, so it takes no armour, no crit and no avoidance. Running it through the pipeline
    /// would make a proc dodgeable, which is not what any of the authored content means. It
    /// <i>can</i> trigger — the depth budget is what bounds it, not a blanket ban.</para>
    /// </summary>
    /// <returns>Damage actually applied.</returns>
    public int DealEffectDamage(
        Combatant source, Combatant target, double amount, string label, EffectContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var applied = (int)Math.Round(Math.Max(0, amount), MidpointRounding.AwayFromZero);
        if (!IsActive || applied <= 0 || !target.IsAlive)
            return 0;

        ReduceWithBarrier(target, applied, context);

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "effect" };
        Publish(GameEvents.DamageDealt, Id(source), Id(target), applied, tags, context);
        Publish(GameEvents.DamageTaken, Id(target), Id(source), applied, tags, context);

        Log($"{label} hits {target.Name} for {applied}. [{target.Name} {target.Health.Current}/{target.Health.Max}]");

        if (!target.IsAlive)
            HandleDefeat(source, target, context);

        StateChanged?.Invoke();
        return applied;
    }

    /// <summary>Healing from an effect. Capped by the pool, and reports what actually landed.</summary>
    public int HealTarget(Combatant target, double amount, string label, EffectContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        var requested = (int)Math.Round(Math.Max(0, amount), MidpointRounding.AwayFromZero);
        if (!IsActive || requested <= 0 || !target.IsAlive)
            return 0;

        var before = target.Health.Current;
        target.Health.Restore(requested);
        var healed = target.Health.Current - before;
        if (healed <= 0)
            return 0;

        Publish(GameEvents.Healed, Id(target), Id(target), healed, context: context);
        Log($"{label} restores {healed} health to {target.Name}.");
        StateChanged?.Invoke();
        return healed;
    }

    /// <summary>
    /// Fills a named gauge, or a combatant pool when the name is one.
    ///
    /// <para>Every authored <c>grantResource</c> names a gauge, which is why gauges had to exist
    /// before this handler could do anything. Pools are handled too because the vocabulary does
    /// not stop content naming Stamina, and silently dropping it would be the failure mode the
    /// whole <c>Unhandled</c> list exists to prevent.</para>
    /// </summary>
    /// <returns>How much was actually added.</returns>
    public double GrantResource(Combatant target, string resource, double amount, EffectContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!IsActive || amount <= 0 || string.IsNullOrWhiteSpace(resource))
            return 0;

        double granted;

        if (Gauges?.Has(resource) == true)
        {
            granted = Gauges.Add(resource, amount, _tick.CurrentTick);
        }
        else
        {
            var pool = PoolNamed(target, resource);
            if (pool is null)
                return 0;

            var before = pool.Current;
            pool.Restore((int)Math.Round(amount, MidpointRounding.AwayFromZero));
            granted = pool.Current - before;
        }

        if (granted <= 0)
            return 0;

        Publish(GameEvents.ResourceGenerated, Id(target), Id(target), granted,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { resource.ToLowerInvariant() }, context);

        StateChanged?.Invoke();
        return granted;
    }

    private static Characters.ResourcePool? PoolNamed(Combatant combatant, string name) => name.ToLowerInvariant() switch
    {
        "health" => combatant.Health,
        "stamina" => combatant.Stamina,
        "mana" => combatant.Mana,
        _ => null,
    };

    /// <summary>
    /// A timing modifier for <paramref name="combatant"/>, or 1 when there is no read path.
    ///
    /// <para>Timing keys are unscoped, so the context is genuinely <see cref="ModifierContext.None"/>
    /// rather than a placeholder — a windup belongs to the actor, not to a lane or a move tag.
    /// </para>
    /// </summary>
    private double ModifierOn(Combatant combatant, string key) =>
        Modifiers?.Resolve(combatant, key, ModifierContext.None) ?? 1.0;

    /// <summary>
    /// The combatant an event id refers to — <see cref="SelfId"/> or an enemy's name. Null for a
    /// non-combatant source such as "world", and null before an encounter has started.
    /// </summary>
    public Combatant? Find(string? id)
    {
        if (string.IsNullOrEmpty(id) || _player is null)
            return null;

        return string.Equals(Id(_player), id, StringComparison.Ordinal)
            ? _player
            : _enemies.FirstOrDefault(enemy => string.Equals(Id(enemy), id, StringComparison.Ordinal));
    }

    /// <summary>
    /// Cuts an action short. Returns false if the combatant has nothing committed.
    ///
    /// <para>This is the capability the telegraph/windup split exists to enable — Stun, Psionic's
    /// Overload and the interrupt family all land here in E2b/E3. The <c>phase</c> tag on
    /// <see cref="GameEvents.ActionInterrupted"/> lets content distinguish "stopped them before
    /// they swung" from "stopped them mid-swing".</para>
    /// </summary>
    public bool Interrupt(Combatant actor, string reason = "interrupted", EffectContext? context = null)
    {
        if (!IsActive || !_inFlight.TryGetValue(actor, out var action))
            return false;

        // An interrupt-immune move shrugs it off (E4) — "the Juggernaut ignores interrupts" is a
        // move property, not a global rule (docs/moves.md §2.3). The modifier flag reads the
        // same answer, so `setFlag uninterruptible` and an authored `interruptible: false` agree.
        if (!action.Move.Interruptible
            || (Modifiers is not null && Modifiers.For(actor).IsSet(ModifierKeys.InterruptImmune, ModifierContext.None)))
        {
            Log($"{actor.Name}'s {action.Name} cannot be stopped!");
            return false;
        }

        _inFlight.Remove(actor);
        if (action.Scheduled is not null)
            _tick.Cancel(action.Scheduled.Id);

        var tags = new HashSet<string>(action.Tags, StringComparer.OrdinalIgnoreCase)
        {
            action.Phase == ActionPhase.Telegraph ? "telegraph" : "windup",
        };

        Log($"{actor.Name}'s {action.Name} is {reason}!");
        Publish(GameEvents.ActionInterrupted, source: Id(actor), target: Id(action.Target), tags: tags, context: context);

        // The interrupted actor still pays recovery — being stopped is not free tempo.
        if (actor.Team == CombatTeam.Enemy && actor.IsAlive)
        {
            var recovery = Math.Max(1, action.Timing.RecoveryTicks);
            _recovery[actor] = _tick.Schedule(recovery, () => BeginEnemyDecision(actor));
        }

        StateChanged?.Invoke();
        return true;
    }

    // --- Shared -------------------------------------------------------------

    /// <summary>
    /// Raises a timed defensive stance. Both stances cost stamina and last a fixed window; the
    /// difference is which window the hit pipeline reads, and that Block additionally opens the
    /// Perfect Block sub-window.
    /// </summary>
    private void EnterStance(DefensiveStance stance)
    {
        if (!IsActive)
            return;

        var isBlock = stance == DefensiveStance.Block;
        var verb = isBlock ? "raise your guard" : "dodge";
        var staminaCost = isBlock ? CombatTuning.BlockStaminaCost : CombatTuning.DodgeStaminaCost;

        if (_player.Stamina.Current < staminaCost)
        {
            Log($"Not enough stamina to {verb}.");
            return;
        }

        _player.Stamina.Reduce(staminaCost);
        var stanceEndsTick = _tick.CurrentTick
            + (isBlock ? CombatTuning.BlockDurationTicks : CombatTuning.DodgeDurationTicks);

        if (isBlock)
        {
            _player.BlockUntilTick = stanceEndsTick;
            _player.BlockStartTick = _tick.CurrentTick;   // the Perfect Block window starts here
        }
        else
        {
            _player.DodgeUntilTick = stanceEndsTick;
        }

        Log($"You {verb}.");
        Publish(GameEvents.ResourceSpent, source: SelfId, amount: staminaCost,
            tags: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "stamina", "defensive", isBlock ? "block" : "dodge" });
        StateChanged?.Invoke();
    }

    private void ApplyResult(
        Combatant attacker, Combatant target, string attackName, HitResult result, HashSet<string> tags,
        EffectContext? context = null)
    {
        // Copy before touching it. The caller hands in the ActionInFlight's own tag set, which
        // was already given to ActionQueued/ActionTelegraphed — and a GameEvent holds the
        // reference rather than a snapshot. Adding `blocked` to the original therefore rewrote
        // the tags of events published before the block existed, so anything that *records*
        // events (the Hit Log, the Lab's recent-firings panel, any replay) read a history that
        // never happened. Live matching never saw it, because the bus dispatches synchronously.
        tags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);

        // Lanes, in the `family:value` convention, so `hitHasLane` needs no world and a lane can
        // never be confused with an ordinary tag. A hit already knows what it arrived as; making
        // content ask the world for that would be asking the wrong thing.
        foreach (var lane in result.Packets.Select(p => p.Lane).Where(l => l is not null).Distinct())
            tags.Add(RuleVocabulary.LaneTagPrefix + lane);

        var source = Id(attacker);
        var victim = Id(target);

        LastHit = result;
        HitResolved?.Invoke(result);

        // The attack happened regardless of how it landed — this is what MoveExecuted means, and
        // it is the single most-referenced event in shipped content (18 hooks).
        Publish(GameEvents.MoveExecuted, source, victim, result.Amount, tags, context);
        Publish(GameEvents.ActionResolved, source, victim, result.Amount, tags, context);

        // `Blocked` is raised from the *defender's* perspective (source is who blocked) and for
        // BOTH block outcomes (D-06 §6.2). Hooking on-block affixes to a landed hit instead would
        // mean a *perfect* block produced no retaliation — punishing the better play.
        if (result.Blocked)
        {
            tags.Add("blocked");
            if (result.PerfectBlock)
                tags.Add("perfect");
            Publish(GameEvents.Blocked, source: victim, target: source, amount: result.Amount, tags: tags, context: context);
        }

        if (result.Avoided)
        {
            var avoidanceVerb = result.AvoidedBy == AvoidedVia.Parry ? "parries"
                : result.PerfectBlock ? "blocks perfectly"
                : result.AvoidedBy == AvoidedVia.Evade ? "evades"
                : "dodges";
            Log($"{target.Name} {avoidanceVerb} {attacker.Name}'s {attackName}!");

            // Dodged is the only avoidance event in E0's vocabulary; E1 does not widen it.
            // HitAvoided arrives in E3 with the rest of §3.1.
            if (result.Dodged)
                Publish(GameEvents.Dodged, source: victim, target: source, tags: tags, context: context);

            // PARRIED (R4c-2): negation plus the counter-window — heavy stagger on the
            // attacker, as Stun buildup through the ordinary Resolve gate (D-08).
            if (result.AvoidedBy == AvoidedVia.Parry)
            {
                Publish(GameEvents.Parried, source: victim, target: source, tags: tags, context: context);
                ApplyStatus(attacker, StunStatusId, victim, CombatTuning.ParryStaggerPower, context: context);
                Log($"{attacker.Name} is thrown open by the parry!");
            }

            StateChanged?.Invoke();
            return;
        }

        if (result.Crit)
            tags.Add("critical");

        // DAMAGE MITIGATED (R4c-2, D-06 §6.3): the prevented total, published before the wound
        // lands — the basis for reflect-% retaliation (and stored retaliation, later).
        if (result.Blocked && result.Mitigated > 0.5)
            Publish(GameEvents.DamageMitigated, source: victim, target: source,
                amount: result.Mitigated, tags: tags, context: context);

        var absorbed = ReduceWithBarrier(target, result.Amount, context);

        Publish(GameEvents.DamageDealt, source, victim, result.Amount, tags, context);
        Publish(GameEvents.DamageTaken, source: victim, target: source, amount: result.Amount, tags: tags, context: context);

        var outcomeNotes = (result.Crit ? " (crit!)" : string.Empty)
            + (result.Blocked ? " (blocked)" : string.Empty)
            + (absorbed > 0 ? $" (barrier absorbs {absorbed})" : string.Empty);
        Log($"{attacker.Name}'s {attackName} hits {target.Name} for {result.Amount} {result.Type}{outcomeNotes}. " +
            $"[{target.Name} {target.Health.Current}/{target.Health.Max}]");

        // Ailments resolve LAST and from the damage that actually landed in each lane — so the
        // target's lane resistance reduces the ailment with the same number that reduced the hit
        // (docs/damage-and-defense.md §3.2, stage 26). No second calculation, no second stat.
        ApplyLaneAilments(attacker, target, result);

        StateChanged?.Invoke();
    }

    private void EndCombat(CombatResult result)
    {
        if (!IsActive)
            return;
        IsActive = false;

        CancelAll();

        Log(result == CombatResult.Victory ? "Victory!" : "You have been defeated.");
        Publish(GameEvents.EncounterEnded, source: SelfId, amount: _defeatedEnemies.Count,
            tags: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { result == CombatResult.Victory ? "victory" : "defeat" });
        Ended?.Invoke(new CombatOutcome { Result = result, DefeatedEnemies = _defeatedEnemies.ToList() });
        StateChanged?.Invoke();
    }

    private void ScheduleStaminaRegen()
    {
        _regenPending = _tick.Schedule(CombatTuning.StaminaRegenIntervalTicks, () =>
        {
            if (!IsActive)
                return;
            foreach (var combatant in Combatants.Where(c => c.IsAlive))
                combatant.Stamina.Restore(CombatTuning.StaminaRegenAmount);
            StateChanged?.Invoke();
            ScheduleStaminaRegen();
        });
    }

    /// <summary>
    /// Advances every status on the shared clock.
    ///
    /// <para>Statuses ride one periodic sweep rather than each scheduling itself, so ordering
    /// stays deterministic under a seed — twenty poison stacks resolving in registration order
    /// beats twenty independent timers racing.</para>
    /// </summary>
    private void ScheduleStatusTick()
    {
        if (Statuses is null && Gauges is null)
            return;

        _statusPending = _tick.Schedule(CombatTuning.StatusTickIntervalTicks, () =>
        {
            if (!IsActive)
                return;
            Statuses?.Advance(Combatants.Where(c => c.IsAlive).ToList());

            // Gauges ride the same sweep for the same reason statuses do — decay resolving on one
            // deterministic clock beats every meter racing its own timer.
            Gauges?.Advance(_tick.CurrentTick);
            Modifiers?.Timed.Advance(_tick.CurrentTick);
            _grantedMoves.RemoveAll(g => g.ExpiresTick <= _tick.CurrentTick);
            _timedMoveModifiers.RemoveAll(m => m.ExpiresTick <= _tick.CurrentTick);
            ScheduleStatusTick();
        });
    }

    // --- Events -------------------------------------------------------------
    //
    // Combat is the only authoritative source of these; the UI and the rule engine both
    // observe. Every event carries the player's health/stamina fractions so the shipped
    // `selfHealthBelow` / `selfHealthAbove` conditions work without the rule engine needing a
    // back-reference into combat, and an `encounter_index` so `firstInEncounter` works.
    //
    // E0 raises only vocabulary that ALREADY EXISTS in `GameEvents`. The additions in
    // docs/effect-foundation.md §3.1 (HitLanded, HitAvoided, DamageMitigated, …) arrive with
    // the packet pipeline in E1, together with the semantics that make them distinct.

    /// <param name="context">
    /// Set when this event is being raised <i>by an effect</i> rather than by a real action.
    /// Carrying it is what keeps the causal chain intact: drop it once and the chain restarts at
    /// depth 0, and the entire proc budget becomes decorative (docs/effect-foundation.md §6.1).
    /// </param>
    /// <param name="canTrigger">
    /// False bars the event from matching any rule at all. Retaliation and ailment ticks use it —
    /// between them, most of the recursion the design has to survive (§6.2).
    /// </param>
    private void Publish(string kind, string? source = null, string? target = null,
        double amount = 0.0, IReadOnlySet<string>? tags = null,
        EffectContext? context = null, bool canTrigger = true)
    {
        _eventCounts.TryGetValue(kind, out var seen);
        _eventCounts[kind] = seen + 1;

        _bus.Publish(new GameEvent(kind, source, target, amount, tags, new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["self_health_fraction"] = Fraction(_player.Health),
            ["self_stamina_fraction"] = Fraction(_player.Stamina),
            ["gauge_fraction"] = Gauges?.HighestFraction ?? 0.0,
            ["encounter_index"] = seen + 1,
        },
        ChainId: context?.ChainId,
        Depth: context?.Depth ?? 0,
        CanTrigger: canTrigger));
    }

    private static double Fraction(Characters.ResourcePool pool) =>
        pool.Max <= 0 ? 0.0 : (double)pool.Current / pool.Max;

    /// <summary>Identity in events — see <see cref="SelfId"/>.</summary>
    private static string Id(Combatant combatant) =>
        combatant.Team == CombatTeam.Player ? SelfId : combatant.Name;

    /// <summary>
    /// The tags a move's events carry: the move's own namespaced tags, its id, and the <b>bare
    /// legacy aliases</b>.
    ///
    /// <para>The aliases are load-bearing: 23 shipped `hasTag` conditions match bare tags —
    /// `heavy`, `melee`, `defensive`, damage-type names — authored before the namespaced
    /// vocabulary existed. Dropping them would kill every one of those hooks silently, which is
    /// the exact failure mode this project keeps refusing to accept. `heavy` stays derived from
    /// time-to-impact (never authored), and `move:&lt;id&gt;` lets a condition target one
    /// specific move.</para>
    /// </summary>
    private static HashSet<string> MoveEventTags(ResolvedMove move)
    {
        var tags = new HashSet<string>(move.Tags, StringComparer.OrdinalIgnoreCase)
        {
            "move:" + move.Id,
            move.Timing.TimeToImpactTicks >= CombatTuning.HeavyTimeToImpactTicks ? "heavy" : "light",
        };

        // Bare aliases for every namespaced value: `action:attack` also reads as `attack`.
        foreach (var tag in move.Tags)
        {
            var colon = tag.IndexOf(':');
            if (colon > 0 && colon < tag.Length - 1)
                tags.Add(tag[(colon + 1)..]);
        }

        // Damage-type names, lowercased — `slashing`, `crushing` — from the packets.
        foreach (var packet in move.Packets)
            tags.Add(packet.Type.ToString().ToLowerInvariant());

        return tags;
    }

    // --- Statuses -----------------------------------------------------------

    /// <summary>
    /// Applies whatever ailments the attacker's per-lane chances call for, sized by the damage
    /// that landed in that lane.
    ///
    /// <para>E1 left the application *chances* with no source — no affix grants them until E5.
    /// The plumbing exists so the magnitude rule is pinned now: an ailment is worth a fraction of
    /// the post-mitigation damage in its own lane, which is why resistance reduces both with one
    /// number.</para>
    /// </summary>
    private void ApplyLaneAilments(Combatant attacker, Combatant target, HitResult result)
    {
        if (Statuses is null || result.Amount <= 0)
            return;

        foreach (var packet in result.Packets)
        {
            if (packet.Lane is not { } lane || packet.Amount <= 0)
                continue;

            var chance = attacker.AilmentChanceFor(lane);
            if (chance <= 0 || _random.NextDouble() >= chance)
                continue;

            var statusId = AilmentForLane(lane);
            if (statusId is null || !Statuses.Definitions.TryGetById(statusId, out var definition))
                continue;

            var magnitude = definition.Magnitude.Basis == MagnitudeBasis.LaneDamage
                ? packet.Amount * definition.Magnitude.Coefficient
                : definition.Magnitude.Coefficient;

            ApplyStatus(target, statusId, Id(attacker), magnitude);
        }
    }

    /// <summary>
    /// The signature ailment of each lane — the test for whether a lane earns its place
    /// (docs/damage-and-defense.md §4.1). Hard-coded rather than data because it *is* the lane
    /// vocabulary: a lane without an ailment identity should not exist.
    /// </summary>
    private static string? AilmentForLane(string lane) => lane switch
    {
        DamageLanes.Physical => "status.bleed",
        DamageLanes.Heat => "status.burn",
        DamageLanes.Toxin => "status.poison",
        DamageLanes.Cold => "status.chill",
        DamageLanes.Charge => "status.shock",
        DamageLanes.Corrosion => "status.corroded",
        _ => null,
    };

    /// <summary>
    /// Whether a combatant may act at all, and with what. Stun and Freeze stop everything;
    /// Fear forbids attacks specifically (it changes *what* the target does rather than
    /// stopping it, which is what makes it a soft control without positioning); Silence
    /// forbids spells.
    /// </summary>
    public bool CanAct(Combatant combatant, string actionTag = "attack")
    {
        if (Statuses is null)
            return true;

        if (Statuses.Has(combatant, "status.stun") || Statuses.Has(combatant, "status.freeze"))
            return false;

        if (actionTag == "attack" && Statuses.Has(combatant, "status.fear"))
            return false;

        if (actionTag == "spell" && Statuses.Has(combatant, "status.silence"))
            return false;

        return true;
    }

    /// <summary>Barrier absorption (R4c-2, the long-standing HitPipeline debt): `status.barrier`
    /// magnitude soaks damage before Health does. Recovery is Barrier, not healing (§5.5).
    /// Returns the amount absorbed; shattering raises <c>BarrierBroken</c> (D-06's sixth event).</summary>
    private int ReduceWithBarrier(Combatant target, int amount, EffectContext? context)
    {
        var barrier = Statuses?.Find(target, BarrierStatusId);
        if (barrier is null || barrier.Magnitude <= 0)
        {
            target.Health.Reduce(amount);
            return 0;
        }

        var absorbed = (int)Math.Min(barrier.Magnitude, amount);
        barrier.Magnitude -= absorbed;
        if (barrier.Magnitude <= 0)
        {
            Statuses!.Remove(target, BarrierStatusId);
            Publish(GameEvents.BarrierBroken, source: Id(target), target: Id(target), amount: absorbed, context: context);
            Log($"{target.Name}'s barrier shatters!");
        }

        target.Health.Reduce(amount - absorbed);
        return absorbed;
    }

    private const string BarrierStatusId = "status.barrier";

    /// <summary>
    /// Applies a status through the controller, publishing whatever the attempt produced. The
    /// controller decides whether a control actually lands; combat only reports it.
    /// </summary>
    public ControlOutcome ApplyStatus(
        Combatant target, string statusId, string sourceId, double magnitude = 0,
        int durationOverride = 0, EffectContext? context = null, string? storedMoveId = null)
    {
        if (Statuses is null)
            return ControlOutcome.Ungated;

        // R4c-2 — the status depth keys. MaterialStrength scales the applier's magnitude; duration
        // scales on the receiver (defensive "shorter suffering"). Both scoped by status id,
        // both no-ops at their defaults, so nothing changes without a source.
        var applier = Find(sourceId);
        var statusContext = ModifierContext.For(ScopeDimensions.Status, statusId);
        if (magnitude > 0 && applier is not null && Modifiers is not null)
            magnitude *= Modifiers.Resolve(applier, ModifierKeys.StatusPotencyMult, statusContext, 1.0);

        var durationMultiplier = Modifiers is null
            ? 1.0
            : Modifiers.Resolve(target, ModifierKeys.StatusDurationMult, statusContext, 1.0);

        var outcome = Statuses.Apply(
            target, statusId, sourceId, magnitude, durationOverride, context, storedMoveId, durationMultiplier);

        if (outcome == ControlOutcome.Applied)
            Log($"{target.Name} is affected by {Statuses.Find(target, statusId)?.Definition.Name ?? statusId}.");

        StateChanged?.Invoke();
        return outcome;
    }

    /// <summary>
    /// A damage-over-time tick. Raises <c>DamageTaken</c> but deliberately <b>never</b>
    /// <c>HitLanded</c>/<c>MoveExecuted</c> — a Poison tick is not a hit, so it cannot proc
    /// thorns. That single rule kills an entire class of DoT-driven proc loops
    /// (docs/damage-and-defense.md §6.3).
    /// </summary>
    private void OnStatusTick(Combatant target, StatusInstance instance)
    {
        if (!IsActive || instance.Magnitude <= 0)
            return;

        var amount = (int)Math.Round(instance.Magnitude * instance.Stacks, MidpointRounding.AwayFromZero);
        if (amount <= 0)
            return;

        target.Health.Reduce(amount);

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ailment", instance.Id,
        };
        if (instance.Definition.Lane is { } lane)
            tags.Add(lane);

        Log($"{target.Name} suffers {amount} from {instance.Definition.Name}. " +
            $"[{target.Name} {target.Health.Current}/{target.Health.Max}]");

        // canTrigger: false is proc-safety rule 4 (docs/effect-foundation.md §6.2). It only
        // started mattering once E3c registered handlers: before that a Poison tick fed nothing,
        // and after it a twenty-second DoT would otherwise proc every rule in the build twice a
        // second, for free, from one application.
        Publish(GameEvents.DamageTaken, source: Id(target), target: instance.SourceId, amount: amount,
            tags: tags, canTrigger: false);

        if (!target.IsAlive)
        {
            Publish(GameEvents.Defeated, source: Id(target), target: instance.SourceId);
            if (target.Team == CombatTeam.Player)
            {
                EndCombat(CombatResult.Defeat);
            }
            else
            {
                _defeatedEnemies.Add(target);
                Log($"{target.Name} is defeated!");
                CancelActionsOf(target);
                if (_enemies.All(e => !e.IsAlive))
                    EndCombat(CombatResult.Victory);
            }
        }

        StateChanged?.Invoke();
    }

    private Combatant? FirstAliveEnemy() => _enemies.FirstOrDefault(e => e.IsAlive);

    /// <summary>Drops a combatant's in-flight action and recovery timer — used on death.</summary>
    private void CancelActionsOf(Combatant combatant)
    {
        if (_inFlight.TryGetValue(combatant, out var action))
        {
            if (action.Scheduled is not null)
                _tick.Cancel(action.Scheduled.Id);
            _inFlight.Remove(combatant);
        }

        if (_recovery.TryGetValue(combatant, out var pending))
        {
            _tick.Cancel(pending.Id);
            _recovery.Remove(combatant);
        }
    }

    private void CancelAll()
    {
        foreach (var action in _inFlight.Values)
            if (action.Scheduled is not null)
                _tick.Cancel(action.Scheduled.Id);
        _inFlight.Clear();

        foreach (var pending in _recovery.Values)
            _tick.Cancel(pending.Id);
        _recovery.Clear();

        if (_regenPending is not null)
            _tick.Cancel(_regenPending.Id);
        _regenPending = null;

        if (_statusPending is not null)
            _tick.Cancel(_statusPending.Id);
        _statusPending = null;

        // Resolve escalation is per-encounter by design, so it goes with everything else.
        Statuses?.Clear();
    }

    private void Log(string message) => Logged?.Invoke(message);
}
