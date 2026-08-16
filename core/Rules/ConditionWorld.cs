namespace Dungeons.Rules;

/// <summary>
/// World state a condition may interrogate (E3c-3).
///
/// <para>Every condition before this slice was a pure function of the <c>GameEvent</c>, which is
/// why <see cref="TriggerRuleEngine.Evaluate"/> could be static. "Only while the target is
/// Chilled" and "only below 30% Stamina" are not answerable from an event, and writing that
/// state into every event instead would mean every publisher guessing what every future
/// condition might want.</para>
///
/// <para>Deliberately <b>narrow</b>: four questions, no entity graph, no queries. A condition
/// vocabulary that can ask anything becomes a query language in content, and the whole point of
/// the closed vocabularies is that content cannot invent mechanics — only combine them.</para>
///
/// <para>Identity is the event's string id (<c>CombatEncounter.SelfId</c> or an enemy name), so
/// <c>Dungeons.Rules</c> stays free of combat types. The implementation resolves it.</para>
/// </summary>
public interface IConditionWorld
{
    /// <summary>Whether the named combatant currently carries a status.</summary>
    bool HasStatus(string? combatantId, string statusId);

    /// <summary>Whether the rule's owner carries a status. Separate from
    /// <see cref="HasStatus"/> so the rules layer never needs to know who "self" is.</summary>
    bool SelfHasStatus(string statusId);

    /// <summary>The owner's fill fraction (0–1) for a named pool — <c>health</c>, <c>stamina</c>,
    /// <c>mana</c>. Unknown names read 0.</summary>
    double SelfResourceFraction(string resource);

    /// <summary>The owner's fill fraction (0–1) for a named gauge. Unknown names read 0.</summary>
    double GaugeFraction(string gaugeName);

    /// <summary>Whether anything the owner has equipped carries the tag.</summary>
    bool HasEquippedTag(string tag);
}

/// <summary>
/// Forwards to a world that does not exist yet.
///
/// <para>The rule engine is constructed before the encounter is, because the build's hooks must
/// be attached from the first frame. Rather than reorder composition — or leave the engine
/// worldless and have every stateful condition silently fail for the life of the process — the
/// engine takes this, and it resolves the real provider on each call.</para>
/// </summary>
public sealed class DeferredConditionWorld : IConditionWorld
{
    private readonly Func<IConditionWorld?> _resolve;

    public DeferredConditionWorld(Func<IConditionWorld?> resolve) =>
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));

    public bool HasStatus(string? combatantId, string statusId) =>
        _resolve()?.HasStatus(combatantId, statusId) ?? false;

    public bool SelfHasStatus(string statusId) => _resolve()?.SelfHasStatus(statusId) ?? false;

    public double SelfResourceFraction(string resource) => _resolve()?.SelfResourceFraction(resource) ?? 0;

    public double GaugeFraction(string gaugeName) => _resolve()?.GaugeFraction(gaugeName) ?? 0;

    public bool HasEquippedTag(string tag) => _resolve()?.HasEquippedTag(tag) ?? false;
}
