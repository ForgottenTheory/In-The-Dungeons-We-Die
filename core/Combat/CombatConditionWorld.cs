using Dungeons.Rules;

namespace Dungeons.Combat;

/// <summary>
/// Combat's answers to <see cref="IConditionWorld"/> (E3c-3).
///
/// <para>Everything here reads live state and computes nothing. That is the point: a condition
/// must be cheap and side-effect-free, because it runs for every attached rule on every event
/// and a rule that changed the world while deciding whether to fire would make the order rules
/// were attached in a gameplay mechanic.</para>
/// </summary>
public sealed class CombatConditionWorld : IConditionWorld
{
    private readonly CombatEncounter _encounter;
    private readonly Func<IEnumerable<string>> _equippedTags;

    /// <param name="equippedTags">
    /// The tags of everything currently worn. Injected rather than read from an equipment type,
    /// so Core's combat layer keeps knowing nothing about inventory.
    /// </param>
    public CombatConditionWorld(CombatEncounter encounter, Func<IEnumerable<string>>? equippedTags = null)
    {
        _encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        _equippedTags = equippedTags ?? Array.Empty<string>;
    }

    public bool HasStatus(string? combatantId, string statusId)
    {
        if (_encounter.Statuses is null || string.IsNullOrWhiteSpace(statusId))
            return false;

        var combatant = _encounter.Find(combatantId);
        return combatant is not null && _encounter.Statuses.Has(combatant, statusId);
    }

    public bool SelfHasStatus(string statusId) => HasStatus(CombatEncounter.SelfId, statusId);

    public double SelfResourceFraction(string resource)
    {
        if (!_encounter.IsActive)
            return 0;

        var player = _encounter.Player;
        var pool = resource.ToLowerInvariant() switch
        {
            "health" => player.Health,
            "stamina" => player.Stamina,
            "mana" => player.Mana,
            _ => null,
        };

        return pool is null || pool.Max <= 0 ? 0 : (double)pool.Current / pool.Max;
    }

    public double GaugeFraction(string gaugeName) =>
        string.IsNullOrWhiteSpace(gaugeName)
            ? _encounter.Gauges?.HighestFraction ?? 0
            : _encounter.Gauges?.Fraction(gaugeName) ?? 0;

    public bool HasEquippedTag(string tag) =>
        !string.IsNullOrWhiteSpace(tag)
        && _equippedTags().Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
}
