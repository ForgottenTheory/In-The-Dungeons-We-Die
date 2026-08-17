using Dungeons.Events;
using Dungeons.Randomness;

namespace Dungeons.Rules;

/// <summary>An effect actually happening: the spec, its resolved magnitude, and what caused it.</summary>
public sealed record EffectInvocation(EffectSpec Effect, double Magnitude, GameEvent Trigger, string Source)
{
    public string Kind => Effect.Kind;

    /// <summary>Who this effect acts on.</summary>
    public EffectTarget Target { get; init; } = EffectTarget.TriggerTarget;

    /// <summary>
    /// The causal chain this belongs to. <b>Handlers must propagate it</b> onto any event they
    /// raise, or the chain restarts at depth 0 and the proc budget means nothing.
    /// </summary>
    public EffectContext Context { get; init; } =
        EffectContext.Origin("detached", string.Empty);
}

/// <summary>
/// Executes one kind of effect. Registered by the system that owns the behaviour — combat
/// registers <c>damage</c>, the inventory registers <c>grantItem</c>, and so on.
/// </summary>
public interface IEffectHandler
{
    string Kind { get; }
    void Execute(EffectInvocation invocation);
}

/// <summary>
/// A place effects can be sent from outside the rule engine (E4).
///
/// <para>Move riders need this: a move's own <c>applyStatus</c> is not a rule firing on an
/// event, but it must run through the same registered handlers and the same proc accounting —
/// otherwise Fireball's Burn would be a second, parallel status path. <see cref="NewChain"/>
/// exists because chain ids are sequential and the engine owns the counter; a rider starting
/// its own chain with a made-up id would collide or break replay.</para>
/// </summary>
public interface IEffectSink
{
    /// <summary>A fresh causal chain, for an effect that is itself an origin (a move's rider).</summary>
    EffectContext NewChain(string origin);

    /// <summary>Dispatches to the registered handler; unhandled kinds are recorded, not dropped.</summary>
    void Execute(EffectInvocation invocation);
}

/// <summary>
/// Evaluates declarative <see cref="TriggerRule"/>s against the event bus.
///
/// <para>This is the whole reason Prefixes and Suffixes can be data. A rule names an event, a
/// few conditions and an effect; this class does the matching, the cooldowns and the dispatch.
/// Nothing here knows what a Prefix is, and nothing here knows what a Base is.</para>
///
/// <para>Deterministic given a seed: the only randomness is <see cref="TriggerRule.Chance"/>,
/// rolled through the injected source, and rules are evaluated in registration order.</para>
/// </summary>
public sealed class TriggerRuleEngine : IDisposable, IEffectSink
{
    private readonly IGameEventBus _bus;
    private readonly IRandomSource _random;
    private readonly Func<long> _currentTick;
    private readonly Dictionary<string, IEffectHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Registration> _rules = new();

    /// <summary>
    /// When each cooldown next expires. Holds two kinds of key, deliberately in one map because
    /// they never collide: <c>"source|ruleId"</c> for a rule's own cooldown, and
    /// <c>"source|ruleId|targetId"</c> for its per-target internal cooldown.
    /// </summary>
    private readonly Dictionary<string, long> _cooldownReadyTick = new(StringComparer.Ordinal);
    private readonly List<EffectInvocation> _unhandled = new();
    private readonly List<string> _unevaluated = new();
    private readonly IConditionWorld? _world;
    private readonly HashSet<string> _firedInChain = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _chainEffectCount = new(StringComparer.Ordinal);
    private IDisposable? _subscription;

    public TriggerRuleEngine(
        IGameEventBus bus, IRandomSource random, Func<long> currentTick, IConditionWorld? world = null)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _currentTick = currentTick ?? throw new ArgumentNullException(nameof(currentTick));
        _world = world;
        _subscription = _bus.Subscribe(OnEvent);
    }

    /// <summary>Effects that fired with no handler registered. Content that references a system
    /// which does not exist yet lands here rather than vanishing — the Character Lab surfaces it
    /// so half-wired content is visible instead of mysteriously inert.</summary>
    public IReadOnlyList<EffectInvocation> Unhandled => _unhandled;

    /// <summary>
    /// Conditions that could not be answered because no <see cref="IConditionWorld"/> was
    /// supplied. The condition half of <see cref="Unhandled"/>: a rule whose condition can never
    /// be evaluated is as dead as one whose effect goes nowhere, and it should be as visible.
    /// </summary>
    public IReadOnlyList<string> UnevaluatedConditions => _unevaluated;

    /// <summary>Every effect that fired, in order. Feeds the log and the tests.</summary>
    public List<EffectInvocation> Fired { get; } = new();

    /// <summary>
    /// Chains stopped by the <see cref="ProcSafety.MaxEffectsPerChain"/> fuse. This is a bug
    /// surface, not a balance one — shipped content must never appear here, and a test says so.
    /// </summary>
    public List<string> Aborted { get; } = new();

    public TriggerRuleEngine Register(IEffectHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[handler.Kind] = handler;
        return this;
    }

    /// <summary>Attaches a rule owned by <paramref name="source"/> (a prefix or suffix id).</summary>
    public TriggerRuleEngine Attach(TriggerRule rule, string source)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(new Registration(rule, source));
        return this;
    }

    public TriggerRuleEngine AttachAll(IEnumerable<TriggerRule> rules, string source)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var rule in rules)
            Attach(rule, source);
        return this;
    }

    /// <summary>Drops every attached rule — used when the character's build changes.</summary>
    public void DetachAll()
    {
        _rules.Clear();
        _cooldownReadyTick.Clear();
        _firedInChain.Clear();
        _chainEffectCount.Clear();
    }

    private void OnEvent(GameEvent gameEvent)
    {
        // An event explicitly barred from triggering — retaliation damage, an ailment tick —
        // matches nothing. This is rule 4 of proc safety and it is what actually breaks the
        // thorns→shock→retaliate→thorns loop, well before the depth ceiling is reached.
        if (!gameEvent.CanTrigger)
            return;

        var chainId = gameEvent.ChainId ?? NextChainId();

        foreach (var registration in _rules.ToList())
        {
            var rule = registration.Rule;

            if (!string.Equals(rule.Event, gameEvent.Kind, StringComparison.Ordinal))
                continue;

            // Depth: a rule may only fire if the event that triggered it is below the ceiling.
            // Anomalous affixes raise their OWN ceiling by exactly one; nothing removes it.
            if (gameEvent.Depth >= rule.Proc.MaxDepth)
                continue;

            // Once-per-chain: kills A→B→A ping-pong even inside the depth budget.
            var chainKey = chainId + "|" + registration.Source + "|" + rule.Id;
            if (rule.Proc.OncePerChain && !_firedInChain.Add(chainKey))
                continue;

            var cooldownKey = registration.Source + "|" + rule.Id;
            if (_cooldownReadyTick.TryGetValue(cooldownKey, out var readyAt) && _currentTick() < readyAt)
                continue;

            // Per-target internal cooldown. Chosen over PoE-style proc coefficients deliberately:
            // an ICD is readable in a tooltip ("once every 2s") and a proc coefficient is not.
            var perTargetCooldownKey = cooldownKey + "|" + (gameEvent.Target ?? string.Empty);
            if (rule.Proc.IcdTicks > 0
                && _cooldownReadyTick.TryGetValue(perTargetCooldownKey, out var targetReadyAt)
                && _currentTick() < targetReadyAt)
                continue;

            if (!rule.When.All(condition => EvaluateHere(condition, gameEvent, registration.Source)))
                continue;

            // Rolled after conditions, so a chance-gated rule doesn't burn entropy on events
            // that were never going to qualify — which keeps seeded runs stable when unrelated
            // content changes.
            if (rule.Chance < 1.0 && _random.NextDouble() >= rule.Chance)
                continue;

            if (rule.CooldownTicks > 0)
                _cooldownReadyTick[cooldownKey] = _currentTick() + rule.CooldownTicks;
            if (rule.Proc.IcdTicks > 0)
                _cooldownReadyTick[perTargetCooldownKey] = _currentTick() + rule.Proc.IcdTicks;

            var context = new EffectContext(
                chainId,
                gameEvent.Source ?? registration.Source,
                registration.Source,
                gameEvent.Depth + 1,
                gameEvent.Tags ?? EmptyTags);

            // ONE chance roll, N effects — which is the whole reason `effects[]` exists.
            foreach (var effect in rule.Payload)
            {
                if (_chainEffectCount.GetValueOrDefault(chainId) >= ProcSafety.MaxEffectsPerChain)
                {
                    Aborted.Add(chainId);
                    return;
                }

                _chainEffectCount[chainId] = _chainEffectCount.GetValueOrDefault(chainId) + 1;

                Dispatch(new EffectInvocation(effect, effect.Magnitude(gameEvent), gameEvent, registration.Source)
                {
                    Target = effect.Target ?? rule.Target,
                    Context = context,
                });
            }
        }
    }

    private static readonly IReadOnlySet<string> EmptyTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private long _chainCounter;

    /// <summary>
    /// Chain ids are sequential, not random — the simulation must replay identically from a
    /// seed, and a GUID here would break that quietly.
    /// </summary>
    private string NextChainId() => "chain." + (++_chainCounter);

    /// <inheritdoc />
    public EffectContext NewChain(string origin) => EffectContext.Origin(NextChainId(), origin);

    /// <inheritdoc />
    void IEffectSink.Execute(EffectInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        Dispatch(invocation);
    }

    private void Dispatch(EffectInvocation invocation)
    {
        Fired.Add(invocation);

        if (_handlers.TryGetValue(invocation.Kind, out var handler))
            handler.Execute(invocation);
        else
            _unhandled.Add(invocation);
    }

    /// <summary>
    /// Condition evaluation. One method, one switch — adding a condition kind is a case here plus
    /// an entry in <see cref="RuleVocabulary.Conditions"/>.
    /// </summary>
    /// <param name="world">
    /// World state for the conditions that need it (E3c-3). Null answers only the conditions that
    /// are pure functions of the event; the instance path records anything it could not evaluate
    /// in <see cref="UnevaluatedConditions"/> rather than letting a rule quietly never fire.
    /// </param>
    public static bool Evaluate(ConditionSpec condition, GameEvent gameEvent, IConditionWorld? world = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(gameEvent);

        var result = condition.Kind switch
        {
            RuleVocabulary.HasTag => gameEvent.HasTag(condition.Text),
            RuleVocabulary.AmountAtLeast => gameEvent.Amount >= condition.Value,
            RuleVocabulary.AmountAtMost => gameEvent.Amount <= condition.Value,
            RuleVocabulary.ValueAtLeast => gameEvent.Value(condition.Text) >= condition.Value,
            RuleVocabulary.ValueAtMost => gameEvent.Value(condition.Text) <= condition.Value,
            RuleVocabulary.SourceIsSelf => string.Equals(gameEvent.Source, condition.Text, StringComparison.Ordinal),
            RuleVocabulary.TargetIsSelf => string.Equals(gameEvent.Target, condition.Text, StringComparison.Ordinal),
            RuleVocabulary.SelfHealthBelow => gameEvent.Value("self_health_fraction") < condition.Value,
            RuleVocabulary.SelfHealthAbove => gameEvent.Value("self_health_fraction") > condition.Value,
            RuleVocabulary.FirstInEncounter => gameEvent.Value("encounter_index") <= 1,

            // A named gauge needs the world; an unnamed one keeps reading the event's
            // `gauge_fraction`, which is the fullest meter and what shipped content means.
            RuleVocabulary.GaugeAtLeast => (string.IsNullOrEmpty(condition.Text) || world is null
                ? gameEvent.Value("gauge_fraction")
                : world.GaugeFraction(condition.Text)) >= condition.Value,

            RuleVocabulary.HitHasLane => gameEvent.HasTag(RuleVocabulary.LaneTagPrefix + condition.Text),

            RuleVocabulary.TargetHasStatus => world?.HasStatus(gameEvent.Target, condition.Text) ?? false,
            RuleVocabulary.SelfHasStatus => world?.SelfHasStatus(condition.Text) ?? false,
            RuleVocabulary.ResourceAbove => (world?.SelfResourceFraction(condition.Text) ?? 0) > condition.Value,
            RuleVocabulary.ResourceBelow => (world?.SelfResourceFraction(condition.Text) ?? 1) < condition.Value,
            RuleVocabulary.EquippedTag => world?.HasEquippedTag(condition.Text) ?? false,

            _ => throw new NotSupportedException($"Unknown condition kind '{condition.Kind}'."),
        };

        return condition.Negate ? !result : result;
    }

    /// <summary>
    /// Whether this condition can be answered without a world.
    ///
    /// <para>A named-gauge <c>gaugeAtLeast</c> counts: falling back to the fullest meter when a
    /// specific one was asked for is a plausible wrong answer, and those are the ones worth
    /// refusing to give.</para>
    /// </summary>
    private static bool NeedsWorld(ConditionSpec condition) =>
        RuleVocabulary.WorldConditions.Contains(condition.Kind)
        || (string.Equals(condition.Kind, RuleVocabulary.GaugeAtLeast, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(condition.Text));

    private bool EvaluateHere(ConditionSpec condition, GameEvent gameEvent, string source)
    {
        if (_world is null && NeedsWorld(condition))
        {
            // Visibly inert rather than silently false — the same bargain `Unhandled` strikes for
            // effects (DECISIONS D23). A rule that can never pass its conditions is exactly as
            // dead as one whose effect goes nowhere, and just as worth surfacing.
            _unevaluated.Add($"{source}: {condition.Kind}");
            return false;
        }

        return Evaluate(condition, gameEvent, _world);
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private sealed record Registration(TriggerRule Rule, string Source);
}
