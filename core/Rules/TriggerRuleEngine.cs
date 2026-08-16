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
/// Evaluates declarative <see cref="TriggerRule"/>s against the event bus.
///
/// <para>This is the whole reason Prefixes and Suffixes can be data. A rule names an event, a
/// few conditions and an effect; this class does the matching, the cooldowns and the dispatch.
/// Nothing here knows what a Prefix is, and nothing here knows what a Base is.</para>
///
/// <para>Deterministic given a seed: the only randomness is <see cref="TriggerRule.Chance"/>,
/// rolled through the injected source, and rules are evaluated in registration order.</para>
/// </summary>
public sealed class TriggerRuleEngine : IDisposable
{
    private readonly IGameEventBus _bus;
    private readonly IRandomSource _random;
    private readonly Func<long> _currentTick;
    private readonly Dictionary<string, IEffectHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Registration> _rules = new();
    private readonly Dictionary<string, long> _readyAt = new(StringComparer.Ordinal);
    private readonly List<EffectInvocation> _unhandled = new();
    private readonly HashSet<string> _firedInChain = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _chainEffectCount = new(StringComparer.Ordinal);
    private IDisposable? _subscription;

    public TriggerRuleEngine(IGameEventBus bus, IRandomSource random, Func<long> currentTick)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _currentTick = currentTick ?? throw new ArgumentNullException(nameof(currentTick));
        _subscription = _bus.Subscribe(OnEvent);
    }

    /// <summary>Effects that fired with no handler registered. Content that references a system
    /// which does not exist yet lands here rather than vanishing — the Character Lab surfaces it
    /// so half-wired content is visible instead of mysteriously inert.</summary>
    public IReadOnlyList<EffectInvocation> Unhandled => _unhandled;

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
        _readyAt.Clear();
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
            if (_readyAt.TryGetValue(cooldownKey, out var readyAt) && _currentTick() < readyAt)
                continue;

            // Per-target internal cooldown. Chosen over PoE-style proc coefficients deliberately:
            // an ICD is readable in a tooltip ("once every 2s") and a proc coefficient is not.
            var icdKey = cooldownKey + "|" + (gameEvent.Target ?? string.Empty);
            if (rule.Proc.IcdTicks > 0
                && _readyAt.TryGetValue(icdKey, out var targetReadyAt)
                && _currentTick() < targetReadyAt)
                continue;

            if (!rule.When.All(condition => Evaluate(condition, gameEvent)))
                continue;

            // Rolled after conditions, so a chance-gated rule doesn't burn entropy on events
            // that were never going to qualify — which keeps seeded runs stable when unrelated
            // content changes.
            if (rule.Chance < 1.0 && _random.NextDouble() >= rule.Chance)
                continue;

            if (rule.CooldownTicks > 0)
                _readyAt[cooldownKey] = _currentTick() + rule.CooldownTicks;
            if (rule.Proc.IcdTicks > 0)
                _readyAt[icdKey] = _currentTick() + rule.Proc.IcdTicks;

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
                    Target = rule.Target,
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

    private void Dispatch(EffectInvocation invocation)
    {
        Fired.Add(invocation);

        if (_handlers.TryGetValue(invocation.Kind, out var handler))
            handler.Execute(invocation);
        else
            _unhandled.Add(invocation);
    }

    /// <summary>Condition evaluation. One method, one switch — adding a condition kind is a case
    /// here plus an entry in <see cref="RuleVocabulary.Conditions"/>.</summary>
    public static bool Evaluate(ConditionSpec condition, GameEvent gameEvent)
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
            RuleVocabulary.GaugeAtLeast => gameEvent.Value("gauge_fraction") >= condition.Value,
            RuleVocabulary.FirstInEncounter => gameEvent.Value("encounter_index") <= 1,
            _ => throw new NotSupportedException($"Unknown condition kind '{condition.Kind}'."),
        };

        return condition.Negate ? !result : result;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private sealed record Registration(TriggerRule Rule, string Source);
}
