using Dungeons.Characters;
using Dungeons.Content;
using Dungeons.Modifiers;

namespace Dungeons.Combat;

/// <summary>
/// Modifiers granted for a span of ticks by the <c>grantModifier</c> effect.
///
/// <para>Fourteen authored effects grant one of these and every one names a duration. Statuses
/// already carry timed modifiers through <c>while_active</c>, but a status is a <i>named,
/// visible, cleansable</i> thing with stacking rules; "+20% damage for 40 ticks" from a proc is
/// none of those. Modelling it as an anonymous status would have put an unnamed entry on the
/// status bar and made it dispellable, so it gets its own list.</para>
/// </summary>
public sealed class TimedModifiers
{
    private readonly List<Entry> _grants = new();

    private sealed record Entry(Combatant Target, ModifierContribution Contribution, long ExpiresTick);

    /// <summary>
    /// Grants a modifier until <paramref name="durationTicks"/> elapses. A duration of 0 lasts
    /// the encounter — the one authored grant that omits it is a permanent armour bonus.
    /// </summary>
    public void Grant(
        Combatant target, string key, double value, string source, int durationTicks, long nowTick,
        ModifierScope? scope = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var expires = durationTicks > 0 ? nowTick + durationTicks : long.MaxValue;
        _grants.Add(new Entry(target, new ModifierContribution(key, value, source, scope), expires));
    }

    /// <summary>Drops everything that has run out. Rides the same sweep statuses and gauges do.</summary>
    public void Advance(long tick) => _grants.RemoveAll(g => g.ExpiresTick <= tick);

    public IEnumerable<ModifierContribution> On(Combatant target) =>
        _grants.Where(g => ReferenceEquals(g.Target, target)).Select(g => g.Contribution);

    public int Count => _grants.Count;

    public void Clear() => _grants.Clear();
}

/// <summary>
/// The one place a combatant's modifiers are assembled, and the first thing in the game that
/// actually <i>reads</i> them.
///
/// <para>Everything contributing was already authored and already validated, and none of it
/// reached a fight: <c>ResolvedBuild.Modifiers</c> had no consumer, <c>StatusController</c>
/// computed <c>ModifierTotal</c> for nobody, gauge bands were declared and ignored, and
/// <c>grantModifier</c> landed in <c>Unhandled</c>. Four inert systems, one missing seam.</para>
///
/// <para>Assembled per query rather than cached. A set is a few dozen contributions and a hit
/// resolves a handful of keys, so the cost is nothing next to a cache that can go stale halfway
/// through a chain of procs.</para>
/// </summary>
public sealed class CombatantModifiers
{
    private readonly DataStore<ModifierKeyDefinition> _keys;
    private readonly Func<Combatant, bool> _isOwner;
    private readonly Func<IEnumerable<ModifierContribution>> _buildModifiers;
    private readonly StatusController? _statuses;
    private readonly GaugeController? _gauges;

    /// <param name="isOwner">
    /// Which combatant the build and its gauges belong to. Build modifiers are the player's; an
    /// enemy gets statuses and grants only.
    /// </param>
    public CombatantModifiers(
        DataStore<ModifierKeyDefinition> keys,
        Func<Combatant, bool> isOwner,
        Func<IEnumerable<ModifierContribution>> buildModifiers,
        StatusController? statuses = null,
        GaugeController? gauges = null,
        TimedModifiers? timed = null)
    {
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _isOwner = isOwner ?? throw new ArgumentNullException(nameof(isOwner));
        _buildModifiers = buildModifiers ?? throw new ArgumentNullException(nameof(buildModifiers));
        _statuses = statuses;
        _gauges = gauges;
        Timed = timed ?? new TimedModifiers();
    }

    public TimedModifiers Timed { get; }

    /// <summary>Everything currently modifying <paramref name="combatant"/>, with provenance.</summary>
    public ModifierSet For(Combatant combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);

        var set = new ModifierSet(_keys);
        var owner = _isOwner(combatant);

        if (owner)
        {
            foreach (var contribution in _buildModifiers())
                AddIfKnown(set, contribution);

            // Gauge bands: what the meter's *level* does, as opposed to what spending it does.
            if (_gauges is not null)
            {
                foreach (var pool in _gauges.Pools)
                foreach (var (key, value) in pool.ActiveBands())
                    AddIfKnown(set, new ModifierContribution(key, value, $"{pool.Name} gauge"));
            }
        }

        if (_statuses is not null)
        {
            foreach (var instance in _statuses.On(combatant))
            foreach (var modifier in instance.Definition.WhileActive)
            {
                AddIfKnown(set, new ModifierContribution(
                    modifier.Key, instance.Contribution(modifier), instance.Definition.Name));
            }
        }

        foreach (var contribution in Timed.On(combatant))
            AddIfKnown(set, contribution);

        return set;
    }

    /// <summary>
    /// Resolves <paramref name="key"/> for <paramref name="combatant"/> in one call.
    /// <paramref name="context"/> is required and never defaulted, for the reason D-12 gives.
    /// </summary>
    public double Resolve(Combatant combatant, string key, ModifierContext context, double? baseValue = null) =>
        For(combatant).Resolve(key, context, baseValue);

    /// <summary>
    /// Unknown keys are skipped rather than thrown.
    ///
    /// <para>This is the one place that bargain is worth making, and only because the content
    /// that could carry a bad key is already checked: <c>ContentValidator</c> rejects an
    /// unregistered key in a status, a gauge band or an effect at load. Throwing here would turn
    /// a stale key in a save file into an unplayable fight rather than a missing bonus.</para>
    /// </summary>
    private void AddIfKnown(ModifierSet set, ModifierContribution contribution)
    {
        if (_keys.Contains(contribution.Key))
            set.Add(contribution.Key, contribution.Value, contribution.Source, contribution.Scope);
    }
}
