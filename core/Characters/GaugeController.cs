using Dungeons.Characters.Composition;

namespace Dungeons.Characters;

/// <summary>
/// One gauge's runtime state — the number behind a <see cref="GaugeDefinition"/>.
///
/// <para>Definitions describe the meter; this holds what it currently reads. Keeping them
/// separate is the same split materials and item instances already have: a Base's Charge gauge
/// is a kind of meter, and a character in a fight has a specific one at 62.</para>
/// </summary>
public sealed class GaugePool
{
    private long _lastGainTick;
    private long _lastAdvanceTick;

    public GaugePool(GaugeDefinition definition, long startTick = 0)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Reset(startTick);
    }

    public GaugeDefinition Definition { get; }

    public string Name => Definition.Name;

    public double Max => Definition.Max;

    public double Current { get; private set; }

    public double Fraction => Max <= 0 ? 0.0 : Current / Max;

    /// <summary>Adds to the gauge and restarts the decay grace period — gaining is activity.</summary>
    public double Add(double amount, long tick)
    {
        if (amount == 0)
            return 0;

        var before = Current;
        Current = Math.Clamp(Current + amount, 0, Max);

        if (amount > 0)
            _lastGainTick = tick;

        return Current - before;
    }

    /// <summary>Spends down to zero and reports what was actually available.</summary>
    public double Spend(double amount)
    {
        var spent = Math.Min(Math.Max(0, amount), Current);
        Current -= spent;
        return spent;
    }

    /// <summary>
    /// Applies regeneration and decay for the ticks since the last advance.
    ///
    /// <para>Decay is charged only for the ticks that actually fell outside the grace window, so
    /// a sweep that straddles the grace boundary bleeds the right amount rather than the whole
    /// interval. Gauges ride the same periodic sweep statuses do, which is what keeps them
    /// deterministic under a seed.</para>
    /// </summary>
    public void Advance(long tick)
    {
        var elapsed = tick - _lastAdvanceTick;
        if (elapsed <= 0)
            return;

        if (Definition.RegenPerTick > 0)
            Current = Math.Clamp(Current + (Definition.RegenPerTick * elapsed), 0, Max);

        if (Definition.DecayPerTick > 0)
        {
            var decayFrom = Math.Max(_lastAdvanceTick, _lastGainTick + Definition.DecayGraceTicks);
            var decayTicks = Math.Max(0, tick - decayFrom);
            if (decayTicks > 0)
                Current = Math.Clamp(Current - (Definition.DecayPerTick * decayTicks), 0, Max);
        }

        _lastAdvanceTick = tick;
    }

    public void Reset(long tick)
    {
        Current = Definition.StartsFull ? Max : 0.0;
        _lastGainTick = tick;
        _lastAdvanceTick = tick;
    }

    /// <summary>The bands currently satisfied, as modifier key/value pairs.</summary>
    public IEnumerable<(string Key, double Value)> ActiveBands()
    {
        var fraction = Fraction;
        foreach (var band in Definition.Bands.Where(b => b.Contains(fraction)))
            yield return (band.Modifier, band.Value);
    }
}

/// <summary>
/// Every gauge a build is running, and the thing <c>grantResource</c> actually fills.
///
/// <para>All fifteen authored <c>grantResource</c> effects name a gauge — Charge, Momentum,
/// Threat, Debt — not Health or Stamina. Without this the whole gauge layer of the class
/// combinator was authored, validated, and inert: the feeds fired and their effects landed in
/// <c>Unhandled</c>.</para>
/// </summary>
public sealed class GaugeController
{
    private readonly Dictionary<string, GaugePool> _pools = new(StringComparer.OrdinalIgnoreCase);

    public GaugeController(IEnumerable<GaugeDefinition> gauges, long startTick = 0)
    {
        ArgumentNullException.ThrowIfNull(gauges);
        foreach (var gauge in gauges)
            _pools[gauge.Name] = new GaugePool(gauge, startTick);
    }

    public IReadOnlyCollection<GaugePool> Pools => _pools.Values;

    /// <summary>
    /// Replaces the gauge set — a build swap changes which meters exist.
    ///
    /// <para>Reconfiguring in place rather than constructing a new controller is what lets the
    /// encounter hold one stable reference while the Character Lab swaps components underneath
    /// it. Values reset, because a Charge total means nothing on a Base that no longer has
    /// Charge.</para>
    /// </summary>
    public void Reconfigure(IEnumerable<GaugeDefinition> gauges, long tick)
    {
        ArgumentNullException.ThrowIfNull(gauges);

        _pools.Clear();
        foreach (var gauge in gauges)
            _pools[gauge.Name] = new GaugePool(gauge, tick);
    }

    public int Count => _pools.Count;

    public bool Has(string name) => _pools.ContainsKey(name);

    public GaugePool? Find(string name) => _pools.GetValueOrDefault(name);

    /// <summary>Adds to a named gauge. Returns what actually landed — 0 if no such gauge.</summary>
    public double Add(string name, double amount, long tick) =>
        _pools.TryGetValue(name, out var pool) ? pool.Add(amount, tick) : 0.0;

    public double Fraction(string name) => _pools.TryGetValue(name, out var pool) ? pool.Fraction : 0.0;

    public double Current(string name) => _pools.TryGetValue(name, out var pool) ? pool.Current : 0.0;

    /// <summary>
    /// What the <c>gaugeAtLeast</c> condition reads.
    ///
    /// <para><b>Known limitation:</b> the condition names no gauge, and a build can run two (one
    /// from the Base, one from the Prefix). The highest fill is used, which is right for every
    /// shipped single-gauge build and ambiguous for a two-gauge one. The fix is a gauge name on
    /// the condition, which belongs with the rest of the condition work.</para>
    /// </summary>
    public double HighestFraction => _pools.Count == 0 ? 0.0 : _pools.Values.Max(p => p.Fraction);

    public void Advance(long tick)
    {
        foreach (var pool in _pools.Values)
            pool.Advance(tick);
    }

    public void Reset(long tick)
    {
        foreach (var pool in _pools.Values)
            pool.Reset(tick);
    }
}
