using System.Linq;
using Dungeons.Content;
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
    private readonly TickEngine _tick;
    private readonly CombatCalculator _calculator;
    private readonly DataStore<AbilityDefinition> _abilities;
    private readonly IRandomSource _rng;
    private readonly string _playerFallbackAbilityId;

    private readonly Dictionary<Combatant, EnemyIntent> _intents = new();
    private readonly Dictionary<Combatant, ScheduledAction> _enemyPending = new();
    private readonly List<Combatant> _defeatedEnemies = new();
    private ScheduledAction? _playerPending;
    private ScheduledAction? _regenPending;

    private Combatant _player = null!;
    private List<Combatant> _enemies = new();

    public CombatEncounter(
        TickEngine tick,
        CombatCalculator calculator,
        DataStore<AbilityDefinition> abilities,
        IRandomSource rng,
        string playerFallbackAbilityId)
    {
        _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
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
        IsActive = true;
        _player.ReadyTick = _tick.CurrentTick;

        Log($"Combat started: {_player.Name} vs {string.Join(", ", _enemies.Select(e => e.Name))}.");
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
            ApplyResult(enemy, _player, ability.Name, result);
            if (!_player.IsAlive)
            {
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
        ApplyResult(_player, target, attack.Name, result);

        if (!target.IsAlive)
        {
            _defeatedEnemies.Add(target);
            Log($"{target.Name} is defeated!");
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
        StateChanged?.Invoke();
    }

    private void ApplyResult(Combatant attacker, Combatant target, string attackName, DamageResult result)
    {
        if (result.Dodged)
        {
            Log($"{target.Name} dodges {attacker.Name}'s {attackName}!");
        }
        else
        {
            target.Health.Reduce(result.Amount);
            var tags = (result.Crit ? " (crit!)" : string.Empty) + (result.Blocked ? " (blocked)" : string.Empty);
            Log($"{attacker.Name}'s {attackName} hits {target.Name} for {result.Amount} {result.Type}{tags}. " +
                $"[{target.Name} {target.Health.Current}/{target.Health.Max}]");
        }

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
