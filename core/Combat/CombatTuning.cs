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

    /// <summary>Applied to the hit total, not per packet, so a hybrid hit cannot floor twice.</summary>
    public const int MinimumDamage = 1;

    /// <summary>
    /// Armour's diminishing constant: <c>reduction = armour / (armour + K × packet)</c>
    /// (D-25, D-27). Replaces flat subtraction, which was total against chip damage (a
    /// 5-damage hit became 1) and near-irrelevant against a telegraphed smash — backwards for a
    /// defensive investment in a telegraph game.
    /// <para>
    /// <b>K = 1, not PoE's 10.</b> The formula shape is borrowed; the constant must not be. PoE
    /// armour is in the thousands, ours is single-digit (iron + CON 5 ≈ 10), so a PoE-scaled K
    /// gutted armour instead of reshaping it — 18% against a light hit rather than 53%.
    /// <b>Recalibrate in C2</b>, when material properties start driving equipment stats.
    /// </para>
    /// </summary>
    public const double ArmourK = 1.0;

    /// <summary>Default cap on resistance in any one lane.</summary>
    public const double MaxResistance = 0.75;

    /// <summary>Hard ceiling that maximum-resistance affixes may raise <see cref="MaxResistance"/> to.</summary>
    public const double MaxResistanceCeiling = 0.90;

    /// <summary>Floor on effective resistance: −100% means at most double damage.</summary>
    public const double ResistanceFloor = -1.00;

    /// <summary>Enemy per-damage-type vulnerability multiplier is clamped here (D-02). Two-way.</summary>
    public const double MinVulnerability = 0.50;
    public const double MaxVulnerability = 1.50;

    /// <summary>
    /// How long after raising guard a block counts as <b>perfect</b> — negating the hit outright
    /// instead of reducing it (D-06). This is what gives the Bastion "precise blocks refund
    /// Guard" a mechanism, and gives the Guard channel two distinct events to hook.
    /// </summary>
    public const int PerfectBlockWindowTicks = 4;

    // --- Resolve: the one crowd-control mechanism (D-08, docs/statuses.md §4) ---------------

    /// <summary>Used when an actor declares no Resolve of its own.</summary>
    public const double DefaultResolve = 50;

    /// <summary>Fraction of max Resolve that control buildup bleeds off each tick.</summary>
    public const double ResolveDecayPerTick = 0.015;

    /// <summary>
    /// After a control lands, <b>all</b> controls are blocked for this long. Shared rather than
    /// per-type, which is what stops a build rotating Stun → Fear → Freeze to keep a target
    /// permanently disabled.
    /// </summary>
    public const int ControlImmunityTicks = 60;

    /// <summary>
    /// Resolve gained permanently, for the rest of the encounter, each time a control lands.
    /// Uncapped on purpose: there is no number of stacks that beats a boss, and a CC build
    /// naturally transitions from locking to punctuating as a fight goes on — the in-fight arc a
    /// flat diminishing-returns ladder cannot produce.
    /// </summary>
    public const double ResolveEscalation = 0.25;

    // Passive stamina recovery during combat.
    public const int StaminaRegenIntervalTicks = 15;
    public const int StaminaRegenAmount = 2;

    /// <summary>How often the status sweep runs. One shared sweep keeps ordering deterministic.</summary>
    public const int StatusTickIntervalTicks = 5;

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
