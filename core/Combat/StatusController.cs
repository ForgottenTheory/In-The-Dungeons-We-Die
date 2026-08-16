using Dungeons.Content;
using Dungeons.Events;

namespace Dungeons.Combat;

/// <summary>Why a control attempt did not land.</summary>
public enum ControlOutcome
{
    /// <summary>Buildup added, threshold not crossed.</summary>
    Resisted,

    /// <summary>Target is inside its post-control immunity window; no buildup was added.</summary>
    Immune,

    /// <summary>Gate status missing — Freeze on an unchilled target.</summary>
    Ungated,

    Applied,
}

/// <summary>
/// Owns every combatant's statuses, and the Resolve pool that gates control effects
/// (docs/statuses.md §4, §6).
///
/// <para>Deliberately narrow: this manages <b>lifetime only</b> — apply, stack, tick, expire,
/// cleanse — because everything a status <i>does</i> already has a home. Its modifiers are
/// ordinary contributions and its hooks are ordinary <c>EffectSpec</c>s. That is why fourteen
/// statuses cost roughly what three would.</para>
/// </summary>
public sealed class StatusController
{
    private readonly DataStore<StatusDefinition> _definitions;

    private readonly IGameEventBus _bus;
    private readonly Func<long> _currentTick;

    private readonly Dictionary<Combatant, List<StatusInstance>> _active = new();
    private readonly Dictionary<Combatant, Dictionary<string, double>> _buildup = new();
    private readonly Dictionary<Combatant, double> _resolveBonus = new();
    private readonly Dictionary<Combatant, long> _immuneUntil = new();

    public StatusController(DataStore<StatusDefinition> definitions, IGameEventBus bus, Func<long> currentTick)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _currentTick = currentTick ?? throw new ArgumentNullException(nameof(currentTick));
    }

    /// <summary>The status library, for callers that need to size a magnitude before applying.</summary>
    public DataStore<StatusDefinition> Definitions => _definitions;

    /// <summary>Raised when a status ticks, so combat can apply the damage it describes.</summary>
    public event Action<Combatant, StatusInstance>? Ticked;

    public IReadOnlyList<StatusInstance> On(Combatant combatant) =>
        _active.TryGetValue(combatant, out var list) ? list : Array.Empty<StatusInstance>();

    public bool Has(Combatant combatant, string statusId) =>
        On(combatant).Any(s => string.Equals(s.Id, statusId, StringComparison.OrdinalIgnoreCase));

    public StatusInstance? Find(Combatant combatant, string statusId) =>
        On(combatant).FirstOrDefault(s => string.Equals(s.Id, statusId, StringComparison.OrdinalIgnoreCase));

    // --- Resolve ------------------------------------------------------------

    /// <summary>
    /// The control threshold. Rises permanently by <see cref="CombatTuning.ResolveEscalation"/>
    /// each time a control lands, for the rest of the encounter — which is what turns a CC build
    /// from *locking* into *punctuating* as a fight goes on, and is the thing a flat
    /// diminishing-returns ladder cannot produce.
    /// </summary>
    public double ResolveOf(Combatant combatant) =>
        BaseResolve(combatant) * (1.0 + _resolveBonus.GetValueOrDefault(combatant));

    public double BuildupOn(Combatant combatant, string statusId) =>
        _buildup.TryGetValue(combatant, out var pools) ? pools.GetValueOrDefault(statusId) : 0;

    public bool IsControlImmune(Combatant combatant) =>
        _currentTick() < _immuneUntil.GetValueOrDefault(combatant, long.MinValue);

    private static double BaseResolve(Combatant combatant) =>
        combatant.Resolve > 0 ? combatant.Resolve : CombatTuning.DefaultResolve;

    // --- Application --------------------------------------------------------

    /// <summary>
    /// Applies a status. Controls route through Resolve and may not land; everything else
    /// applies directly (docs/statuses.md §5).
    /// </summary>
    /// <param name="context">
    /// The causal chain this application belongs to, when a rule caused it. Threaded onto
    /// <c>StatusApplied</c>/<c>ControlResisted</c> so a status applied by a proc does not restart
    /// the chain at depth 0 — which would make the whole proc budget decorative
    /// (docs/effect-foundation.md §6.1).
    /// </param>
    public ControlOutcome Apply(
        Combatant target, string statusId, string sourceId, double magnitude = 0, int durationOverride = 0,
        Rules.EffectContext? context = null)
    {
        if (!_definitions.TryGetById(statusId, out var definition))
            return ControlOutcome.Ungated;

        if (definition.IsControl)
            return ApplyControl(target, definition, sourceId, magnitude, durationOverride, context);

        Land(target, definition, sourceId, magnitude, durationOverride, context);
        return ControlOutcome.Applied;
    }

    private ControlOutcome ApplyControl(
        Combatant target, StatusDefinition definition, string sourceId, double magnitude, int durationOverride,
        Rules.EffectContext? context)
    {
        // A gate is not a resistance — an unchilled target accumulates no Freeze at all.
        if (definition.RequiresStatus is { } gate && !Has(target, gate))
        {
            RaiseResisted(target, definition, sourceId, "ungated", context);
            return ControlOutcome.Ungated;
        }

        if (IsControlImmune(target))
        {
            RaiseResisted(target, definition, sourceId, "immune", context);
            return ControlOutcome.Immune;
        }

        var pools = _buildup.TryGetValue(target, out var existing)
            ? existing
            : _buildup[target] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var added = definition.ControlBuildup > 0 ? definition.ControlBuildup : magnitude;
        var total = pools.GetValueOrDefault(definition.Id) + added;
        var resolve = ResolveOf(target);

        if (total < resolve)
        {
            // Buildup is tracked per control type, but the threshold, the immunity window and
            // the escalation are shared — which is what stops a build rotating Stun → Fear →
            // Freeze to keep something permanently disabled.
            pools[definition.Id] = total;
            RaiseResisted(target, definition, sourceId, "buildup", context);
            return ControlOutcome.Resisted;
        }

        pools[definition.Id] = 0;
        _immuneUntil[target] = _currentTick() + CombatTuning.ControlImmunityTicks;
        _resolveBonus[target] = _resolveBonus.GetValueOrDefault(target) + CombatTuning.ResolveEscalation;

        Land(target, definition, sourceId, magnitude, durationOverride, context);
        return ControlOutcome.Applied;
    }

    private void Land(
        Combatant target, StatusDefinition definition, string sourceId, double magnitude, int durationOverride,
        Rules.EffectContext? context)
    {
        var now = _currentTick();
        var duration = durationOverride > 0 ? durationOverride : definition.DurationTicks;
        var list = _active.TryGetValue(target, out var existing) ? existing : _active[target] = new List<StatusInstance>();
        var current = list.FirstOrDefault(s => s.Id == definition.Id);

        switch (definition.StackPolicy)
        {
            case StackPolicy.Unique when current is not null:
                return;

            case StackPolicy.RefreshHighest when current is not null:
                // A stronger application overwrites; a weaker one only refreshes the clock.
                current.Magnitude = Math.Max(current.Magnitude, magnitude);
                current.ExpiresTick = now + duration;
                break;

            case StackPolicy.RefreshDuration when current is not null:
                current.ExpiresTick = now + duration;
                break;

            case StackPolicy.Stack when current is not null:
                current.Stacks = Math.Min(definition.MaxStacks, current.Stacks + 1);
                current.Magnitude = Math.Max(current.Magnitude, magnitude);
                current.ExpiresTick = now + duration;
                break;

            default:
                list.Add(new StatusInstance
                {
                    Definition = definition,
                    SourceId = sourceId,
                    AppliedTick = now,
                    Magnitude = magnitude,
                    ExpiresTick = now + duration,
                    NextTickAt = definition.TickInterval > 0 ? now + definition.TickInterval : long.MaxValue,
                });
                break;
        }

        _bus.Publish(new GameEvent(
            GameEvents.StatusApplied, sourceId, target.Name, magnitude,
            Tags(definition), Values(definition),
            ChainId: context?.ChainId, Depth: context?.Depth ?? 0));
    }

    private void RaiseResisted(
        Combatant target, StatusDefinition definition, string sourceId, string why,
        Rules.EffectContext? context = null) =>
        _bus.Publish(new GameEvent(
            GameEvents.ControlResisted, sourceId, target.Name, 0,
            new HashSet<string>(Tags(definition), StringComparer.OrdinalIgnoreCase) { why },
            Values(definition),
            ChainId: context?.ChainId, Depth: context?.Depth ?? 0));

    private static IReadOnlySet<string> Tags(StatusDefinition definition)
    {
        var tags = new HashSet<string>(definition.Tags, StringComparer.OrdinalIgnoreCase)
        {
            definition.Category.ToString().ToLowerInvariant(),
            definition.Id,
        };
        if (definition.Lane is not null)
            tags.Add(definition.Lane);
        return tags;
    }

    private static IReadOnlyDictionary<string, double> Values(StatusDefinition definition) =>
        new Dictionary<string, double>(StringComparer.Ordinal) { ["duration_ticks"] = definition.DurationTicks };

    // --- Removal ------------------------------------------------------------

    public bool Remove(Combatant target, string statusId)
    {
        if (!_active.TryGetValue(target, out var list))
            return false;

        var instance = list.FirstOrDefault(s => string.Equals(s.Id, statusId, StringComparison.OrdinalIgnoreCase));
        if (instance is null)
            return false;

        Expire(target, list, instance);
        return true;
    }

    /// <summary>Cleanses everything in a group — `ailment`, `impairment`, `control`, `state`.</summary>
    public int CleanseGroup(Combatant target, string group)
    {
        if (!_active.TryGetValue(target, out var list))
            return 0;

        var doomed = list.Where(s => string.Equals(s.Definition.Group, group, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var instance in doomed)
            Expire(target, list, instance);

        return doomed.Count;
    }

    private void Expire(Combatant target, List<StatusInstance> list, StatusInstance instance)
    {
        list.Remove(instance);
        _bus.Publish(new GameEvent(
            GameEvents.StatusExpired, instance.SourceId, target.Name, instance.Magnitude,
            Tags(instance.Definition), Values(instance.Definition)));
    }

    // --- The clock ----------------------------------------------------------

    /// <summary>
    /// Advances every status on every combatant. Called once per combat tick — statuses ride the
    /// shared <c>TickEngine</c> rather than scheduling individually, so ordering stays
    /// deterministic under a seed.
    /// </summary>
    public void Advance(IEnumerable<Combatant> combatants)
    {
        var now = _currentTick();

        foreach (var combatant in combatants)
        {
            if (_active.TryGetValue(combatant, out var list) && list.Count > 0)
            {
                foreach (var instance in list.ToList())
                {
                    // `<=` so a status ticks ON its expiry too: an authored `duration 60,
                    // interval 15` gives four ticks, which is the arithmetic the number looks
                    // like. Pre-empting the last tick silently docks every DoT one tick.
                    while (instance.Definition.TickInterval > 0
                           && now >= instance.NextTickAt
                           && now <= instance.ExpiresTick)
                    {
                        Ticked?.Invoke(combatant, instance);
                        instance.NextTickAt += instance.Definition.TickInterval;
                    }

                    if (now >= instance.ExpiresTick)
                        Expire(combatant, list, instance);
                }
            }

            // Buildup bleeds away, so pressure has to be sustained rather than accumulated
            // across a whole fight.
            //
            // Outside the status loop deliberately: a target part-way to being Stunned has
            // buildup and NO active status, which is exactly the case that has to decay. Nesting
            // this under the status check meant partial buildup never expired.
            if (_buildup.TryGetValue(combatant, out var pools))
                foreach (var key in pools.Keys.ToList())
                    pools[key] = Math.Max(0, pools[key] - (ResolveOf(combatant) * CombatTuning.ResolveDecayPerTick));
        }
    }

    /// <summary>Drops all status state. Called when an encounter ends — Resolve escalation is
    /// per-encounter by design.</summary>
    public void Clear()
    {
        _active.Clear();
        _buildup.Clear();
        _resolveBonus.Clear();
        _immuneUntil.Clear();
    }

    /// <summary>
    /// Total contribution of every active status toward a modifier key — how Chill slows a
    /// target and Corroded strips its armour, without either needing code.
    ///
    /// <para><b>This is the status-only subtotal, for display.</b> Combat resolves through
    /// <see cref="CombatantModifiers"/>, which folds these together with build modifiers, gauge
    /// bands and timed grants and applies the key's stacking rule and clamps. Answering "what is
    /// Chill doing to me?" is a different question from "what is my windup?", which is why both
    /// survive — but nothing authoritative may read this one.</para>
    /// </summary>
    public double ModifierTotal(Combatant combatant, string key, bool multiplicative = false)
    {
        var total = multiplicative ? 1.0 : 0.0;

        foreach (var instance in On(combatant))
            foreach (var modifier in instance.Definition.WhileActive)
            {
                if (!string.Equals(modifier.Key, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (multiplicative)
                    total *= instance.Contribution(modifier);
                else
                    total += instance.Contribution(modifier);
            }

        return total;
    }
}
