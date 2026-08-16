using System.Linq;
using Dungeons.Content;
using Dungeons.Events;
using Dungeons.Randomness;
using Dungeons.Simulation;

namespace Dungeons.Combat;

public enum CombatResult
{
    Victory,
    Defeat,
}

/// <summary>A pending enemy attack the player can see and react to.</summary>
public sealed class EnemyIntent
{
    public required Combatant Attacker { get; init; }
    public required AbilityDefinition Ability { get; init; }
    public required long ExecuteTick { get; init; }
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

    private readonly Dictionary<Combatant, EnemyIntent> _intents = new();
    private readonly Dictionary<Combatant, ScheduledAction> _enemyPending = new();
    private readonly List<Combatant> _defeatedEnemies = new();
    private readonly Dictionary<string, int> _eventCounts = new(StringComparer.Ordinal);
    private ScheduledAction? _playerPending;
    private ScheduledAction? _regenPending;

    private Combatant _player = null!;
    private List<Combatant> _enemies = new();

    public CombatEncounter(
        TickEngine tick,
        CombatCalculator calculator,
        DataStore<AbilityDefinition> abilities,
        IRandomSource rng,
        IGameEventBus bus,
        string playerFallbackAbilityId)
    {
        _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _playerFallbackAbilityId = playerFallbackAbilityId;
    }

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

    public bool IsActive { get; private set; }
    public Combatant Player => _player;
    public IReadOnlyList<Combatant> Enemies => _enemies;
    public IReadOnlyList<Combatant> Combatants => _enemies.Prepend(_player).ToList();
    public IReadOnlyList<EnemyIntent> Intents => _intents.Values.ToList();
    public long CurrentTick => _tick.CurrentTick;
    public bool PlayerReady => IsActive && _player.IsReady(_tick.CurrentTick);

    public void Start(Combatant player, IReadOnlyList<Combatant> enemies)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _enemies = enemies?.ToList() ?? throw new ArgumentNullException(nameof(enemies));
        _intents.Clear();
        _enemyPending.Clear();
        _defeatedEnemies.Clear();
        _eventCounts.Clear();
        IsActive = true;
        _player.ReadyTick = _tick.CurrentTick;

        Log($"Combat started: {_player.Name} vs {string.Join(", ", _enemies.Select(e => e.Name))}.");
        Publish(GameEvents.EncounterStarted, source: SelfId, amount: _enemies.Count);
        ScheduleStaminaRegen();
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
        _playerPending = _tick.Schedule(executeIn, () => ResolvePlayerAttack(attack));
        Log($"You ready {attack.Name}.");

        var tags = AttackTags(attack.DamageType, attack.Timing);
        Publish(GameEvents.ResourceSpent, source: SelfId, amount: attack.StaminaCost,
            tags: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "stamina", "attack" });
        Publish(GameEvents.ActionQueued, source: SelfId, target: Id(target), tags: tags);
        Publish(GameEvents.ActionTelegraphed, source: SelfId, target: Id(target), tags: tags);

        StateChanged?.Invoke();
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

        var abilityId = enemy.AbilityIds[_rng.NextInt(0, enemy.AbilityIds.Count)];
        var ability = _abilities.GetById(abilityId);
        var executeIn = Math.Max(1, ability.Timing.TimeToImpactTicks);
        var executeTick = _tick.CurrentTick + executeIn;

        _intents[enemy] = new EnemyIntent { Attacker = enemy, Ability = ability, ExecuteTick = executeTick };
        _enemyPending[enemy] = _tick.Schedule(executeIn, () => ResolveEnemyAttack(enemy, ability));

        Log($"{enemy.Name} begins {ability.Name}!");
        var tags = AttackTags(ability.DamageType, ability.Timing);
        Publish(GameEvents.ActionQueued, source: Id(enemy), target: SelfId, tags: tags);
        Publish(GameEvents.ActionTelegraphed, source: Id(enemy), target: SelfId, tags: tags);
        StateChanged?.Invoke();
    }

    private void ResolveEnemyAttack(Combatant enemy, AbilityDefinition ability)
    {
        _intents.Remove(enemy);
        _enemyPending.Remove(enemy);
        if (!IsActive || !enemy.IsAlive)
            return;

        if (_player.IsAlive)
        {
            var result = _calculator.Resolve(enemy, _player, ability.DamageType, ability.BaseValue, _tick.CurrentTick);
            ApplyResult(enemy, _player, ability.Name, result, AttackTags(ability.DamageType, ability.Timing));
            if (!_player.IsAlive)
            {
                Publish(GameEvents.Killed, source: Id(enemy), target: SelfId);
                Publish(GameEvents.Defeated, source: SelfId, target: Id(enemy));
                EndCombat(CombatResult.Defeat);
                return;
            }
        }

        var recovery = Math.Max(1, ability.Timing.RecoveryTicks);
        _enemyPending[enemy] = _tick.Schedule(recovery, () => BeginEnemyDecision(enemy));
    }

    private void ResolvePlayerAttack(AttackProfile attack)
    {
        _playerPending = null;
        if (!IsActive)
            return;

        var target = FirstAliveEnemy();
        if (target is null)
            return;

        var result = _calculator.Resolve(_player, target, attack.DamageType, attack.BaseDamage, _tick.CurrentTick);
        ApplyResult(_player, target, attack.Name, result, AttackTags(attack.DamageType, attack.Timing));

        if (!target.IsAlive)
        {
            _defeatedEnemies.Add(target);
            Log($"{target.Name} is defeated!");
            Publish(GameEvents.Killed, source: SelfId, target: Id(target));
            Publish(GameEvents.Defeated, source: Id(target), target: SelfId);
            CancelEnemy(target);
            if (_enemies.All(e => !e.IsAlive))
                EndCombat(CombatResult.Victory);
        }
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
            _player.BlockUntilTick = until;
        else
            _player.DodgeUntilTick = until;

        Log($"You {verb}.");
        Publish(GameEvents.ResourceSpent, source: SelfId, amount: staminaCost,
            tags: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "stamina", "defensive", isBlock ? "block" : "dodge" });
        StateChanged?.Invoke();
    }

    private void ApplyResult(
        Combatant attacker, Combatant target, string attackName, DamageResult result, HashSet<string> tags)
    {
        var source = Id(attacker);
        var victim = Id(target);

        // The attack happened regardless of how it landed — this is what MoveExecuted means, and
        // it is the single most-referenced event in shipped content (18 hooks).
        Publish(GameEvents.MoveExecuted, source, victim, result.Amount, tags);
        Publish(GameEvents.ActionResolved, source, victim, result.Amount, tags);

        if (result.Dodged)
        {
            Log($"{target.Name} dodges {attacker.Name}'s {attackName}!");
            Publish(GameEvents.Dodged, source: victim, target: source, tags: tags);
            StateChanged?.Invoke();
            return;
        }

        if (result.Crit)
            tags.Add("critical");
        if (result.Blocked)
        {
            tags.Add("blocked");
            // Raised from the *defender's* perspective: source is who blocked, target is who was
            // blocked. Exploding Kneecaps' Guard expression detonates "against the attacker",
            // so the attacker has to be reachable from the event.
            Publish(GameEvents.Blocked, source: victim, target: source, amount: result.Amount, tags: tags);
        }

        target.Health.Reduce(result.Amount);

        Publish(GameEvents.DamageDealt, source, victim, result.Amount, tags);
        Publish(GameEvents.DamageTaken, source: victim, target: source, amount: result.Amount, tags: tags);

        var suffix = (result.Crit ? " (crit!)" : string.Empty) + (result.Blocked ? " (blocked)" : string.Empty);
        Log($"{attacker.Name}'s {attackName} hits {target.Name} for {result.Amount} {result.Type}{suffix}. " +
            $"[{target.Name} {target.Health.Current}/{target.Health.Max}]");

        StateChanged?.Invoke();
    }

    private void EndCombat(CombatResult result)
    {
        if (!IsActive)
            return;
        IsActive = false;

        CancelAll();
        _intents.Clear();

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

    private void Publish(string kind, string? source = null, string? target = null,
        double amount = 0.0, IReadOnlySet<string>? tags = null)
    {
        _eventCounts.TryGetValue(kind, out var seen);
        _eventCounts[kind] = seen + 1;

        _bus.Publish(new GameEvent(kind, source, target, amount, tags, new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["self_health_fraction"] = Fraction(_player.Health),
            ["self_stamina_fraction"] = Fraction(_player.Stamina),
            ["encounter_index"] = seen + 1,
        }));
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

    private Combatant? FirstAliveEnemy() => _enemies.FirstOrDefault(e => e.IsAlive);

    private void CancelEnemy(Combatant enemy)
    {
        if (_enemyPending.TryGetValue(enemy, out var pending))
        {
            _tick.Cancel(pending.Id);
            _enemyPending.Remove(enemy);
        }

        _intents.Remove(enemy);
    }

    private void CancelAll()
    {
        foreach (var pending in _enemyPending.Values)
            _tick.Cancel(pending.Id);
        _enemyPending.Clear();
        if (_playerPending is not null)
            _tick.Cancel(_playerPending.Id);
        _playerPending = null;
        if (_regenPending is not null)
            _tick.Cancel(_regenPending.Id);
        _regenPending = null;
    }

    private void Log(string message) => Logged?.Invoke(message);
}
