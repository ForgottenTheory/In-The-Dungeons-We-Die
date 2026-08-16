using Dungeons.Randomness;

namespace Dungeons.Combat;

/// <summary>How a hit failed to land. Null on <see cref="HitResult"/> means it landed.</summary>
public static class AvoidedVia
{
    public const string Dodge = "dodge";
    public const string PerfectBlock = "perfect_block";
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
/// <para><b>E1 scope.</b> The offensive scaling stages (flat added / increased / more /
/// conversion / added-as-extra) and the avoidance stages (evade / negate) exist in order but
/// have no source feeding them — equipment produces <see cref="AttackProfile"/>, not modifier
/// contributions, until E3 wires <c>ModifierSet</c> in. Pinning the ordering while it is cheap
/// is the point. Barrier absorption and ailment application arrive with statuses in E2.</para>
///
/// <para>Deterministic given the RNG. Mutates nothing — the encounter applies the result.</para>
/// </summary>
public sealed class HitPipeline
{
    private readonly IRandomSource _rng;

    public HitPipeline(IRandomSource rng) => _rng = rng ?? throw new ArgumentNullException(nameof(rng));

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

        // Evade (untelegraphed hits only) and per-lane Negate have no source until E5's
        // avoidance affixes. Recorded as stages so the ordering is fixed now.

        // ── THE HIT LANDS ────────────────────────────────────────────────────

        var packets = hit.Packets.ToList();

        // FLAT ADDED first. Attribute scaling is a flat addition, and in E3 it becomes literally
        // that — an ordinary `combat.damage.flat` contribution rather than a special case.
        packets = ApplyAttributeScaling(hit, packets, log);

        // CRIT multiplies base+flat, and stops there. Putting it after `increased`/`more` would
        // let it multiply every other multiplier as well, and crit builds would scale
        // quadratically with everything else — the difference between crit being *a* build and
        // *the* build.
        var crit = RollCrit(attacker, log);
        if (crit)
            packets = Scale(packets, CombatTuning.CritMultiplier, log, HitStages.Crit,
                $"×{CombatTuning.CritMultiplier}");

        // increased → more/less → conversion → added-as-extra all sit here.
        // No contributors until E3.

        // ── PER-PACKET MITIGATION ────────────────────────────────────────────

        var beforeMitigation = packets.Sum(p => p.Amount);
        packets = packets.Select(p => Mitigate(p, target, log)).ToList();

        // ── WHOLE-HIT MITIGATION ─────────────────────────────────────────────

        var blocked = target.IsBlocking(currentTick);
        if (blocked)
            packets = Scale(packets, CombatTuning.BlockDamageMultiplier, log, HitStages.Block,
                $"×{CombatTuning.BlockDamageMultiplier} (guard held)");

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

    private static HitResult Avoided(Hit hit, HitLog log, string via, string why)
    {
        log.Add(via == AvoidedVia.Dodge ? HitStages.Dodge : HitStages.PerfectBlock, $"AVOIDED — {why}");

        return new HitResult
        {
            Type = hit.PrimaryType,
            Amount = 0,
            Packets = Array.Empty<Packet>(),
            Log = log,
            AvoidedBy = via,
            Blocked = via == AvoidedVia.PerfectBlock,
            PerfectBlock = via == AvoidedVia.PerfectBlock,
            Mitigated = hit.RawTotal,
        };
    }

    private bool RollCrit(Combatant attacker, HitLog log)
    {
        var chance = Math.Min(CombatTuning.MaxCritChance, attacker.Attributes.Luck * CombatTuning.CritChancePerLuck);
        var crit = _rng.NextDouble() < chance;
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

    private static Packet Mitigate(Packet packet, Combatant target, HitLog log)
    {
        var amount = packet.Amount;

        // ARMOUR — physical-typed packets only, whatever their aspect.
        if (packet.ArmourApplies)
        {
            var armour = target.Armour;
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
            var effective = target.EffectiveResistance(lane);
            if (Math.Abs(effective) > 0.0001)
            {
                var after = amount * (1.0 - effective);
                log.Add(HitStages.Resistance, $"{lane} {effective:P0}", amount, after);
                amount = after;
            }
        }

        // VULNERABILITY — per damage *type*, on the enemy (D-02). Two-way: >1 is a weakness,
        // <1 is toughness, so a skeleton can be soft to crushing and hard against piercing.
        var vulnerability = target.VulnerabilityTo(packet.Type);
        if (Math.Abs(vulnerability - 1.0) > 0.0001)
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
