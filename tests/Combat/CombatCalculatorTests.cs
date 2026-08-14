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

        var result = calc.Resolve(attacker, target, Strike, currentTick: 0);

        // 10 + STR10*0.5 = 15; armor CON10*0.3 = 3; → 12
        Assert.Equal(12, result.Amount);
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

        var result = calc.Resolve(attacker, target, Strike, currentTick: 5);

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

        var result = calc.Resolve(attacker, target, Strike, currentTick: 5);

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

        var result = calc.Resolve(attacker, target, Strike, currentTick: 0);

        // (15 * 1.5) - 3 = 19.5 → 20
        Assert.True(result.Crit);
        Assert.Equal(20, result.Amount);
    }

    [Fact]
    public void MagicDamage_IgnoresPhysicalArmor()
    {
        var calc = new CombatCalculator(new FakeRandom(0.99));
        var attacker = Enemy("A", 50, Attrs(intel: 10), "ability.bolt");
        var target = Player(attrs: Attrs(con: 10));

        var result = calc.Resolve(attacker, target, Bolt, currentTick: 0);

        // 10 + INT10*0.5 = 15; no armor → 15
        Assert.Equal(15, result.Amount);
    }
}
