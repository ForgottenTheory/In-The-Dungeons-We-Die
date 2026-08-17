using Dungeons.Modifiers;
using Dungeons.Randomness;

namespace Dungeons.Combat;

/// <summary>How a hit failed to land. Null on <see cref="HitResult"/> means it landed.</summary>
public static class AvoidedVia
{
    public const string Dodge = "dodge";
    public const string PerfectBlock = "perfect_block";
    public const string Parry = "parry";
    public const string Evade = "evade";
    public const string Negate = "negate";
}

/// <summary>The authoritative outcome of one hit, plus the trace explaining it.</summary>
public sealed class HitResult
{
    public required DamageType Type { get; init; }
    public required int Amount { get; init; }
    public required IReadOnlyList<Packet> Packets { get; init; }
    public required HitLog Log { get; init; }

    /// <summary>Set when the hit was avoided entirely — one of <see cref="AvoidedVia"/>.</summary>
    public string? AvoidedBy { get; init; }

    public bool Avoided => AvoidedBy is not null;
    public bool Crit { get; init; }
    public bool Blocked { get; init; }
    public bool PerfectBlock { get; init; }

    /// <summary>Damage prevented by mitigation — the basis for "return % of mitigated damage".</summary>
    public double Mitigated { get; init; }

    /// <summary>Kept for the encounter's existing narration; Dodge is one avoidance among several.</summary>
    public bool Dodged => AvoidedBy == AvoidedVia.Dodge;
}

/// <summary>
/// The damage resolution pipeline (docs/damage-and-defense.md §3). Replaces
/// <c>CombatCalculator.Resolve</c>'s fixed body.
///
/// <para><b>The order is the specification.</b> Every stage is discrete, individually testable,
/// and appends to a <see cref="HitLog"/>; golden tests assert the whole trace rather than the
/// final number, so a reordering cannot pass silently.</para>
///
/// <para><b>Every stage's source is a modifier contribution</b>, resolved through
/// <see cref="CombatantModifiers"/> — which is what lets an affix, a status or a build hook all
/// feed the same stage without the pipeline knowing any of them exist. A stage with no
/// contribution falls back to its <see cref="CombatTuning"/> constant.</para>
///
/// <para>Deterministic given the random source. Mutates nothing — the encounter applies the
/// result.</para>
/// </summary>
public sealed class HitPipeline
{
    private readonly IRandomSource _random;
    private readonly CombatantModifiers? _modifiers;

    /// <param name="modifiers">
    /// The modifier read path (E3c-2). Optional so calculation tests need not assemble one; when
    /// absent every stage falls back to its tuning constant.
    /// </param>
    public HitPipeline(IRandomSource random, CombatantModifiers? modifiers = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _modifiers = modifiers;
    }

    /// <summary>
    /// The situation a hit's modifiers resolve in.
    ///
    /// <para>Every tag the hit carries is supplied as a <c>move_tag</c>, because one swing is
    /// <c>melee</c> and <c>attack</c> and <c>light</c> at the same time and a modifier scoped to
    /// any of them applies. This is the first real <see cref="ModifierContext"/> in the game —
    /// built from the hit, never defaulted to make a call compile.</para>
    /// </summary>
    private static ModifierContext ContextFor(Hit hit) =>
        hit.Tags.Count == 0
            ? ModifierContext.None
            : ModifierContext.For(ScopeDimensions.MoveTag, hit.Tags.ToArray());

    private static ModifierContext ContextFor(Hit hit, Packet packet) =>
        packet.Lane is { } lane ? ContextFor(hit).With(ScopeDimensions.Lane, lane) : ContextFor(hit);

    private double Resolve(Combatant? combatant, string key, ModifierContext context, double baseValue) =>
        combatant is null || _modifiers is null ? baseValue : _modifiers.Resolve(combatant, key, context, baseValue);

    public HitResult Resolve(Hit hit, long currentTick)
    {
        ArgumentNullException.ThrowIfNull(hit);

        var log = new HitLog();
        var attacker = hit.Source;
        var target = hit.Target;

        log.Add(HitStages.Packets, string.Join(" · ", hit.Packets));

        // ── AVOIDANCE (binary; any success ends resolution) ──────────────────
        //
        // Before anything else, so an avoided hit produces no packets — and therefore no
        // ailment, no thorns, no on-hit. Resolving damage we then discard would make the log lie.

        if (target.IsDodging(currentTick))
            return Avoided(hit, log, AvoidedVia.Dodge, $"stance active through tick {target.DodgeUntilTick}");

        if (target.IsPerfectBlocking(currentTick))
            return Avoided(hit, log, AvoidedVia.PerfectBlock,
                $"within {CombatTuning.PerfectBlockWindowTicks} ticks of raising guard");

        // PARRY (R4c-2, D-26): gear-granted, the tightest window — negates and (encounter-side)
        // staggers the attacker open. The encounter gates the command; the pipeline only times it.
        if (target.IsParrying(currentTick))
            return Avoided(hit, log, AvoidedVia.Parry,
                $"within the {CombatTuning.ParryWindowTicks}-tick parry window");

        // EVADE (R4c, D-07): the one passive avoidance roll, and only against hits that gave
        // no telegraph — the skill test stays mandatory for everything the player can read.
        // Diminishing and danger-capped at 15% as data.
        if (hit.Untelegraphed)
        {
            var evade = Resolve(target, ModifierKeys.EvadeChance, ContextFor(hit), 0);
            if (evade > 0 && _random.NextDouble() < evade)
                return Avoided(hit, log, AvoidedVia.Evade, $"{evade:P0} vs an untelegraphed hit");
        }

        // ── THE HIT LANDS ────────────────────────────────────────────────────

        var packets = hit.Packets.ToList();

        // FLAT ADDED first. Attribute scaling is a flat addition, and in E3 it becomes literally
        // that — an ordinary `combat.damage.flat` contribution rather than a special case.
        packets = ApplyAttributeScaling(hit, packets, log);

        // CRIT multiplies base+flat, and stops there. Putting it after `increased`/`more` would
        // let it multiply every other multiplier as well, and crit builds would scale
        // quadratically with everything else — the difference between crit being *a* build and
        // *the* build.
        var context = ContextFor(hit);

        var crit = RollCrit(attacker, context, log);
        if (crit)
        {
            var critMultiplier = Resolve(attacker, ModifierKeys.CritMult, context, CombatTuning.CritMultiplier);
            packets = Scale(packets, critMultiplier, log, HitStages.Crit, $"×{critMultiplier:0.##}");
        }

        // INCREASED — the first stage with a real source. `more`/`less`, conversion and
        // added-as-extra still sit here with none.
        var increasedMultiplier = Resolve(attacker, ModifierKeys.DamageMult, context, 1.0);
        if (Math.Abs(increasedMultiplier - 1.0) > CombatTuning.MultiplierEpsilon)
            packets = Scale(packets, increasedMultiplier, log, HitStages.Increased, $"×{increasedMultiplier:0.###}");

        // ── PER-PACKET MITIGATION ────────────────────────────────────────────

        var beforeMitigation = packets.Sum(p => p.Amount);
        packets = packets.Select(p => Mitigate(hit, p, target, log)).ToList();

        // ── WHOLE-HIT MITIGATION ─────────────────────────────────────────────

        var blocked = target.IsBlocking(currentTick);
        if (blocked)
        {
            // Block *strength* scales how much of the hit the guard eats, not the damage that
            // gets through — so "+30% block strength" reads as mitigating 30% more rather than
            // taking 30% more damage, which is what the sign would say if it multiplied directly.
            var blockStrength = Resolve(target, ModifierKeys.BlockMult, context, 1.0);
            var throughput = Math.Max(0, 1.0 - ((1.0 - CombatTuning.BlockDamageMultiplier) * blockStrength));
            var strengthNote = Math.Abs(blockStrength - 1.0) > CombatTuning.MultiplierEpsilon
                ? $", strength ×{blockStrength:0.##}"
                : string.Empty;
            packets = Scale(packets, throughput, log, HitStages.Block,
                $"×{throughput:0.##} (guard held{strengthNote})");
        }

        var damageTakenMultiplier = Resolve(target, ModifierKeys.DamageTakenMult, context, 1.0);
        if (Math.Abs(damageTakenMultiplier - 1.0) > CombatTuning.MultiplierEpsilon)
            packets = Scale(packets, damageTakenMultiplier, log, HitStages.DamageTaken,
                $"×{damageTakenMultiplier:0.###}");

        // The floor applies to the hit TOTAL, not per packet — otherwise a three-packet hit
        // would floor three times and out-damage a single packet of the same size against a
        // heavily armoured target.
        var total = packets.Sum(p => p.Amount);
        var rounded = (int)Math.Round(total, MidpointRounding.AwayFromZero);
        var amount = Math.Max(CombatTuning.MinimumDamage, rounded);

        // Logged only when the minimum actually bound. Recording ordinary rounding here would
        // put a line in every single trace that tells the reader nothing.
        if (amount != rounded)
            log.Add(HitStages.Floor, $"minimum {CombatTuning.MinimumDamage}", rounded, amount);

        log.Add(HitStages.Applied, $"{amount} {hit.PrimaryType}");

        return new HitResult
        {
            Type = hit.PrimaryType,
            Amount = amount,
            Packets = packets,
            Log = log,
            Crit = crit,
            Blocked = blocked,
            PerfectBlock = false,
            Mitigated = Math.Max(0, beforeMitigation - total),
        };
    }

    // --- Stages -------------------------------------------------------------

    /// <param name="avoidedVia">One of <see cref="AvoidedVia"/> — which defence stopped the hit.</param>
    /// <param name="reason">The trace line's explanation, e.g. "within the 3-tick parry window".</param>
    private static HitResult Avoided(Hit hit, HitLog log, string avoidedVia, string reason)
    {
        var stage = avoidedVia switch
        {
            AvoidedVia.Dodge => HitStages.Dodge,
            AvoidedVia.Evade => HitStages.Evade,
            AvoidedVia.Parry => HitStages.Parry,
            _ => HitStages.PerfectBlock,
        };
        log.Add(stage, $"AVOIDED — {reason}");

        return new HitResult
        {
            Type = hit.PrimaryType,
            Amount = 0,
            Packets = Array.Empty<Packet>(),
            Log = log,
            AvoidedBy = avoidedVia,
            Blocked = avoidedVia == AvoidedVia.PerfectBlock,
            PerfectBlock = avoidedVia == AvoidedVia.PerfectBlock,
            Mitigated = hit.RawTotal,
        };
    }

    private bool RollCrit(Combatant attacker, ModifierContext context, HitLog log)
    {
        // The tuning cap bounds what *Luck alone* can buy; modifiers add on top and answer to the
        // key's own clamp instead. Capping the total here would make every crit-chance affix past
        // 50% worth exactly zero without saying so.
        var critChanceFromLuck = Math.Min(
            CombatTuning.MaxCritChance, attacker.Attributes.Luck * CombatTuning.CritChancePerLuck);
        var chance = Resolve(attacker, ModifierKeys.CritChance, context, critChanceFromLuck);
        var crit = _random.NextDouble() < chance;
        if (!crit && chance > 0)
            log.Add(HitStages.Crit, $"no ({chance:P0} chance)");
        return crit;
    }

    /// <summary>
    /// STR scales physical-typed packets, INT scales Magic-typed ones. Kept from the old
    /// calculator so E1 changes the *structure* without silently changing the character model —
    /// attribute scaling becomes ordinary flat-added modifiers in E3.
    /// </summary>
    private static List<Packet> ApplyAttributeScaling(Hit hit, List<Packet> packets, HitLog log)
    {
        var attributes = hit.Source.Attributes;
        var before = packets.Sum(p => p.Amount);

        var physicalBonus = attributes.Strength * CombatTuning.PhysicalScalingPerStrength;
        var magicBonus = attributes.Intelligence * CombatTuning.MagicScalingPerIntelligence;

        var physicalPool = packets.Where(p => DamageTypes.IsPhysical(p.Type)).Sum(p => p.Amount);
        var magicPool = packets.Where(p => !DamageTypes.IsPhysical(p.Type)).Sum(p => p.Amount);

        // The bonus is granted ONCE per hit and split across packets by share — never once per
        // packet. Otherwise adding a 1-damage heat packet would hand you a whole second STR
        // bonus, and splitting a hit would be free damage. Flat-added modifiers in E3 inherit
        // this rule for the same reason.
        var scaled = packets.Select(packet =>
        {
            var physical = DamageTypes.IsPhysical(packet.Type);
            var pool = physical ? physicalPool : magicPool;
            var bonus = physical ? physicalBonus : magicBonus;
            var share = pool <= 0 ? 0 : packet.Amount / pool;
            return packet.WithAmount(packet.Amount + (bonus * share));
        }).ToList();

        var after = scaled.Sum(p => p.Amount);
        log.AddIfChanged(HitStages.Scaling, "attributes", before, after);
        return scaled;
    }

    private Packet Mitigate(Hit hit, Packet packet, Combatant target, HitLog log)
    {
        var amount = packet.Amount;

        // LANE AVOIDANCE (R4c): rare, diminishing, danger-capped at 25% — negates this packet
        // outright. Arcane has no lane and therefore cannot be avoided this way (D-03a).
        if (packet.Lane is { } avoidableLane)
        {
            var avoid = Resolve(target, ModifierKeys.AvoidLane, ContextFor(hit, packet), 0);
            if (avoid > 0 && _random.NextDouble() < avoid)
            {
                log.Add(HitStages.Negate, $"{avoidableLane} — negated ({avoid:P0} lane avoidance)", amount, 0);
                return packet.WithAmount(0);
            }
        }

        // ARMOUR — physical-typed packets only, whatever their aspect.
        if (packet.ArmourApplies)
        {
            // Resolved rather than read straight off the profile, which is what finally lets
            // Corroded strip armour and a granted `combat.armor` add to it.
            var armour = Resolve(target, ModifierKeys.Armor, ContextFor(hit, packet), target.Armour);
            if (armour > 0 && amount > 0)
            {
                var reduction = armour / (armour + (CombatTuning.ArmourK * amount));
                var after = amount * (1.0 - reduction);
                log.Add(HitStages.Armour,
                    $"{Describe(packet)} — armour {armour:0.##} vs {amount:0.##} → −{reduction:P0}", amount, after);
                amount = after;
            }
        }

        // RESISTANCE — exactly one lane per packet. Arcane has none by design (D-03a).
        var lane = packet.Lane;
        if (lane is null)
        {
            log.Add(HitStages.Resistance, "arcane — unresistable");
        }
        else
        {
            // R4a: the lane total is base (armour profile / physiology) plus every
            // `combat.resist.<lane>` contribution — which is what lets affixes, statuses and
            // build sources grant real lane resistance. Cap and floor stay the combatant's.
            var resistanceTotal = Resolve(
                target, ModifierKeys.ResistLane(lane), ContextFor(hit, packet),
                target.ArmorProfile.ResistanceFor(lane));
            var cappedResistance = Combatant.CapResistance(resistanceTotal);

            // PENETRATION (R4c, §4.2 step 6): flat, attacker-side, applied AFTER the cap — so
            // it eats through overcap, which is exactly what keeps it distinct from exposure
            // (a negative contribution summed before the cap). Floored at −100%.
            var penetration = Resolve(hit.Source, ModifierKeys.PenLane, ContextFor(hit, packet), 0);
            var effectiveResistance = penetration > 0
                ? Math.Max(cappedResistance - penetration, CombatTuning.ResistanceFloor)
                : cappedResistance;

            if (Math.Abs(effectiveResistance) > CombatTuning.MultiplierEpsilon || penetration > 0)
            {
                var after = amount * (1.0 - effectiveResistance);
                var detail = penetration > 0
                    ? $"{lane} {cappedResistance:P0} − pen {penetration:P0} → {effectiveResistance:P0}"
                    : $"{lane} {effectiveResistance:P0}";
                log.Add(HitStages.Resistance, detail, amount, after);
                amount = after;
            }
        }

        // VULNERABILITY — per damage *type*, on the enemy (D-02). Two-way: >1 is a weakness,
        // <1 is toughness, so a skeleton can be soft to crushing and hard against piercing.
        var vulnerability = target.VulnerabilityTo(packet.Type);
        if (Math.Abs(vulnerability - 1.0) > CombatTuning.MultiplierEpsilon)
        {
            var after = amount * vulnerability;
            log.Add(HitStages.Vulnerability, $"{Describe(packet)} ×{vulnerability:0.##}", amount, after);
            amount = after;
        }

        return packet.WithAmount(Math.Max(0, amount));
    }

    /// <summary>Names a packet in the trace so a hybrid hit's stages stay attributable.</summary>
    private static string Describe(Packet packet) =>
        packet.Aspect is null ? packet.Type.ToString() : $"{packet.Type}/{packet.Aspect}";

    private static List<Packet> Scale(List<Packet> packets, double factor, HitLog log, string stage, string detail)
    {
        var before = packets.Sum(p => p.Amount);
        var scaled = packets.Select(p => p.WithAmount(p.Amount * factor)).ToList();
        log.AddIfChanged(stage, detail, before, scaled.Sum(p => p.Amount));
        return scaled;
    }
}
