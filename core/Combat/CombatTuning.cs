namespace Dungeons.Combat;

/// <summary>Central combat balance constants (placeholder values for the vertical slice).</summary>
public static class CombatTuning
{
    // Defensive stance windows and costs.
    public const int BlockDurationTicks = 16;
    public const int DodgeDurationTicks = 10;
    public const int BlockStaminaCost = 6;
    public const int DodgeStaminaCost = 8;

    /// <summary>Damage multiplier applied when an attack lands on a blocking target.</summary>
    public const double BlockDamageMultiplier = 0.4;

    // Offensive scaling.
    public const double PhysicalScalingPerStrength = 0.5;
    public const double MagicScalingPerIntelligence = 0.5;
    public const double CritMultiplier = 1.5;
    public const double CritChancePerLuck = 0.01;
    public const double MaxCritChance = 0.5;

    // Mitigation.
    public const double ArmorPerConstitution = 0.3;
    public const int MinimumDamage = 1;

    /// <summary>Cap on typed resistance from equipped armor (fraction).</summary>
    public const double MaxResistance = 0.75;

    // Passive stamina recovery during combat.
    public const int StaminaRegenIntervalTicks = 15;
    public const int StaminaRegenAmount = 2;

    /// <summary>Attack tempo lost after using an item — you can still block/dodge, but not immediately strike.</summary>
    public const int ItemUseRecoveryTicks = 10;

    /// <summary>
    /// Time-to-impact at or above which an attack is tagged <c>heavy</c> in combat events.
    /// <para>
    /// Derived rather than authored, and the line is meaningful: 24 ticks is 1.2s at 20 ticks/s,
    /// long enough that the attack is something you *see coming and answer*. Overhead Smash sits
    /// at 48; every other attack in the game is 10–16. Replaced by real move tags in E4.
    /// </para>
    /// </summary>
    public const int HeavyTimeToImpactTicks = 24;
}
