using Dungeons.Combat;
using Dungeons.Events;
using Dungeons.Simulation;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Combat;

/// <summary>
/// E2a — telegraph and windup are separate scheduler states (docs/moves.md §2.3).
///
/// <para>GDD §5.2 recorded this as *"the riskiest single change in the combat roadmap"* and the
/// reason "interrupt during windup" was inexpressible: the two phases were collapsed into one
/// time-to-impact value. Splitting them changes no timing — the same total ticks to impact —
/// but makes the committed window addressable.</para>
/// </summary>
public class ActionLifecycleTests
{
    private static readonly AbilityDefinition Strike = Ability("ability.strike", DamageType.Slashing, 8, 2, 8, 15, stamina: 5);
    private static readonly AbilityDefinition Slash = Ability("ability.goblin_slash", DamageType.Slashing, 6, 8, 8, 20);
    private static readonly AbilityDefinition Instant = Ability("ability.instant", DamageType.Piercing, 5, 0, 4, 10);

    private static (CombatEncounter enc, TickEngine tick, List<GameEvent> log) Build()
    {
        var tick = new TickEngine();
        var bus = new GameEventBus();
        var log = new List<GameEvent>();
        bus.Subscribe(log.Add);

        var enc = new CombatEncounter(
            tick, new CombatCalculator(new FakeRandom(0.99)), Abilities(Strike, Slash, Instant),
            new FakeRandom(0.99), bus, "ability.strike");

        return (enc, tick, log);
    }

    // --- The phases are real ------------------------------------------------

    [Fact]
    public void AnActionTelegraphsFirst_ThenWindsUp_ThenLands()
    {
        var (enc, tick, _) = Build();
        var player = Player(hp: 200, attrs: Attrs(con: 5));
        var enemy = Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash");
        enc.Start(player, new[] { enemy });

        // Rusty Slash: telegraph 8, windup 8.
        Assert.Equal(ActionPhase.Telegraph, enc.ActionOf(enemy)!.Phase);

        tick.Advance(7);
        Assert.Equal(ActionPhase.Telegraph, enc.ActionOf(enemy)!.Phase);

        tick.Advance(1); // telegraph ends
        Assert.Equal(ActionPhase.Windup, enc.ActionOf(enemy)!.Phase);
        Assert.Equal(200, player.Health.Current); // nothing has landed

        tick.Advance(8); // windup ends
        Assert.Null(enc.ActionOf(enemy));
        Assert.True(player.Health.Current < 200);
    }

    [Fact]
    public void TotalTimeToImpactIsUnchangedBySplittingThePhases()
    {
        // The split must be behaviour-preserving for timing, or every existing tuning value and
        // every telegraph-reading habit silently shifts.
        var (enc, tick, _) = Build();
        var player = Player(hp: 200, attrs: Attrs(con: 5));
        var enemy = Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash");
        enc.Start(player, new[] { enemy });

        Assert.Equal(16, enc.Intents[0].ExecuteTick); // telegraph 8 + windup 8, exactly as before

        tick.Advance(15);
        Assert.Equal(200, player.Health.Current);
        tick.Advance(1);
        Assert.True(player.Health.Current < 200);
    }

    [Fact]
    public void TheReportedExecuteTickIsStableAcrossThePhaseBoundary()
    {
        // The UI renders a countdown off this. It must not jump when the phase flips.
        var (enc, tick, _) = Build();
        var enemy = Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash");
        enc.Start(Player(hp: 200, attrs: Attrs(con: 5)), new[] { enemy });

        var duringTelegraph = enc.Intents[0].ExecuteTick;
        tick.Advance(8);
        var duringWindup = enc.Intents[0].ExecuteTick;

        Assert.Equal(duringTelegraph, duringWindup);
    }

    [Fact]
    public void AnActionWithNoTelegraphGoesStraightToWindup()
    {
        // Ambushes and instant moves. Correspondingly harder to answer, which is the point.
        var (enc, _, _) = Build();
        var enemy = Enemy("Cutpurse", 50, Attrs(str: 6), "ability.instant");
        enc.Start(Player(hp: 200, attrs: Attrs(con: 5)), new[] { enemy });

        Assert.Equal(ActionPhase.Windup, enc.ActionOf(enemy)!.Phase);
    }

    [Fact]
    public void ThePlayersOwnAttackHasPhasesToo()
    {
        // Symmetry matters: Trickster's Feint cancels the player's telegraph, and E4 gives both
        // sides the same Move lifecycle.
        var (enc, tick, _) = Build();
        var player = Player(hp: 100, attrs: Attrs(str: 5), stamina: 50);
        enc.Start(player, new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });

        enc.Attack(); // Strike: telegraph 2, windup 8
        Assert.Equal(ActionPhase.Telegraph, enc.ActionOf(player)!.Phase);

        tick.Advance(2);
        Assert.Equal(ActionPhase.Windup, enc.ActionOf(player)!.Phase);
    }

    // --- Interrupts ---------------------------------------------------------

    [Fact]
    public void InterruptingAWindupStopsTheHitLanding()
    {
        var (enc, tick, log) = Build();
        var player = Player(hp: 200, attrs: Attrs(con: 5));
        var enemy = Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash");
        enc.Start(player, new[] { enemy });

        tick.Advance(10); // into windup
        Assert.Equal(ActionPhase.Windup, enc.ActionOf(enemy)!.Phase);

        Assert.True(enc.Interrupt(enemy));
        Assert.Null(enc.ActionOf(enemy));

        tick.Advance(20); // well past the original impact tick
        Assert.Equal(200, player.Health.Current);

        var interrupted = log.Single(e => e.Kind == GameEvents.ActionInterrupted);
        Assert.Equal("Raider", interrupted.Source);
        Assert.True(interrupted.HasTag("windup"));
    }

    [Fact]
    public void InterruptingATelegraphIsTaggedDifferently()
    {
        // So content can distinguish "stopped them before they swung" (a read) from "stopped
        // them mid-swing" (a punish) — the whole reason the phases are separate.
        var (enc, tick, log) = Build();
        var enemy = Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash");
        enc.Start(Player(hp: 200, attrs: Attrs(con: 5)), new[] { enemy });

        tick.Advance(3); // still telegraphing
        Assert.True(enc.Interrupt(enemy));

        var interrupted = log.Single(e => e.Kind == GameEvents.ActionInterrupted);
        Assert.True(interrupted.HasTag("telegraph"));
        Assert.False(interrupted.HasTag("windup"));
    }

    [Fact]
    public void InterruptingSomeoneWhoIsNotActingDoesNothing()
    {
        var (enc, tick, _) = Build();
        var enemy = Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash");
        enc.Start(Player(hp: 200, attrs: Attrs(con: 5)), new[] { enemy });

        enc.Interrupt(enemy);
        tick.Advance(1);

        Assert.False(enc.Interrupt(enemy)); // already interrupted; now in recovery
    }

    [Fact]
    public void AnInterruptedEnemyStillPaysRecoveryThenActsAgain()
    {
        // Being stopped is not free tempo for the victim — otherwise interrupt-locking would be
        // strictly better than killing.
        var (enc, tick, _) = Build();
        var enemy = Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash");
        enc.Start(Player(hp: 500, attrs: Attrs(con: 5)), new[] { enemy });

        tick.Advance(10);
        enc.Interrupt(enemy);
        Assert.Null(enc.ActionOf(enemy));

        tick.Advance(20); // recovery 20
        Assert.NotNull(enc.ActionOf(enemy)); // decided again
    }

    [Fact]
    public void KillingAnEnemyMidWindupCancelsItsAction()
    {
        var (enc, tick, _) = Build();
        var player = Player(hp: 200, attrs: Attrs(str: 40), stamina: 50);
        var enemy = Enemy("Raider", 1, Attrs(str: 6), "ability.goblin_slash");
        enc.Start(player, new[] { enemy });

        enc.Attack();
        tick.Advance(10); // the player's Strike lands and kills

        Assert.Null(enc.ActionOf(enemy));
        Assert.Equal(200, player.Health.Current); // its windup never resolved
    }
}
