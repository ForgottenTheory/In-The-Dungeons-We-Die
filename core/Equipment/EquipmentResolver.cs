using Dungeons.Combat;

namespace Dungeons.Items;

/// <summary>Placeholder constants for how material properties adjust equipment stats.</summary>
public static class EquipmentTuning
{
    public const double DamagePerMass = 1.0;     // heavier weapons hit harder…
    public const double WindupTicksPerMass = 2;  // …but swing slower
    public const double ArmorPerHardness = 0.5;  // harder armour mitigates more
}

/// <summary>
/// Resolves an equipment definition + its worn instance into the neutral combat
/// profiles combat consumes.
/// <para>
/// The property→stat mapping here is deliberately a <b>small, illustrative seam</b>
/// (Mass → damage/speed, Hardness → armour). This is the single place the future
/// material→combat rules (Heat/Cold/Charge/Toxicity/Growth/Decay/Arcane → on-hit
/// effects, resistances, status) will grow — combat never needs to change
/// (docs/itemization.md §3).
/// </para>
/// </summary>
public static class EquipmentResolver
{
    public static AttackProfile ResolveWeapon(EquipmentDefinition definition, ItemInstance? instance, AttackProfile unarmed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(unarmed);
        if (definition.Weapon is null)
            return unarmed;

        var props = instance?.Properties ?? definition.BaseProperties;
        var mass = props.Get(ItemProperties.Mass);
        var w = definition.Weapon;

        return new AttackProfile
        {
            Name = !string.IsNullOrEmpty(instance?.DisplayName) ? instance!.DisplayName : definition.Name,
            DamageType = w.DamageType,
            BaseDamage = w.BaseDamage + (mass * EquipmentTuning.DamagePerMass),
            StaminaCost = w.StaminaCost,
            Timing = new AbilityTiming
            {
                TelegraphTicks = w.Timing.TelegraphTicks,
                WindupTicks = w.Timing.WindupTicks + (int)Math.Round(mass * EquipmentTuning.WindupTicksPerMass),
                RecoveryTicks = w.Timing.RecoveryTicks,
            },
        };
    }

    public static ArmorProfile ResolveArmor(EquipmentDefinition definition, ItemInstance? instance)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Armor is null)
            return ArmorProfile.None;

        var props = instance?.Properties ?? definition.BaseProperties;
        var hardness = props.Get(ItemProperties.Hardness);

        // Keyed by DamageLane, not by damage-type name (D-02). Future: derive lane resistances
        // from instance reactive properties here — insulation → heat/cold, etc.
        var resistances = new Dictionary<string, double>(definition.Armor.Resistances, StringComparer.OrdinalIgnoreCase);

        return new ArmorProfile
        {
            Armor = definition.Armor.Armor + (hardness * EquipmentTuning.ArmorPerHardness),
            Resistances = resistances,
        };
    }
}
