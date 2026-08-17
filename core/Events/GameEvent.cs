namespace Dungeons.Events;

/// <summary>
/// Something that happened, in a shape data-driven rules can match against.
///
/// <para>Deliberately <b>uniform rather than strongly typed per event</b>. Prefixes and
/// suffixes are authored as JSON, so a rule has to be able to ask "did this event have the
/// <c>heavy</c> tag" without a C# case per event kind. Well-known fields are named; anything
/// else rides in <see cref="Values"/> and <see cref="Tags"/>.</para>
///
/// <para>The vocabulary follows docs/architecture.md §14, which specified these events long
/// before anything raised them.</para>
/// </summary>
/// <param name="Kind">One of <see cref="GameEvents"/>.</param>
/// <param name="Source">Who caused it (combatant/actor id), if anyone.</param>
/// <param name="Target">Who it happened to, if anyone.</param>
/// <param name="Amount">The event's headline number — damage dealt, healing, resource spent.</param>
/// <param name="Tags">Free-form markers a condition can test: <c>heavy</c>, <c>critical</c>,
/// <c>slashing</c>, <c>spell</c>. Lowercase by convention.</param>
/// <param name="Values">Secondary numbers, e.g. <c>self_health_fraction</c>.</param>
/// <param name="ChainId">
/// The causal chain this belongs to. Null starts a new one. Carried as a bare string rather than
/// an <c>EffectContext</c> so <c>Dungeons.Events</c> stays dependency-free.
/// </param>
/// <param name="Depth">0 for a real action, 1 for a proc, 2 for a proc's proc.</param>
/// <param name="CanTrigger">
/// False bars this event from matching any rule at all, whatever its depth. Retaliation damage
/// and ailment ticks set it, and between them those two cases account for most of the
/// recursion the design has to survive (docs/effect-foundation.md §6.2).
/// </param>
public sealed record GameEvent(
    string Kind,
    string? Source = null,
    string? Target = null,
    double Amount = 0.0,
    IReadOnlySet<string>? Tags = null,
    IReadOnlyDictionary<string, double>? Values = null,
    string? ChainId = null,
    int Depth = 0,
    bool CanTrigger = true)
{
    private static readonly HashSet<string> NoTags = new(StringComparer.OrdinalIgnoreCase);

    public bool HasTag(string tag) => (Tags ?? NoTags).Contains(tag);

    /// <summary>A named secondary value, or 0 if the event didn't carry it.</summary>
    public double Value(string name) =>
        Values is not null && Values.TryGetValue(name, out var value) ? value : 0.0;

    public GameEvent With(params string[] tags) =>
        this with { Tags = new HashSet<string>((Tags ?? NoTags).Concat(tags), StringComparer.OrdinalIgnoreCase) };
}

/// <summary>
/// The event vocabulary. Constants rather than an enum so content can name events in JSON and
/// the validator can check them against this set — the same bargain the modifier registry makes.
/// </summary>
public static class GameEvents
{
    // Combat — the tick lifecycle
    public const string ActionQueued = "ActionQueued";
    public const string ActionTelegraphed = "ActionTelegraphed";
    public const string ActionResolved = "ActionResolved";
    public const string ActionInterrupted = "ActionInterrupted";
    public const string RecoveryStarted = "RecoveryStarted";
    public const string MoveExecuted = "MoveExecuted";

    // Combat — outcomes
    public const string DamageDealt = "DamageDealt";
    public const string DamageTaken = "DamageTaken";
    public const string CriticalLanded = "CriticalLanded";
    public const string Blocked = "Blocked";
    public const string Dodged = "Dodged";

    /// <summary>R4c-2 (D-06): a gear-granted parry landed — negation plus the counter-window.</summary>
    public const string Parried = "Parried";

    /// <summary>R4c-2 (D-06 §6.3): damage prevented by mitigation; `amount` is the prevented
    /// total — the basis for reflect-% retaliation and, later, stored retaliation.</summary>
    public const string DamageMitigated = "DamageMitigated";

    /// <summary>R4c-2: a Barrier absorbed its last point and shattered (D-06's sixth event).</summary>
    public const string BarrierBroken = "BarrierBroken";
    public const string Healed = "Healed";
    public const string StatusApplied = "StatusApplied";
    public const string StatusExpired = "StatusExpired";

    /// <summary>
    /// A control attempt failed to land — buildup added but Resolve not crossed, the target was
    /// inside its immunity window, or a gate status was missing. The hook for
    /// "when you resist control…" affixes (E2, docs/statuses.md §4.4).
    /// </summary>
    public const string ControlResisted = "ControlResisted";
    public const string Killed = "Killed";
    public const string Defeated = "Defeated";
    public const string Moved = "Moved";

    // Resources
    public const string ResourceGenerated = "ResourceGenerated";
    public const string ResourceSpent = "ResourceSpent";

    // Out of combat — suffixes reach these deliberately (docs/classes.md §6)
    public const string ItemReceived = "ItemReceived";
    public const string ChestOpened = "ChestOpened";
    public const string CraftCompleted = "CraftCompleted";
    public const string CraftFailed = "CraftFailed";
    public const string DiscoveryMade = "DiscoveryMade";
    public const string LocationDiscovered = "LocationDiscovered";
    public const string RealmEntered = "RealmEntered";
    public const string DepthChanged = "DepthChanged";
    public const string ExtractionCompleted = "ExtractionCompleted";
    public const string EncounterStarted = "EncounterStarted";
    public const string EncounterEnded = "EncounterEnded";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        ActionQueued, ActionTelegraphed, ActionResolved, ActionInterrupted, RecoveryStarted, MoveExecuted,
        DamageDealt, DamageTaken, CriticalLanded, Blocked, Dodged, Parried, DamageMitigated, BarrierBroken,
        Healed, StatusApplied, StatusExpired,
        Killed, Defeated, Moved, ResourceGenerated, ResourceSpent, ControlResisted,
        ItemReceived, ChestOpened, CraftCompleted, CraftFailed, DiscoveryMade, LocationDiscovered,
        RealmEntered, DepthChanged, ExtractionCompleted, EncounterStarted, EncounterEnded,
    };
}

/// <summary>Raises game events to whoever is listening. Engine-free.</summary>
public interface IGameEventBus
{
    void Publish(GameEvent gameEvent);
    IDisposable Subscribe(Action<GameEvent> handler);

    /// <summary>Subscribe to one kind only — the common case, and cheaper than filtering.</summary>
    IDisposable Subscribe(string eventKind, Action<GameEvent> handler);
}

/// <summary>
/// The default bus.
///
/// <para>Publication is <b>synchronous and ordered</b>: handlers run in subscription order and
/// finish before <see cref="Publish"/> returns. That is a deliberate determinism choice — an
/// async or queued bus would make the outcome of a craft or a combat tick depend on scheduling,
/// and the whole simulation is built on being reproducible from a seed.</para>
///
/// <para>Events raised <i>by</i> a handler are queued and drained after the current event
/// finishes, so a rule that causes damage cannot re-enter the same handler mid-flight.</para>
/// </summary>
public sealed class GameEventBus : IGameEventBus
{
    private readonly List<Subscription> _subscriptions = new();
    private readonly Queue<GameEvent> _pending = new();
    private bool _draining;

    public void Publish(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        _pending.Enqueue(gameEvent);
        if (_draining)
            return;

        _draining = true;
        try
        {
            while (_pending.Count > 0)
            {
                var next = _pending.Dequeue();

                // Snapshot: a handler may unsubscribe, and mutating mid-iteration would throw.
                foreach (var subscription in _subscriptions.ToList())
                {
                    if (subscription.Kind is null || string.Equals(subscription.Kind, next.Kind, StringComparison.Ordinal))
                        subscription.Handler(next);
                }
            }
        }
        finally
        {
            _draining = false;
        }
    }

    public IDisposable Subscribe(Action<GameEvent> handler) => Listen(null, handler);

    public IDisposable Subscribe(string eventKind, Action<GameEvent> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventKind);
        return Listen(eventKind, handler);
    }

    private IDisposable Listen(string? eventKind, Action<GameEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new Subscription(eventKind, handler, this);
        _subscriptions.Add(subscription);
        return subscription;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly GameEventBus _bus;

        public Subscription(string? kind, Action<GameEvent> handler, GameEventBus bus)
        {
            Kind = kind;
            Handler = handler;
            _bus = bus;
        }

        public string? Kind { get; }
        public Action<GameEvent> Handler { get; }

        public void Dispose() => _bus._subscriptions.Remove(this);
    }
}
