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
/// Resolves an equipment definition + its worn instance into what combat consumes.
///
/// <para>Since E4 a weapon resolves into <b>moves</b>, not an attack profile (D-18; D8's intent
/// preserved — combat still reads a neutral shape and never an equipment type). The
/// property→move mapping here stays the <b>small, illustrative seam</b> it always was
/// (Mass → damage/speed, Hardness → armour): C2's fabrication rules grow here, and combat never
/// needs to change.</para>
/// </summary>
public static class EquipmentResolver
{
    /// <summary>
    /// The definitions of the moves this weapon grants, with the instance's properties applied.
    ///
    /// <para>Mass lands on the packets the way attribute scaling lands on a hit — once, split by
    /// share — so a two-packet move gets one mass bonus, not two. The result is a per-item
    /// <i>definition</i>; the moveset builder still applies build-wide modifiers on top.</para>
    /// </summary>
    public static IReadOnlyList<MoveDefinition> ResolveWeaponMoves(
        EquipmentDefinition definition,
        ItemInstance? instance,
        Content.DataStore<MoveDefinition> moves)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(moves);

        var properties = instance?.Properties ?? definition.BaseProperties;
        var mass = properties.Get(ItemProperties.Mass);
        var resolved = new List<MoveDefinition>();

        foreach (var grant in definition.Moves)
        {
            if (!moves.TryGetById(grant.Id, out var move))
                continue; // the validator rejects this at load; at runtime, skip loudly-validated content

            // Identity-minted gear (Phase 3, D46) carries an explicit base delivery instead
            // of a mass property — damage and swing weight arrive pre-separated, which is
            // what lets a keen light blade exist at all.
            if (instance?.BaseDelivery is { } delivery)
                resolved.Add(WithDelivery(move, delivery));
            else
                resolved.Add(mass == 0 ? move : WithMass(move, mass));
        }

        return resolved;
    }

    /// <summary>The identity-model twin of <see cref="WithMass"/>: the delivery's damage
    /// bonus lands once, split by packet share; its windup ticks land whole.</summary>
    private static MoveDefinition WithDelivery(MoveDefinition move, Dungeons.Crafting.Identity.ItemBaseDelivery delivery)
    {
        if (delivery.DamageBonus == 0 && delivery.WindupTicks == 0)
            return move;

        var total = move.Packets.Sum(p => p.Amount);

        return new MoveDefinition
        {
            Id = move.Id,
            Name = move.Name,
            Description = move.Description,
            Kind = move.Kind,
            Tags = move.Tags,
            Timing = new Actions.ActionTiming
            {
                TelegraphTicks = move.Timing.TelegraphTicks,
                WindupTicks = move.Timing.WindupTicks + delivery.WindupTicks,
                RecoveryTicks = move.Timing.RecoveryTicks,
            },
            Costs = move.Costs,
            Requires = move.Requires,
            Targeting = move.Targeting,
            MaxTargets = move.MaxTargets,
            CooldownTicks = move.CooldownTicks,
            Interruptible = move.Interruptible,
            Packets = total <= 0 || delivery.DamageBonus == 0
                ? move.Packets
                : move.Packets.Select(p => p.WithAmount(p.Amount + (delivery.DamageBonus * (p.Amount / total)))).ToList(),
            StaggerPower = move.StaggerPower,
            Effects = move.Effects,
        };
    }

    private static MoveDefinition WithMass(MoveDefinition move, double mass)
    {
        var total = move.Packets.Sum(p => p.Amount);
        var bonus = mass * EquipmentTuning.DamagePerMass;

        return new MoveDefinition
        {
            Id = move.Id,
            Name = move.Name,
            Description = move.Description,
            Kind = move.Kind,
            Tags = move.Tags,
            Timing = new Actions.ActionTiming
            {
                TelegraphTicks = move.Timing.TelegraphTicks,
                WindupTicks = move.Timing.WindupTicks + (int)Math.Round(mass * EquipmentTuning.WindupTicksPerMass),
                RecoveryTicks = move.Timing.RecoveryTicks,
            },
            Costs = move.Costs,
            Requires = move.Requires,
            Targeting = move.Targeting,
            MaxTargets = move.MaxTargets,
            CooldownTicks = move.CooldownTicks,
            Interruptible = move.Interruptible,
            Packets = total <= 0
                ? move.Packets
                : move.Packets.Select(p => p.WithAmount(p.Amount + (bonus * (p.Amount / total)))).ToList(),
            StaggerPower = move.StaggerPower,
            Effects = move.Effects,
        };
    }

    /// <summary>
    /// The mitigation of everything worn at once. A loadout is the sum of its pieces: armour
    /// values add, and lane resistances add per lane — <b>raw and uncapped</b>, because the
    /// cap belongs to the pipeline (D-05a) and capping here would lose the overcap that absorbs
    /// exposure.
    ///
    /// <para>How much each piece contributes is a <em>content</em> decision, not a code one: a
    /// helm reads hardness at a lower stat_map weight than a vest does, so coverage is authored
    /// in the form rather than hard-coded as a per-slot multiplier here.</para>
    /// </summary>
    public static ArmorProfile ResolveWornArmor(IEnumerable<(EquipmentDefinition Definition, ItemInstance? Instance)> worn)
    {
        ArgumentNullException.ThrowIfNull(worn);

        var armor = 0.0;
        var resistances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (definition, instance) in worn)
        {
            var piece = ResolveArmor(definition, instance);
            armor += piece.Armor;
            foreach (var (lane, value) in piece.Resistances)
                resistances[lane] = resistances.GetValueOrDefault(lane) + value;
        }

        return armor == 0 && resistances.Count == 0
            ? ArmorProfile.None
            : new ArmorProfile { Armor = armor, Resistances = resistances };
    }

    /// <summary>One worn piece. <see cref="ResolveWornArmor"/> is what combat should ask.</summary>
    public static ArmorProfile ResolveArmor(EquipmentDefinition definition, ItemInstance? instance)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // Identity-minted gear (Phase 3, D46): armor arrives on the instance's base
        // delivery, not through a hardness read — the definition carries no armor block.
        if (instance?.BaseDelivery is { } delivery)
        {
            return delivery.Armor == 0 && definition.Armor is null
                ? ArmorProfile.None
                : new ArmorProfile
                {
                    Armor = delivery.Armor + (definition.Armor?.Armor ?? 0),
                    Resistances = new Dictionary<string, double>(
                        definition.Armor?.Resistances ?? new Dictionary<string, double>(),
                        StringComparer.OrdinalIgnoreCase),
                };
        }

        if (definition.Armor is null)
            return ArmorProfile.None;

        var properties = instance?.Properties ?? definition.BaseProperties;
        var hardness = properties.Get(ItemProperties.Hardness);

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
