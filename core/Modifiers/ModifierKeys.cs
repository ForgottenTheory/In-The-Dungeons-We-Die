using Dungeons.Characters.Modifiers;

namespace Dungeons.Modifiers;

/// <summary>
/// Constants for the modifier keys that code references directly, and the bridge from the
/// legacy <see cref="StatId"/> enum.
///
/// <para>The JSON registry is authoritative for which keys <i>exist</i> — these constants only
/// exist so code that must name a key doesn't do it with a string literal. A bijection test
/// keeps the two in step, the same arrangement <see cref="Dungeons.Items.ItemProperties"/> has
/// with the property registry (DECISIONS D17).</para>
///
/// <para><see cref="StatId"/> is <b>not</b> being retired. It stays the enum for the seven
/// attributes and three resource maxima, because attributes are a genuinely closed set and
/// typed access to them is worth having. <see cref="From"/> is the bridge, so a single
/// modifier system serves both.</para>
/// </summary>
public static class ModifierKeys
{
    public const string Strength = "attr.strength";
    public const string Dexterity = "attr.dexterity";
    public const string Intelligence = "attr.intelligence";
    public const string Constitution = "attr.constitution";
    public const string Wisdom = "attr.wisdom";
    public const string Endurance = "attr.endurance";
    public const string Luck = "attr.luck";

    public const string MaxHealth = "resource.max_health";
    public const string MaxMana = "resource.max_mana";
    public const string MaxStamina = "resource.max_stamina";
    public const string GaugeMax = "resource.gauge.max";
    public const string GaugeGain = "resource.gauge.gain";

    public const string DamageFlat = "combat.damage.flat";
    public const string DamageMult = "combat.damage.mult";
    public const string CritChance = "combat.crit.chance";
    public const string CritMult = "combat.crit.mult";

    public const string IntervalMult = "combat.interval.mult";
    public const string WindupMult = "combat.windup.mult";
    public const string RecoveryMult = "combat.recovery.mult";

    public const string Armor = "combat.armor";
    public const string DamageTakenMult = "combat.damage_taken.mult";
    public const string BlockMult = "combat.block.mult";
    public const string DodgeChance = "combat.dodge.chance";

    public const string InterruptImmune = "combat.interrupt.immune";
    public const string Uncancellable = "combat.uncancellable";

    /// <summary>The modifier key a legacy <see cref="StatId"/> targets.</summary>
    public static string From(StatId stat) => stat switch
    {
        StatId.Strength => Strength,
        StatId.Dexterity => Dexterity,
        StatId.Intelligence => Intelligence,
        StatId.Constitution => Constitution,
        StatId.Wisdom => Wisdom,
        StatId.Endurance => Endurance,
        StatId.Luck => Luck,
        StatId.MaxHealth => MaxHealth,
        StatId.MaxMana => MaxMana,
        StatId.MaxStamina => MaxStamina,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "No modifier key for this stat."),
    };

    /// <summary>Every key a <see cref="StatId"/> maps to — used by the bijection test.</summary>
    public static IReadOnlyList<string> StatBacked =>
        Enum.GetValues<StatId>().Select(From).ToList();
}
