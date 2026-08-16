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
/// <para>Player and enemy share this because the phases are the same for both; the divergence
/// is only in who chooses the action. E4's <c>MoveDefinition</c> collapses that difference too.</para>
/// </summary>
public sealed class ActionInFlight
{
    public required Combatant Actor { get; init; }
    public required Combatant Target { get; init; }
    public required string Name { get; init; }
    public required DamageType DamageType { get; init; }
    public required double BaseDamage { get; init; }
    public required AbilityTiming Timing { get; init; }
    public required HashSet<string> Tags { get; init; }

    /// <summary>Null for the player, whose action comes from the equipped weapon.</summary>
    public AbilityDefinition? Ability { get; init; }

    public ActionPhase Phase { get; internal set; } = ActionPhase.Telegraph;

    /// <summary>Tick the current phase ends. During Windup this is the moment of impact.</summary>
    public long PhaseEndsTick { get; internal set; }

    internal ScheduledAction? Scheduled { get; set; }
}

/// <summary>A pending enemy attack the player can see and react to.</summary>
public sealed class EnemyIntent
{
    public required Combatant Attacker { get; init; }
    public required AbilityDefinition Ability { get; init; }
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
/// Authoritative tick-driven encounter (docs/combat-spec.md). Enemies run a
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
    private readonly CombatCalculator _calculator;
    private readonly DataStore<AbilityDefinition> _abilities;
    private readonly IRandomSource _rng;
    private readonly IGameEventBus _bus;
    private readonly string _playerFallbackAbilityId;

    /// <summary>Every action currently between commitment and impact, both sides.</summary>
    private readonly Dictionary<Combatant, ActionInFlight> _inFlight = new();

    /// <summary>Enemy recovery timers — separate because they exist *between* actions.</summary>
    private readonly Dictionary<Combatant, ScheduledAction> _recovery = new();

    private readonly List<Combatant> _defeatedEnemies = new();
    private readonly Dictionary<string, int> _eventCounts = new(StringComparer.Ordinal);
    private ScheduledAction? _regenPending;
    private ScheduledAction? _statusPending;

    private Combatant _player = null!;
    private List<Combatant> _enemies = new();

    public CombatEncounter(
        TickEngine tick,
        CombatCalculator calculator,
        DataStore<AbilityDefinition> abilities,
        IRandomSource rng,
        IGameEventBus bus,
        string playerFallbackAbilityId,
        StatusController? statuses = null,
        Characters.GaugeController? gauges = null,
        CombatantModifiers? modifiers = null)
    {
        _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _playerFallbackAbilityId = playerFallbackAbilityId;

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

    /// <summary>The player's basic attack: the equipped-weapon profile, or the fallback ability if unarmed.</summary>
    private AttackProfile PlayerAttackProfile =>
        _player.Attack ?? ToProfile(_abilities.GetById(_playerFallbackAbilityId));

    private static AttackProfile ToProfile(AbilityDefinition ability) => new()
    {
        Name = ability.Name,
        DamageType = ability.DamageType,
        BaseDamage = ability.BaseValue,
        StaminaCost = ability.StaminaCost,
        Timing = ability.Timing,
    };

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
        .Where(a => a.Actor.Team == CombatTeam.Enemy && a.Ability is not null)
        .Select(a => new EnemyIntent
        {
            Attacker = a.Actor,
            Ability = a.Ability!,
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
        _defeatedEnemies.Clear();
        _eventCounts.Clear();
        IsActive = true;
        _player.ReadyTick = _tick.CurrentTick;

        // Gauges are per-encounter: Charge earned against the last pack does not carry into the
        // next fight, or the meter stops being something you build inside a fight.
        Gauges?.Reset(_tick.CurrentTick);
        Modifiers?.Timed.Clear();

        Log($"Combat started: {_player.Name} vs {string.Join(", ", _enemies.Select(e => e.Name))}.");
        Publish(GameEvents.EncounterStarted, source: SelfId, amount: _enemies.Count);
        ScheduleStaminaRegen();
        ScheduleStatusTick();
        foreach (var enemy in _enemies)
            BeginEnemyDecision(enemy);

        StateChanged?.Invoke();
    }

    // --- Player commands ----------------------------------------------------

    public bool Attack()
    {
        if (!IsActive)
            return false;
        if (!_player.IsReady(_tick.CurrentTick))
        {
            Log("You are still recovering.");
            return false;
        }

        var attack = PlayerAttackProfile;
        if (_player.Stamina.Current < attack.StaminaCost)
        {
            Log("Not enough stamina to attack.");
            return false;
        }

        var target = FirstAliveEnemy();
        if (target is null)
            return false;

        _player.Stamina.Reduce(attack.StaminaCost);
        var executeIn = Math.Max(1, attack.Timing.TimeToImpactTicks);
        _player.ReadyTick = _tick.CurrentTick + executeIn + attack.Timing.RecoveryTicks;
        Log($"You ready {attack.Name}.");

        Publish(GameEvents.ResourceSpent, source: SelfId, amount: attack.StaminaCost,
            tags: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "stamina", "attack" });

        Commit(new ActionInFlight
        {
            Actor = _player,
            Target = target,
            Name = attack.Name,
            DamageType = attack.DamageType,
            BaseDamage = attack.BaseDamage,
            Timing = attack.Timing,
            Tags = AttackTags(attack.DamageType, attack.Timing),
        });

        return true;
    }

    public void Block() => EnterStance("raise your guard", CombatTuning.BlockStaminaCost, isBlock: true);

    public void Dodge() => EnterStance("dodge", CombatTuning.DodgeStaminaCost, isBlock: false);

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
        if (!IsActive || !enemy.IsAlive || enemy.AbilityIds.Count == 0)
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

        var abilityId = enemy.AbilityIds[_rng.NextInt(0, enemy.AbilityIds.Count)];
        var ability = _abilities.GetById(abilityId);

        Commit(new ActionInFlight
        {
            Actor = enemy,
            Target = _player,
            Name = ability.Name,
            DamageType = ability.DamageType,
            BaseDamage = ability.BaseValue,
            Timing = ability.Timing,
            Tags = AttackTags(ability.DamageType, ability.Timing),
            Ability = ability,
        });

        Log($"{enemy.Name} begins {ability.Name}!");
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
        var target = attacker.Team == CombatTeam.Enemy ? _player : FirstAliveEnemy();

        if (!attacker.IsAlive || target is null)
            return;

        if (target.IsAlive)
        {
            var result = _calculator.Resolve(attacker, target, action.DamageType, action.BaseDamage, _tick.CurrentTick);
            ApplyResult(attacker, target, action.Name, result, action.Tags);

            if (!target.IsAlive && HandleDefeat(attacker, target))
                return;
        }

        // Enemies self-schedule their next decision; the player's tempo is their ReadyTick.
        if (attacker.Team == CombatTeam.Enemy && attacker.IsAlive)
        {
            var recovery = Math.Max(1, action.Timing.RecoveryTicks);
            _recovery[attacker] = _tick.Schedule(recovery, () => BeginEnemyDecision(attacker));
        }
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

        target.Health.Reduce(applied);

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

    /// <summary>The combatant an event id refers to — <see cref="SelfId"/> or an enemy's name.</summary>
    public Combatant? Find(string? id) =>
        string.IsNullOrEmpty(id) ? null : Combatants.FirstOrDefault(c => string.Equals(Id(c), id, StringComparison.Ordinal));

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

    private void EnterStance(string verb, int staminaCost, bool isBlock)
    {
        if (!IsActive)
            return;
        if (_player.Stamina.Current < staminaCost)
        {
            Log($"Not enough stamina to {verb}.");
            return;
        }

        _player.Stamina.Reduce(staminaCost);
        var until = _tick.CurrentTick + (isBlock ? CombatTuning.BlockDurationTicks : CombatTuning.DodgeDurationTicks);
        if (isBlock)
        {
            _player.BlockUntilTick = until;
            _player.BlockStartTick = _tick.CurrentTick;   // the Perfect Block window starts here
        }
        else
        {
            _player.DodgeUntilTick = until;
        }

        Log($"You {verb}.");
        Publish(GameEvents.ResourceSpent, source: SelfId, amount: staminaCost,
            tags: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "stamina", "defensive", isBlock ? "block" : "dodge" });
        StateChanged?.Invoke();
    }

    private void ApplyResult(
        Combatant attacker, Combatant target, string attackName, HitResult result, HashSet<string> tags)
    {
        // Copy before touching it. The caller hands in the ActionInFlight's own tag set, which
        // was already given to ActionQueued/ActionTelegraphed — and a GameEvent holds the
        // reference rather than a snapshot. Adding `blocked` to the original therefore rewrote
        // the tags of events published before the block existed, so anything that *records*
        // events (the Hit Log, the Lab's recent-firings panel, any replay) read a history that
        // never happened. Live matching never saw it, because the bus dispatches synchronously.
        tags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);

        var source = Id(attacker);
        var victim = Id(target);

        LastHit = result;
        HitResolved?.Invoke(result);

        // The attack happened regardless of how it landed — this is what MoveExecuted means, and
        // it is the single most-referenced event in shipped content (18 hooks).
        Publish(GameEvents.MoveExecuted, source, victim, result.Amount, tags);
        Publish(GameEvents.ActionResolved, source, victim, result.Amount, tags);

        // `Blocked` is raised from the *defender's* perspective (source is who blocked) and for
        // BOTH block outcomes (D-06 §6.2). Hooking on-block affixes to a landed hit instead would
        // mean a *perfect* block produced no retaliation — punishing the better play.
        if (result.Blocked)
        {
            tags.Add("blocked");
            if (result.PerfectBlock)
                tags.Add("perfect");
            Publish(GameEvents.Blocked, source: victim, target: source, amount: result.Amount, tags: tags);
        }

        if (result.Avoided)
        {
            var how = result.PerfectBlock ? "blocks perfectly" : "dodges";
            Log($"{target.Name} {how} {attacker.Name}'s {attackName}!");

            // Dodged is the only avoidance event in E0's vocabulary; E1 does not widen it.
            // HitAvoided arrives in E3 with the rest of §3.1.
            if (result.Dodged)
                Publish(GameEvents.Dodged, source: victim, target: source, tags: tags);

            StateChanged?.Invoke();
            return;
        }

        if (result.Crit)
            tags.Add("critical");

        target.Health.Reduce(result.Amount);

        Publish(GameEvents.DamageDealt, source, victim, result.Amount, tags);
        Publish(GameEvents.DamageTaken, source: victim, target: source, amount: result.Amount, tags: tags);

        var suffix = (result.Crit ? " (crit!)" : string.Empty) + (result.Blocked ? " (blocked)" : string.Empty);
        Log($"{attacker.Name}'s {attackName} hits {target.Name} for {result.Amount} {result.Type}{suffix}. " +
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
    /// Tags describing one attack. `heavy` is derived from time-to-impact
    /// (<see cref="CombatTuning.HeavyTimeToImpactTicks"/>) rather than authored, which is what
    /// makes Exploding Kneecaps and the Venomous burst fire against the Goblin Brute today.
    /// <para><b>Known simplification:</b> everything is `melee` because everything currently is.
    /// Real delivery tags arrive with moves in E4.</para>
    /// </summary>
    private static HashSet<string> AttackTags(DamageType type, AbilityTiming timing) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            "attack",
            "melee",
            type.ToString().ToLowerInvariant(),
            timing.TimeToImpactTicks >= CombatTuning.HeavyTimeToImpactTicks ? "heavy" : "light",
        };

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
            if (chance <= 0 || _rng.NextDouble() >= chance)
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

    /// <summary>
    /// Applies a status through the controller, publishing whatever the attempt produced. The
    /// controller decides whether a control actually lands; combat only reports it.
    /// </summary>
    public ControlOutcome ApplyStatus(
        Combatant target, string statusId, string sourceId, double magnitude = 0,
        int durationOverride = 0, EffectContext? context = null)
    {
        if (Statuses is null)
            return ControlOutcome.Ungated;

        var outcome = Statuses.Apply(target, statusId, sourceId, magnitude, durationOverride, context);

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
