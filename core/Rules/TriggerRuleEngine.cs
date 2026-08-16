using Dungeons.Events;
using Dungeons.Randomness;

namespace Dungeons.Rules;

/// <summary>An effect actually happening: the spec, its resolved magnitude, and what caused it.</summary>
public sealed record EffectInvocation(EffectSpec Effect, double Magnitude, GameEvent Trigger, string Source)
{
    public string Kind => Effect.Kind;
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
    }

    private void OnEvent(GameEvent gameEvent)
    {
        foreach (var registration in _rules.ToList())
        {
            var rule = registration.Rule;

            if (!string.Equals(rule.Event, gameEvent.Kind, StringComparison.Ordinal))
                continue;

            var cooldownKey = registration.Source + "|" + rule.Id;
            if (_readyAt.TryGetValue(cooldownKey, out var readyAt) && _currentTick() < readyAt)
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

            Dispatch(new EffectInvocation(
                rule.Effect, rule.Effect.Magnitude(gameEvent), gameEvent, registration.Source));
        }
    }

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
