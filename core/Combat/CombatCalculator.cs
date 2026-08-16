using Dungeons.Randomness;

namespace Dungeons.Combat;

/// <summary>
/// Thin compatibility façade over <see cref="HitPipeline"/>.
///
/// <para>E1 replaced this class's body: resolution is now an ordered, traced pipeline over
/// <see cref="Packet"/>s (docs/damage-and-defense.md §3). The <c>(damageType, baseDamage)</c>
/// entry point survives only as the <b>D-18 bridge</b> — an <see cref="AttackProfile"/> is a
/// degenerate Move carrying one aspectless packet. When <c>MoveDefinition</c> lands in E4,
/// callers pass a <see cref="Hit"/> directly and this type is deleted.</para>
/// </summary>
public sealed class CombatCalculator
{
    private readonly HitPipeline _pipeline;

    public CombatCalculator(IRandomSource rng, CombatantModifiers? modifiers = null) =>
        _pipeline = new HitPipeline(rng, modifiers);

    /// <summary>Resolves a single-packet attack. Prefer <see cref="Resolve(Hit, long)"/>.</summary>
    public HitResult Resolve(
        Combatant attacker, Combatant target, DamageType type, double baseDamage, long currentTick) =>
        Resolve(
            new Hit
            {
                Source = attacker,
                Target = target,
                Name = type.ToString(),
                Packets = Hit.ToPackets(type, baseDamage),
            },
            currentTick);

    public HitResult Resolve(Hit hit, long currentTick) => _pipeline.Resolve(hit, currentTick);
}
