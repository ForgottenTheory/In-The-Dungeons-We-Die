using Dungeons.Combat;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Combat;

public class CombatCalculatorTests
{
    private static readonly AbilityDefinition Strike = Ability("ability.strike", DamageType.Slashing, 10, 2, 8, 15);
    private static readonly AbilityDefinition Bolt = Ability("ability.bolt", DamageType.Magic, 10, 2, 8, 15);

    [Fact]
    public void PhysicalDamage_ScalesWithStrength_MitigatedByConstitution()
    {
        var calc = new CombatCalculator(new FakeRandom(0.99)); // no crit
        var attacker = Enemy("A", 50, Attrs(str: 10), "ability.strike");
        var target = Player(attrs: Attrs(con: 10));

        var result = calc.Resolve(attacker, target, Strike.DamageType, Strike.BaseValue, currentTick: 0);

        // 10 + STR10*0.5 = 15. Armour CON10*0.3 = 3, diminishing (D-25/D-27):
        // 3/(3 + 1*15) = 16.7% → 12.5 → 13.
        // Was 12 under flat subtraction; armour is now weaker against a mid-size hit and has no
        // cliff against small ones.
        Assert.Equal(13, result.Amount);
        Assert.False(result.Crit);
        Assert.False(result.Blocked);
        Assert.False(result.Dodged);
    }

    [Fact]
    public void Dodging_NegatesDamage()
    {
        var calc = new CombatCalculator(new FakeRandom(0.99));
        var attacker = Enemy("A", 50, Attrs(str: 10), "ability.strike");
        var target = Player(attrs: Attrs(con: 10));
        target.DodgeUntilTick = 10;

        var result = calc.Resolve(attacker, target, Strike.DamageType, Strike.BaseValue, currentTick: 5);

        Assert.True(result.Dodged);
        Assert.Equal(0, result.Amount);
    }

    [Fact]
    public void Blocking_ReducesDamage()
    {
        var calc = new CombatCalculator(new FakeRandom(0.99));
        var attacker = Enemy("A", 50, Attrs(str: 10), "ability.strike");
        var target = Player(attrs: Attrs(con: 10));
        target.BlockUntilTick = 10;

        var result = calc.Resolve(attacker, target, Strike.DamageType, Strike.BaseValue, currentTick: 5);

        // (15 - 3) * 0.4 = 4.8 → 5
        Assert.True(result.Blocked);
        Assert.Equal(5, result.Amount);
    }

    [Fact]
    public void Crit_MultipliesBeforeArmor()
    {
        var calc = new CombatCalculator(new FakeRandom(0.1)); // < crit chance
        var attacker = Enemy("A", 50, Attrs(str: 10, luck: 100), "ability.strike");
        var target = Player(attrs: Attrs(con: 10));

        var result = calc.Resolve(attacker, target, Strike.DamageType, Strike.BaseValue, currentTick: 0);

        // Crit multiplies base+flat and stops: (10 + 5) * 1.5 = 22.5.
        // Armour 3 vs 22.5 → 11.8% → 19.85 → 20.
        // Unchanged from the flat-armour era by coincidence, and worth keeping for exactly that
        // reason: it pins that crit still lands *after* attribute scaling.
        Assert.True(result.Crit);
        Assert.Equal(20, result.Amount);
    }

    [Fact]
    public void MagicDamage_IgnoresPhysicalArmor()
    {
        var calc = new CombatCalculator(new FakeRandom(0.99));
        var attacker = Enemy("A", 50, Attrs(intel: 10), "ability.bolt");
        var target = Player(attrs: Attrs(con: 10));

        var result = calc.Resolve(attacker, target, Bolt.DamageType, Bolt.BaseValue, currentTick: 0);

        // 10 + INT10*0.5 = 15; no armor → 15
        Assert.Equal(15, result.Amount);
    }

    [Fact]
    public void EquippedArmor_AddsMitigation_AndLaneResistance()
    {
        var calc = new CombatCalculator(new FakeRandom(0.99));
        var attacker = Enemy("A", 50, Attrs(str: 10), "ability.strike");
        var target = Player(attrs: Attrs(con: 10), armor: new ArmorProfile
        {
            Armor = 5,
            // Keyed by LANE now, not by damage-type name (D-02). Slashing/Crushing/Piercing all
            // answer to `physical`; per-type weakness moved onto the enemy.
            Resistances = new Dictionary<string, double> { [DamageLanes.Physical] = 0.5 },
        });

        var result = calc.Resolve(attacker, target, Strike.DamageType, Strike.BaseValue, currentTick: 0);

        // 15 raw; armour CON3 + gear5 = 8 → 8/(8+15) = 34.8% → 9.78;
        // then 50% physical resistance → 4.89 → 5.
        Assert.Equal(5, result.Amount);
    }

    [Fact]
    public void ResistanceIsKeyedByLane_SoADamageTypeNameResistsNothing()
    {
        // The migration hazard this validator rule exists to catch: authoring "Slashing" used to
        // work and now silently resists nothing. ContentValidator rejects it at load; this pins
        // the runtime behaviour so the two can never disagree.
        var calc = new CombatCalculator(new FakeRandom(0.99));
        var attacker = Enemy("A", 50, Attrs(str: 10), "ability.strike");
        // con 0 so armour contributes nothing and the resistance lane is the only variable.
        var target = Player(attrs: Attrs(con: 0), armor: new ArmorProfile
        {
            Armor = 0,
            Resistances = new Dictionary<string, double> { ["Slashing"] = 0.5 },
        });

        var result = calc.Resolve(attacker, target, Strike.DamageType, Strike.BaseValue, currentTick: 0);

        Assert.Equal(15, result.Amount); // untouched — "Slashing" is not a lane
    }

    [Fact]
    public void ArmourIsStrongAgainstChipDamage_AndWeakAgainstSpikes()
    {
        // The whole point of D-25: armour is the attrition answer, resistance is the spike
        // answer. Under flat subtraction this relationship was inverted at the low end — a
        // 5-damage hit became 1 (an 80% cut) while a 60-damage hit barely noticed.
        var calc = new CombatCalculator(new FakeRandom(0.99));
        var attacker = Enemy("A", 50, Attrs(str: 0), "ability.strike");
        var target = Player(attrs: Attrs(con: 0), armor: new ArmorProfile
        {
            Armor = 20,
            Resistances = new Dictionary<string, double>(),
        });

        var chip = calc.Resolve(attacker, target, DamageType.Slashing, 5, currentTick: 0);
        var spike = calc.Resolve(attacker, target, DamageType.Slashing, 60, currentTick: 0);

        var chipReduction = 1.0 - (chip.Amount / 5.0);
        var spikeReduction = 1.0 - (spike.Amount / 60.0);

        Assert.True(chipReduction > spikeReduction,
            $"armour must fall off against bigger hits (chip {chipReduction:P0}, spike {spikeReduction:P0})");
        Assert.True(chip.Amount > 0, "and it must never fully negate — the flat formula's cliff is gone");
    }

    [Fact]
    public void EnemyVulnerability_IsTwoWayAndPerDamageType()
    {
        // D-02: the counter-play that used to live in three physical resistances on player gear.
        var calc = new CombatCalculator(new FakeRandom(0.99));
        var attacker = Enemy("A", 50, Attrs(str: 0), "ability.strike");

        var brute = Enemy("Brute", 100, Attrs(con: 0), "ability.strike",
            vulnerable: new Dictionary<string, double> { ["Crushing"] = 1.25, ["Slashing"] = 0.85 });

        var crushed = calc.Resolve(attacker, brute, DamageType.Crushing, 20, currentTick: 0);
        var slashed = calc.Resolve(attacker, brute, DamageType.Slashing, 20, currentTick: 0);
        var pierced = calc.Resolve(attacker, brute, DamageType.Piercing, 20, currentTick: 0);

        Assert.Equal(25, crushed.Amount);  // 20 × 1.25
        Assert.Equal(17, slashed.Amount);  // 20 × 0.85 — tough, not just weak
        Assert.Equal(20, pierced.Amount);  // unlisted types are 1.0
    }
}
