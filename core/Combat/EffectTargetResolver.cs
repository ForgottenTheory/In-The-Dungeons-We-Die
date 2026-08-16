using Dungeons.Randomness;
using Dungeons.Rules;

namespace Dungeons.Combat;

/// <summary>
/// Turns an <see cref="EffectTarget"/> selector into the combatants an effect actually acts on.
///
/// <para>This is the piece E3a's selectors were waiting for. Exploding Kneecaps' Guard
/// expression detonates against <see cref="EffectTarget.TriggerSource"/> and its Surge
/// expression around <see cref="EffectTarget.Self"/> — same effect kind, and this class is the
/// entire difference between them.</para>
///
/// <para><b>Known simplification:</b> <see cref="EffectTarget.Self"/> resolves to the player,
/// because every attached rule currently comes from the player's build. It becomes "the
/// combatant who owns the rule" when anything else owns one — the same debt
/// <see cref="CombatEncounter.SelfId"/> already carries.</para>
/// </summary>
public sealed class EffectTargetResolver
{
    private readonly CombatEncounter _encounter;
    private readonly IRandomSource _random;

    public EffectTargetResolver(CombatEncounter encounter, IRandomSource random)
    {
        _encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>
    /// Who <paramref name="invocation"/> acts on. Dead combatants are filtered out, so an effect
    /// that fires on the killing blow does not land on a corpse.
    /// </summary>
    public IReadOnlyList<Combatant> Resolve(EffectInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        if (!_encounter.IsActive)
            return Array.Empty<Combatant>();

        var trigger = invocation.Trigger;

        var candidates = invocation.Target switch
        {
            EffectTarget.TriggerTarget => One(_encounter.Find(trigger.Target)),
            EffectTarget.TriggerSource => One(_encounter.Find(trigger.Source)),
            EffectTarget.Self => One(_encounter.Player),
            EffectTarget.AllAllies => One(_encounter.Player),
            EffectTarget.AllEnemies => AliveEnemies(),
            EffectTarget.RandomEnemy => RandomEnemy(),
            EffectTarget.LowestHealthEnemy => LowestHealthEnemy(),
            _ => Array.Empty<Combatant>(),
        };

        return candidates.Where(c => c.IsAlive).ToList();
    }

    /// <summary>
    /// Everyone an area effect catches.
    ///
    /// <para><b>There is no positioning yet</b>, so "around the attacker" and "around you" both
    /// mean every living enemy — which happens to be exactly right for both of Exploding
    /// Kneecaps' expressions today. When positions exist this narrows to a radius around
    /// <see cref="Resolve"/>'s answer, and the selector starts doing work here too.</para>
    /// </summary>
    public IReadOnlyList<Combatant> ResolveArea(EffectInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return _encounter.IsActive ? AliveEnemies() : Array.Empty<Combatant>();
    }

    private static IReadOnlyList<Combatant> One(Combatant? combatant) =>
        combatant is null ? Array.Empty<Combatant>() : new[] { combatant };

    private List<Combatant> AliveEnemies() => _encounter.Enemies.Where(e => e.IsAlive).ToList();

    private IReadOnlyList<Combatant> RandomEnemy()
    {
        var alive = AliveEnemies();
        return alive.Count == 0 ? Array.Empty<Combatant>() : new[] { alive[_random.NextInt(0, alive.Count)] };
    }

    private IReadOnlyList<Combatant> LowestHealthEnemy()
    {
        var alive = AliveEnemies();
        if (alive.Count == 0)
            return Array.Empty<Combatant>();

        // OrderBy, not MinBy, so ties resolve by roster order rather than by whichever the
        // enumerator reached first — the simulation has to replay identically from a seed.
        return new[] { alive.OrderBy(e => e.Health.Current).ThenBy(e => e.Name, StringComparer.Ordinal).First() };
    }
}
