using Dungeons.Combat;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Combat;

/// <summary>
/// E1 — the damage resolution pipeline (docs/damage-and-defense.md §3).
///
/// <para><b>The order is the specification.</b> These tests assert the <i>whole trace</i>, not
/// just the final number, because a test pinning only the number cannot tell the difference
/// between "armour then resistance" and "resistance then armour" — the two produce identical
/// results for a single hit and wildly different ones once penetration and conversion exist.</para>
/// </summary>
public class HitPipelineTests
{
    private static HitPipeline Pipeline(double roll = 0.99) => new(new FakeRandom(roll));

    private static Hit Attack(Combatant source, Combatant target, params Packet[] packets) => new()
    {
        Source = source,
        Target = target,
        Name = "Test Attack",
        Packets = packets,
    };

    // --- Packets ------------------------------------------------------------

    [Fact]
    public void APacketsLaneIsItsAspectOrElseItsType()
    {
        Assert.Equal(DamageLanes.Physical, new Packet(DamageType.Slashing, 10).Lane);
        Assert.Equal(DamageLanes.Physical, new Packet(DamageType.Crushing, 10).Lane);
        Assert.Equal(DamageLanes.Magic, new Packet(DamageType.Magic, 10).Lane);
        Assert.Equal(DamageLanes.Heat, new Packet(DamageType.Slashing, DamageAspects.Heat, 10).Lane);
        Assert.Equal(DamageLanes.Charge, new Packet(DamageType.Magic, DamageAspects.Charge, 10).Lane);
    }

    [Fact]
    public void TheArcaneAspectHasNoLaneAtAll()
    {
        // D-03a: unresistable by construction, and structurally unamplifiable in exchange.
        Assert.Null(new Packet(DamageType.Magic, DamageAspects.Arcane, 10).Lane);
        Assert.Null(new Packet(DamageType.Piercing, DamageAspects.Arcane, 10).Lane);
    }

    [Fact]
    public void ArmourFollowsTheDeliveryType_NotTheAspect()
    {
        // The exploit this closes: you cannot slip a sword past armour by making it hot.
        Assert.True(new Packet(DamageType.Slashing, DamageAspects.Heat, 10).ArmourApplies);
        Assert.False(new Packet(DamageType.Magic, DamageAspects.Heat, 10).ArmourApplies);
    }

    // --- Golden traces ------------------------------------------------------

    [Fact]
    public void TheStageOrderIsFixed()
    {
        var attacker = Enemy("A", 50, Attrs(str: 10, luck: 100), "ability.strike");
        var target = Player(attrs: Attrs(con: 10), armor: new ArmorProfile
        {
            Armor = 5,
            Resistances = new Dictionary<string, double> { [DamageLanes.Physical] = 0.25 },
        });
        target.BlockUntilTick = 100;
        target.BlockStartTick = 0;

        var result = Pipeline().Resolve(Attack(attacker, target, new Packet(DamageType.Slashing, 20)), currentTick: 50);

        Assert.Equal(
            new[]
            {
                HitStages.Packets,
                HitStages.Scaling,       // flat added — attribute scaling is one
                HitStages.Crit,          // multiplies base+flat and stops
                HitStages.Armour,        // flat-ish, before the percentage layer
                HitStages.Resistance,    // percentage, so the two are genuinely multiplicative
                HitStages.Block,         // last among the multipliers, so timing always pays
                HitStages.Applied,
            },
            result.Log.Stages);
    }

    [Fact]
    public void CritMultipliesBaseAndFlat_ButNotMitigation()
    {
        var attacker = Enemy("A", 50, Attrs(str: 10, luck: 100), "ability.strike");
        var target = Player(attrs: Attrs(con: 0));

        var normal = Pipeline(roll: 0.99).Resolve(Attack(attacker, target, new Packet(DamageType.Slashing, 10)), 0);
        var crit = Pipeline(roll: 0.10).Resolve(Attack(attacker, target, new Packet(DamageType.Slashing, 10)), 0);

        Assert.False(normal.Crit);
        Assert.True(crit.Crit);

        // Both include the +5 STR flat bonus; crit scales the sum, so exactly ×1.5.
        Assert.Equal(15, normal.Amount);
        Assert.Equal(23, crit.Amount); // 15 × 1.5 = 22.5 → 23
    }

    [Fact]
    public void EachPacketIsReducedByExactlyOneResistanceLane()
    {
        // The D-01 rule: hybrid damage is never taxed twice.
        var attacker = Enemy("A", 50, Attrs(str: 0), "ability.strike");
        var target = Player(attrs: Attrs(con: 0), armor: new ArmorProfile
        {
            Armor = 0,
            Resistances = new Dictionary<string, double>
            {
                [DamageLanes.Physical] = 0.50,
                [DamageLanes.Heat] = 0.25,
            },
        });

        var hit = Attack(attacker, target,
            new Packet(DamageType.Slashing, 80),                          // physical lane
            new Packet(DamageType.Slashing, DamageAspects.Heat, 20));     // heat lane

        var result = Pipeline().Resolve(hit, 0);

        // 80 × 0.50 = 40, and 20 × 0.75 = 15. The heat packet is NOT also cut by physical.
        Assert.Equal(55, result.Amount);
    }

    [Fact]
    public void AttributeScalingIsGrantedOncePerHit_NotOncePerPacket()
    {
        // Found by rendering a worked example: applying the STR bonus per packet meant adding a
        // 1-damage heat rider handed you a whole second bonus, and splitting a hit was free
        // damage. The bonus is split by share instead.
        var attacker = Enemy("A", 50, Attrs(str: 10), "ability.strike");
        var target = Player(attrs: Attrs(con: 0));

        var single = Pipeline().Resolve(Attack(attacker, target, new Packet(DamageType.Slashing, 100)), 0);
        var split = Pipeline().Resolve(
            Attack(attacker, target,
                new Packet(DamageType.Slashing, 80),
                new Packet(DamageType.Slashing, DamageAspects.Heat, 20)), 0);

        Assert.Equal(105, single.Amount);        // 100 + STR bonus 5
        Assert.Equal(single.Amount, split.Amount); // splitting changes nothing
    }

    [Fact]
    public void FlatAddedAspectDamageIncreasesTheTotal_RatherThanRelabellingIt()
    {
        // The clarification behind D-01: "Adds 20 Heat damage" takes an 80-damage sword to 100.
        var attacker = Enemy("A", 50, Attrs(str: 0), "ability.strike");
        var target = Player(attrs: Attrs(con: 0));

        var plain = Pipeline().Resolve(Attack(attacker, target, new Packet(DamageType.Slashing, 80)), 0);
        var withHeat = Pipeline().Resolve(
            Attack(attacker, target,
                new Packet(DamageType.Slashing, 80),
                new Packet(DamageType.Slashing, DamageAspects.Heat, 20)), 0);

        Assert.Equal(80, plain.Amount);
        Assert.Equal(100, withHeat.Amount);
    }

    [Fact]
    public void ArcaneDamageIgnoresEvenAMaximalResistance()
    {
        var attacker = Enemy("A", 50, Attrs(str: 0, intel: 0), "ability.strike");
        var target = Player(attrs: Attrs(con: 0), armor: new ArmorProfile
        {
            Armor = 0,
            Resistances = DamageLanes.All.ToDictionary(l => l, _ => 0.75),
        });

        var result = Pipeline().Resolve(
            Attack(attacker, target, new Packet(DamageType.Magic, DamageAspects.Arcane, 40)), 0);

        Assert.Equal(40, result.Amount);
        Assert.Contains(result.Log.Lines, l => l.Detail.Contains("unresistable"));
    }

    // --- Mitigation ---------------------------------------------------------

    [Fact]
    public void ResistanceIsCappedAndFloored()
    {
        var attacker = Enemy("A", 50, Attrs(str: 0), "ability.strike");

        var overcapped = Player(attrs: Attrs(con: 0), armor: new ArmorProfile
        {
            Armor = 0,
            Resistances = new Dictionary<string, double> { [DamageLanes.Physical] = 5.0 },
        });
        var negative = Player(attrs: Attrs(con: 0), armor: new ArmorProfile
        {
            Armor = 0,
            Resistances = new Dictionary<string, double> { [DamageLanes.Physical] = -5.0 },
        });

        // Capped at 75%: 100 → 25, never to zero however much is stacked.
        Assert.Equal(25, Pipeline().Resolve(Attack(attacker, overcapped, new Packet(DamageType.Slashing, 100)), 0).Amount);

        // Floored at −100%: at most double damage, never more.
        Assert.Equal(200, Pipeline().Resolve(Attack(attacker, negative, new Packet(DamageType.Slashing, 100)), 0).Amount);
    }

    [Fact]
    public void ADodgeEndsResolutionBeforeAnyPacketIsComputed()
    {
        var attacker = Enemy("A", 50, Attrs(str: 10), "ability.strike");
        var target = Player(attrs: Attrs(con: 10));
        target.DodgeUntilTick = 100;

        var result = Pipeline().Resolve(Attack(attacker, target, new Packet(DamageType.Slashing, 50)), 50);

        Assert.True(result.Avoided);
        Assert.Equal(AvoidedVia.Dodge, result.AvoidedBy);
        Assert.Equal(0, result.Amount);
        Assert.Empty(result.Packets);

        // Avoidance runs before mitigation, so nothing was scaled or resisted on the way out.
        Assert.Equal(new[] { HitStages.Packets, HitStages.Dodge }, result.Log.Stages);
    }

    [Fact]
    public void PerfectBlockAvoids_WhileAnOrdinaryBlockMitigates()
    {
        var attacker = Enemy("A", 50, Attrs(str: 0), "ability.strike");

        var precise = Player(attrs: Attrs(con: 0));
        precise.BlockStartTick = 10;
        precise.BlockUntilTick = 26;

        var early = Player(attrs: Attrs(con: 0));
        early.BlockStartTick = 0;
        early.BlockUntilTick = 26;

        var perfect = Pipeline().Resolve(Attack(attacker, precise, new Packet(DamageType.Slashing, 100)), currentTick: 12);
        var ordinary = Pipeline().Resolve(Attack(attacker, early, new Packet(DamageType.Slashing, 100)), currentTick: 12);

        // Avoidance: nothing lands, and it is reported as a block so on-block hooks still fire.
        Assert.True(perfect.Avoided);
        Assert.True(perfect.PerfectBlock);
        Assert.True(perfect.Blocked);
        Assert.Equal(0, perfect.Amount);

        // Mitigation: the hit lands, reduced.
        Assert.False(ordinary.Avoided);
        Assert.True(ordinary.Blocked);
        Assert.Equal(40, ordinary.Amount);
    }

    [Fact]
    public void MitigatedTracksWhatWasPrevented()
    {
        // The basis for "return X% of damage mitigated" in the retaliation family.
        var attacker = Enemy("A", 50, Attrs(str: 0), "ability.strike");
        var target = Player(attrs: Attrs(con: 0), armor: new ArmorProfile
        {
            Armor = 0,
            Resistances = new Dictionary<string, double> { [DamageLanes.Physical] = 0.50 },
        });

        var result = Pipeline().Resolve(Attack(attacker, target, new Packet(DamageType.Slashing, 100)), 0);

        Assert.Equal(50, result.Amount);
        Assert.Equal(50, result.Mitigated, 3);
    }

    [Fact]
    public void TheMinimumDamageFloorAppliesToTheHit_NotToEachPacket()
    {
        // Otherwise a three-packet hit would floor three times and out-damage a one-packet hit
        // of the same size against a heavily armoured target.
        var attacker = Enemy("A", 50, Attrs(str: 0), "ability.strike");
        var target = Player(attrs: Attrs(con: 0), armor: new ArmorProfile
        {
            Armor = 0,
            Resistances = DamageLanes.All.ToDictionary(l => l, _ => 0.75),
        });

        var split = Pipeline().Resolve(
            Attack(attacker, target,
                new Packet(DamageType.Slashing, 1),
                new Packet(DamageType.Slashing, DamageAspects.Heat, 1),
                new Packet(DamageType.Slashing, DamageAspects.Cold, 1)), 0);

        Assert.Equal(1, split.Amount); // 0.25 × 3 = 0.75 → floored once, to 1
    }

    // --- Legibility ---------------------------------------------------------

    [Fact]
    public void TheHitLogExplainsEveryNumberItChanged()
    {
        var attacker = Enemy("Goblin Brute", 60, Attrs(str: 12), "ability.goblin_smash");
        var target = Player(attrs: Attrs(con: 10), armor: new ArmorProfile
        {
            Armor = 5,
            Resistances = new Dictionary<string, double> { [DamageLanes.Physical] = 0.25 },
        });

        var result = Pipeline().Resolve(Attack(attacker, target, new Packet(DamageType.Crushing, 20)), 0);
        var rendered = result.Log.Render("Overhead Smash — Goblin Brute → You");

        // Every mitigation stage names its source, so "why did that hit for 17?" is answerable
        // from the trace alone. This is the required-scope claim, tested rather than asserted.
        Assert.Contains("armour", rendered);
        Assert.Contains("physical 25%", rendered);
        Assert.Contains("→", rendered); // before → after on every stage that moved a number

        foreach (var line in result.Log.Lines)
            Assert.False(string.IsNullOrWhiteSpace(line.Detail), $"stage {line.Stage} explains nothing");
    }
}
