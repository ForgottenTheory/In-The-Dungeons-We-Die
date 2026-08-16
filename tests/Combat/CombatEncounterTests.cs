using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Events;
using Dungeons.Simulation;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Combat;

public class CombatEncounterTests
{
    private static readonly AbilityDefinition Strike = Ability("ability.strike", DamageType.Slashing, 8, 2, 8, 15, stamina: 5);
    private static readonly AbilityDefinition Slash = Ability("ability.goblin_slash", DamageType.Slashing, 6, 8, 8, 20);
    private static readonly AbilityDefinition Smash = Ability("ability.goblin_smash", DamageType.Crushing, 18, 20, 20, 35);

    private static (CombatEncounter enc, TickEngine tick) Build()
    {
        var tick = new TickEngine();
        var calc = new CombatCalculator(new FakeRandom(0.99)); // never crit
        var abilities = Abilities(Strike, Slash, Smash);
        var enc = new CombatEncounter(tick, calc, abilities, new FakeRandom(0.99), new GameEventBus(), "ability.strike");
        return (enc, tick);
    }

    [Fact]
    public void EnemyAttack_ResolvesAtImpactTick()
    {
        var (enc, tick) = Build();
        var player = Player(hp: 100, attrs: Attrs(con: 5));
        var enemy = Enemy("Goblin Raider", 50, Attrs(str: 6), "ability.goblin_slash");
        enc.Start(player, new[] { enemy });

        Assert.Single(enc.Intents);
        Assert.Equal(16, enc.Intents[0].ExecuteTick); // telegraph 8 + windup 8

        tick.Advance(15);
        Assert.Equal(100, player.Health.Current); // not yet

        tick.Advance(1); // impact
        Assert.Equal(92, player.Health.Current); // 6 + 3 - 1.5 = 7.5 → 8
    }

    [Fact]
    public void DodgingBeforeImpact_NegatesTheHit()
    {
        var (enc, tick) = Build();
        var player = Player(hp: 100, attrs: Attrs(con: 5));
        enc.Start(player, new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });

        tick.Advance(15);
        enc.Dodge();     // dodge window covers the tick-16 impact
        tick.Advance(1);

        Assert.Equal(100, player.Health.Current);
    }

    [Fact]
    public void BlockingBeforeImpact_ReducesTheHit()
    {
        var (enc, tick) = Build();
        var player = Player(hp: 100, attrs: Attrs(con: 5));
        enc.Start(player, new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });

        // Guard raised at tick 5, impact at 16 — outside the 4-tick Perfect Block window, so
        // this is ordinary mitigation.
        tick.Advance(5);
        enc.Block();
        tick.Advance(11);

        // 6 + STR3 = 9; armour 1.5 → 1.5/(1.5+9) = 14.3% → 7.71; block ×0.4 = 3.09 → 3.
        Assert.Equal(97, player.Health.Current);
    }

    [Fact]
    public void BlockingInsideThePerfectWindow_NegatesTheHitEntirely()
    {
        var (enc, tick) = Build();
        var player = Player(hp: 100, attrs: Attrs(con: 5));
        enc.Start(player, new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });

        // Guard raised at tick 15, impact at 16 — one tick, well inside the window. Blocking at
        // the last possible moment is precise blocking, and D-06 makes that avoidance rather
        // than mitigation: it is what gives the Bastion "precise blocks refund Guard".
        tick.Advance(15);
        enc.Block();
        tick.Advance(1);

        Assert.Equal(100, player.Health.Current);
    }

    [Fact]
    public void PlayerAttack_DefeatsEnemy_EndsInVictory()
    {
        var (enc, tick) = Build();
        CombatOutcome? outcome = null;
        enc.Ended += o => outcome = o;

        var player = Player(attrs: Attrs(str: 20));
        var enemy = Enemy("Weakling", 5, Attrs(con: 4), "ability.goblin_slash");
        enc.Start(player, new[] { enemy });

        Assert.True(enc.Attack());
        tick.Advance(10); // player strike resolves (impact 10)

        Assert.False(enemy.IsAlive);
        Assert.False(enc.IsActive);
        Assert.NotNull(outcome);
        Assert.Equal(CombatResult.Victory, outcome!.Result);
        Assert.Contains(enemy, outcome.DefeatedEnemies);
    }

    [Fact]
    public void PlayerDeath_EndsInDefeat()
    {
        var (enc, tick) = Build();
        CombatOutcome? outcome = null;
        enc.Ended += o => outcome = o;

        var player = Player(hp: 5, attrs: Attrs(con: 5));
        enc.Start(player, new[] { Enemy("Brute", 200, Attrs(str: 10), "ability.goblin_slash") });

        tick.Advance(16); // enemy hits for ~10 > 5 hp

        Assert.False(player.Health.Current > 0);
        Assert.False(enc.IsActive);
        Assert.Equal(CombatResult.Defeat, outcome!.Result);
    }

    [Fact]
    public void Attack_ConsumesStamina_AndGatesOnRecovery()
    {
        var (enc, tick) = Build();
        var player = Player(hp: 100, stamina: 100, attrs: Attrs(str: 5));
        enc.Start(player, new[] { Enemy("Brute", 200, Attrs(str: 5), "ability.goblin_smash") });

        Assert.True(enc.PlayerReady);
        Assert.True(enc.Attack());
        Assert.Equal(95, player.Stamina.Current);
        Assert.False(enc.Attack());      // still winding up / recovering
        Assert.False(enc.PlayerReady);

        tick.Advance(24);
        Assert.False(enc.PlayerReady);   // ready at 10 + 15 = 25
        tick.Advance(1);
        Assert.True(enc.PlayerReady);
    }

    [Fact]
    public void PlayerAttack_UsesEquippedWeaponProfile_NotFallbackAbility()
    {
        var (enc, tick) = Build();
        var ironSword = new AttackProfile
        {
            Name = "Iron Sword",
            DamageType = DamageType.Slashing,
            BaseDamage = 20, // far above the fallback strike's 8
            StaminaCost = 5,
            Timing = new AbilityTiming { TelegraphTicks = 1, WindupTicks = 4, RecoveryTicks = 10 },
        };
        var player = Player(hp: 100, attrs: Attrs(str: 5), attack: ironSword);
        var enemy = Enemy("Dummy", 100, Attrs(con: 2), "ability.goblin_slash");
        enc.Start(player, new[] { enemy });

        Assert.True(enc.Attack());
        tick.Advance(5); // weapon impact = telegraph 1 + windup 4

        // 20 + STR5*0.5 = 22.5; armor CON2*0.3 = 0.6 → 21.9 → 22
        Assert.Equal(78, enemy.Health.Current);
    }

    [Fact]
    public void UseHealingItem_RestoresHealth_AndCostsAttackTempo()
    {
        var (enc, tick) = Build();
        var player = Player(hp: 100, attrs: Attrs());
        enc.Start(player, new[] { Enemy("Brute", 500, Attrs(str: 1), "ability.goblin_smash") }); // slow, won't interfere

        player.Health.Reduce(60); // 40/100
        Assert.True(enc.PlayerReady);

        var healed = enc.UseHealingItem("Healing Salve", 25);
        Assert.Equal(25, healed);
        Assert.Equal(65, player.Health.Current);
        Assert.False(enc.PlayerReady); // spent tempo — can't immediately strike

        tick.Advance(9);
        Assert.False(enc.PlayerReady);
        tick.Advance(1); // ItemUseRecoveryTicks = 10
        Assert.True(enc.PlayerReady);
    }

    [Fact]
    public void EnemyAi_LoopsAcrossRecovery()
    {
        var (enc, tick) = Build();
        var player = Player(hp: 100, attrs: Attrs(con: 5));
        enc.Start(player, new[] { Enemy("Raider", 500, Attrs(str: 6), "ability.goblin_slash") });

        tick.Advance(60); // hits at tick 16 and tick 52 (recovery 20 → decide at 36 → impact 52)

        Assert.Equal(84, player.Health.Current); // two 8-damage hits
    }
}
