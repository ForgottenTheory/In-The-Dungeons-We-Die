using Dungeons.Events;
using Dungeons.Randomness;
using Dungeons.Rules;
using Dungeons.Simulation;

namespace Dungeons.Combat;

/// <summary>
/// Plays the player's side of a fight by issuing the same commands a hand on the keyboard would
/// (GDD §5.7, docs/damage-and-defense.md §5.1.1).
///
/// <para><b>It chooses; it never resolves.</b> Everything it does is
/// <see cref="CombatEncounter.UseMove"/>, <see cref="CombatEncounter.Block"/> and
/// <see cref="CombatEncounter.Dodge"/> — so move timing, telegraphs, costs, statuses, damage,
/// defences, cooldowns and triggers all run through the one encounter, on the one tick engine.
/// There is deliberately no simplified combat calculator anywhere in this file, and there must
/// never be one: the moment automated play has its own maths, passive and active are two balance
/// models wearing one name.</para>
///
/// <para><b>Its offensive brain is the enemy brain.</b> Engaging puts the profile's rules onto
/// <see cref="Combatant.Ai"/> and asks <see cref="CombatEncounter.ChooseMoveFor"/> — the same
/// weighted selection over the same condition vocabulary that every enemy in the game runs.</para>
///
/// <para><b>Its weakness is one number: <see cref="AutoCombatProfileDefinition.ReactionTicks"/>.</b>
/// An agent whose hand is R ticks behind its eye cannot commit at the last moment; to cover an
/// impact at tick T it must decide at T−R, and the stance therefore goes up R ticks early. Every
/// tight window in the game is measured from the moment the stance was raised, so R ticks early
/// is outside all of them: it blocks (16-tick window) and dodges (10) reliably, and can never
/// land a Perfect Block (4) or a Parry (3). That is D-07 in full — active play earns its
/// advantage by being present, never by a hidden damage bonus — and it is why this class contains
/// no multiplier of any kind.</para>
///
/// <para><b>It reads only what the screen shows.</b> Incoming attacks come from
/// <see cref="CombatEncounter.Intents"/>, the same read-model the combat panel renders. A pilot
/// that reached into the encounter's internals would be playing a different game from the one the
/// player can see, and "auto-combat is weaker" would stop being checkable.</para>
/// </summary>
public sealed class AutoCombatPilot
{
    private readonly CombatEncounter _encounter;
    private readonly TickEngine _tick;
    private readonly IRandomSource _random;

    private ScheduledAction? _nextDecision;

    /// <summary>
    /// The incoming action being watched, when it was first seen, and whether it has been
    /// answered. Identity is the <see cref="ActionInFlight"/> instance itself — one is minted per
    /// action, so two consecutive swings of the same move are two different things to react to,
    /// which reference-comparing the <see cref="ResolvedMove"/> would get wrong.
    /// </summary>
    private ActionInFlight? _watchedAction;
    private long _noticedAtTick;
    private bool _watchedActionAnswered;

    public AutoCombatPilot(
        CombatEncounter encounter,
        TickEngine tick,
        AutoCombatProfileDefinition profile,
        IRandomSource random)
    {
        _encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        _tick = tick ?? throw new ArgumentNullException(nameof(tick));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public AutoCombatProfileDefinition Profile { get; }

    public bool IsEngaged { get; private set; }

    /// <summary>Raised for anything worth a log line — which move it chose, which stance it
    /// raised. Automated play the player cannot read is automated play they cannot learn to
    /// trust or to correct.</summary>
    public event Action<string>? Decided;

    /// <summary>
    /// Takes over the player's side of the current encounter. Hands the profile's rules to the
    /// player combatant, so the encounter's own move selection answers for them from now on.
    /// </summary>
    public void Engage()
    {
        if (IsEngaged || !_encounter.IsActive)
            return;

        IsEngaged = true;
        _encounter.Player.Ai = Profile.Rules;
        _encounter.Player.AvoidRepeatWeight = Profile.AvoidRepeatWeight;
        _watchedAction = null;
        _watchedActionAnswered = false;
        ScheduleNextDecision();
    }

    /// <summary>Hands control back. The player combatant loses the brain, so a manual command
    /// is never competing with a scheduled one.</summary>
    public void Disengage()
    {
        if (!IsEngaged)
            return;

        IsEngaged = false;
        if (_nextDecision is not null)
        {
            _tick.Cancel(_nextDecision.Id);
            _nextDecision = null;
        }

        _encounter.Player.Ai = Array.Empty<AiRuleSpec>();
        _encounter.Player.AvoidRepeatWeight = 1.0;
    }

    private void ScheduleNextDecision() =>
        _nextDecision = _tick.Schedule(AutoCombatTuning.DecisionPollTicks, Decide);

    /// <summary>
    /// One look at the fight. Defence first: a stance that misses its moment cannot be taken
    /// again, whereas an attack deferred by a tick is only an attack a tick later.
    /// </summary>
    private void Decide()
    {
        _nextDecision = null;
        if (!IsEngaged)
            return;

        if (!_encounter.IsActive || !_encounter.Player.IsAlive)
        {
            Disengage();
            return;
        }

        AnswerIncomingAttack();
        Attack();
        ScheduleNextDecision();
    }

    /// <summary>
    /// Raises a stance against the soonest incoming attack, at the last moment this pilot's
    /// reaction allows.
    ///
    /// <para><b>Two constraints, and their interaction is the whole design.</b> A slow hand
    /// cannot act until <see cref="AutoCombatProfileDefinition.ReactionTicks"/> after it
    /// <em>noticed</em> the intent, and it cannot leave the decision later than
    /// <c>ReactionTicks</c> before impact or it will not have moved in time. So the stance goes
    /// up at <c>max(noticed + R, impact − R)</c>:</para>
    /// <list type="bullet">
    ///   <item>A telegraphed swing is answered at <c>impact − R</c> — covered by the wide
    ///   windows, never inside the tight ones, because every tight window is measured from the
    ///   moment the stance went up.</item>
    ///   <item>An attack arriving sooner than <c>2R</c> after it appears cannot be answered at
    ///   all: the earliest the hand can move is already past the moment it needed to. Fast and
    ///   untelegraphed moves beating automation is the correct outcome, and it is why the small
    ///   untelegraphed-only <c>evade</c> passive survived D-07.</item>
    /// </list>
    /// </summary>
    private void AnswerIncomingAttack()
    {
        if (Profile.Defence.Count == 0)
            return;

        var incoming = SoonestIncomingAttack();
        if (incoming is null)
        {
            _watchedAction = null;
            return;
        }

        var action = _encounter.ActionOf(incoming.Attacker);
        if (!ReferenceEquals(action, _watchedAction))
        {
            _watchedAction = action;
            _noticedAtTick = _tick.CurrentTick;
            _watchedActionAnswered = false;
        }

        // One telegraph, one answer: without this the pilot would re-raise its stance on every
        // poll for the rest of the windup and drain its own stamina answering one attack.
        if (_watchedActionAnswered)
            return;

        var earliestItCouldMove = _noticedAtTick + Profile.ReactionTicks;
        var latestItCouldDecide = incoming.ExecuteTick - Profile.ReactionTicks;
        if (_tick.CurrentTick < Math.Max(earliestItCouldMove, latestItCouldDecide))
            return;

        _watchedActionAnswered = true;

        var stance = ChooseStance();
        if (stance is null)
            return;

        if (stance == DefensiveStance.Block)
            _encounter.Block();
        else
            _encounter.Dodge();

        Decided?.Invoke($"[Auto] {stance} against {incoming.Move.Name}.");
    }

    /// <summary>
    /// The soonest attack aimed at the player that is still in flight. Self-targeted enemy moves
    /// (a brace, a buff) are not something to block, so they are not answered.
    /// </summary>
    private EnemyIntent? SoonestIncomingAttack() =>
        _encounter.Intents
            .Where(intent => intent.Move.Targeting != Targeting.Self)
            .OrderBy(intent => intent.ExecuteTick)
            .FirstOrDefault();

    /// <summary>Weighted pick over the profile's defence rules, in the same shape and with the
    /// same condition vocabulary as its move rules. Null when no rule's conditions pass — a
    /// brain that has decided this is not a moment to spend stamina on.</summary>
    private DefensiveStance? ChooseStance()
    {
        var decision = PlayerStateEvent();
        var candidates = new List<(DefensiveStance Stance, double Weight)>();

        foreach (var rule in Profile.Defence)
        {
            if (rule.Weight <= 0)
                continue;
            if (!rule.When.All(condition => TriggerRuleEngine.Evaluate(condition, decision, _encounter.ConditionWorld)))
                continue;
            candidates.Add((rule.Stance, rule.Weight));
        }

        if (candidates.Count == 0)
            return null;

        var total = candidates.Sum(candidate => candidate.Weight);
        var roll = _random.NextDouble() * total;
        foreach (var (stance, weight) in candidates)
        {
            roll -= weight;
            if (roll < 0)
                return stance;
        }

        return candidates[^1].Stance;
    }

    /// <summary>
    /// The event a defence rule is evaluated against — the player's own bars, so
    /// <c>selfHealthBelow</c> reads the player the way it reads an enemy checking itself.
    /// </summary>
    private GameEvent PlayerStateEvent()
    {
        var player = _encounter.Player;
        return new GameEvent(
            "AutoCombatDecision",
            Source: CombatEncounter.SelfId,
            Target: CombatEncounter.SelfId,
            Values: new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["self_health_fraction"] = Fraction(player.Health),
                ["self_stamina_fraction"] = Fraction(player.Stamina),
            },
            CanTrigger: false);
    }

    private static double Fraction(Characters.ResourcePool pool) =>
        pool.Max <= 0 ? 0 : (double)pool.Current / pool.Max;

    /// <summary>
    /// Attacks when the player is out of recovery, using the encounter's own weighted selection.
    /// No latency is applied here beyond the poll: the design's disadvantage is <em>reacting</em>
    /// late, not swinging slowly, and a tempo penalty on top would be the arbitrary damage
    /// handicap D-07 rejects.
    /// </summary>
    private void Attack()
    {
        if (!_encounter.PlayerReady)
            return;

        var move = _encounter.ChooseMoveFor(_encounter.Player);
        if (move is null)
            return;

        if (_encounter.UseMove(move.Id))
            Decided?.Invoke($"[Auto] {move.Name}.");
    }
}
