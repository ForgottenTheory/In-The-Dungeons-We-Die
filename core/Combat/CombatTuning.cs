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

    // Passive stamina recovery during combat.
    public const int StaminaRegenIntervalTicks = 15;
    public const int StaminaRegenAmount = 2;
}
