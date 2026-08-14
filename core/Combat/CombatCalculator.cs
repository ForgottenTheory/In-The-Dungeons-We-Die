using Dungeons.Randomness;

namespace Dungeons.Combat;

/// <summary>The authoritative result of one attack resolution.</summary>
public sealed class DamageResult
{
    public required DamageType Type { get; init; }
    public required bool Dodged { get; init; }
    public required bool Blocked { get; init; }
    public required bool Crit { get; init; }

    /// <summary>Final damage to apply to the target's Health (0 if dodged).</summary>
    public required int Amount { get; init; }
}

/// <summary>
/// Resolves the combat damage pipeline (docs/combat-spec.md §16):
/// base → attribute scaling → crit → armor mitigation → block/dodge. It reads the
/// target's live defensive stance but does not mutate anything — the encounter
/// applies the result. Deterministic given the injected RNG.
/// </summary>
public sealed class CombatCalculator
{
    private readonly IRandomSource _rng;

    public CombatCalculator(IRandomSource rng) => _rng = rng ?? throw new ArgumentNullException(nameof(rng));

    public DamageResult Resolve(Combatant attacker, Combatant target, AbilityDefinition ability, long currentTick)
    {
        var type = ability.DamageType;

        if (target.IsDodging(currentTick))
            return new DamageResult { Type = type, Dodged = true, Blocked = false, Crit = false, Amount = 0 };

        var physical = DamageTypes.IsPhysical(type);
        var scaling = physical
            ? attacker.Attributes.Strength * CombatTuning.PhysicalScalingPerStrength
            : attacker.Attributes.Intelligence * CombatTuning.MagicScalingPerIntelligence;
        var raw = ability.BaseValue + scaling;

        var critChance = Math.Min(CombatTuning.MaxCritChance, attacker.Attributes.Luck * CombatTuning.CritChancePerLuck);
        var crit = _rng.NextDouble() < critChance;
        if (crit)
            raw *= CombatTuning.CritMultiplier;

        var armor = physical ? target.Attributes.Constitution * CombatTuning.ArmorPerConstitution : 0.0;
        var afterArmor = Math.Max(CombatTuning.MinimumDamage, raw - armor);

        var blocked = target.IsBlocking(currentTick);
        if (blocked)
            afterArmor *= CombatTuning.BlockDamageMultiplier;

        var amount = Math.Max(CombatTuning.MinimumDamage, (int)Math.Round(afterArmor, MidpointRounding.AwayFromZero));
        return new DamageResult { Type = type, Dodged = false, Blocked = blocked, Crit = crit, Amount = amount };
    }
}
