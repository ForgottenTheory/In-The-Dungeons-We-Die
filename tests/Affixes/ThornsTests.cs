using Dungeons.Affixes;
using Dungeons.Characters;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Events;
using Dungeons.Randomness;
using Dungeons.Rules;
using Dungeons.Simulation;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Affixes;

/// <summary>
/// The retaliation family, end to end (damage-and-defense.md §7): a worn Brambled affix's rule
/// grant attached to the engine, a real enemy swing landing, and the attacker bleeding for it —
/// at depth 1, through the standing proc-safety rules, with zero new machinery.
/// </summary>
public class ThornsTests
{
    [Fact]
    public void AWornThornsAffixRetaliatesWhenTheOwnerIsHit()
    {
        var tick = new TickEngine();
        var bus = new GameEventBus();
        var statuses = new StatusController(
            TestPaths.LoadStore<StatusDefinition>("statuses"), bus, () => tick.CurrentTick);
        var gauges = new GaugeController(Array.Empty<Dungeons.Characters.Composition.GaugeDefinition>());
        var modifiers = new CombatantModifiers(
            TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
            isOwner: c => c.Team == CombatTeam.Player,
            buildModifiers: () => Array.Empty<Dungeons.Modifiers.ModifierContribution>(),
            statuses, gauges);

        var rng = new FakeRandom(0.99); // no crits, no procs-by-chance
        var slash = Move("move.slash", DamageType.Slashing, 8, 8, 6, 10);
        var encounter = new CombatEncounter(
            tick, new HitPipeline(rng, modifiers), Moves(slash), rng, bus, statuses, gauges, modifiers);

        var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => tick.CurrentTick)
            .RegisterCombatHandlers(encounter, rng);

        // The affix's own rule grant, exactly as GameRoot attaches it from worn gear (R4b).
        var bramble = TestPaths.LoadStore<AffixDefinition>("affixes").GetById("affix.bramble");
        var rolled = new RolledAffix("affix.bramble", 2, 4.0);
        foreach (var rule in AffixGrants.Rules(rolled, bramble))
            engine.Attach(rule, "Brambled (test vest)");

        var player = Player(hp: 200, attrs: Attrs(), stamina: 50);
        var raider = Enemy("Raider", 100, Attrs(), slash);
        encounter.Start(player, new[] { raider });

        var enemyBefore = encounter.Enemies[0].Health.Current;

        // Let the raider's slash telegraph, wind up and land — the player never acts.
        tick.Advance(40);

        Assert.True(player.Health.Current < 200, "the slash must actually land for thorns to answer");
        Assert.True(encounter.Enemies[0].Health.Current < enemyBefore,
            $"the attacker should bleed for the retaliation ({enemyBefore} → {encounter.Enemies[0].Health.Current})");
    }

    /// <summary>R4c-2: Barrier absorbs before Health does, and shattering raises
    /// <c>BarrierBroken</c> — the long-standing HitPipeline debt, closed.</summary>
    [Fact]
    public void BarrierAbsorbsDamageBeforeHealthAndShatters()
    {
        var tick = new TickEngine();
        var bus = new GameEventBus();
        var statuses = new StatusController(
            TestPaths.LoadStore<StatusDefinition>("statuses"), bus, () => tick.CurrentTick);
        var gauges = new GaugeController(Array.Empty<Dungeons.Characters.Composition.GaugeDefinition>());
        var modifiers = new CombatantModifiers(
            TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
            isOwner: c => c.Team == CombatTeam.Player,
            buildModifiers: () => Array.Empty<Dungeons.Modifiers.ModifierContribution>(),
            statuses, gauges);

        var rng = new FakeRandom(0.99);
        var slash = Move("move.slash", DamageType.Slashing, 8, 8, 6, 10);
        var encounter = new CombatEncounter(
            tick, new HitPipeline(rng, modifiers), Moves(slash), rng, bus, statuses, gauges, modifiers);

        var broken = false;
        bus.Subscribe(GameEvents.BarrierBroken, _ => broken = true);

        var player = Player(hp: 200, attrs: Attrs(), stamina: 50);
        var raider = Enemy("Raider", 100, Attrs(), slash);
        encounter.Start(player, new[] { raider });

        // A small ward: it should soak the first points of the incoming slash, then shatter.
        encounter.ApplyStatus(player, "status.barrier", "test", magnitude: 3);

        tick.Advance(40);

        Assert.True(player.Health.Current < 200, "the hit must land");
        Assert.True(broken, "a fully-consumed barrier must raise BarrierBroken");
        Assert.Null(statuses.Find(player, "status.barrier"));
    }

    /// <summary>R4c-2: the receiver's `status.duration.mult` shortens landed statuses.</summary>
    [Fact]
    public void StatusDurationScalesOnTheReceiver()
    {
        var statuses = new StatusController(
            TestPaths.LoadStore<StatusDefinition>("statuses"), new GameEventBus(), () => 0);
        var victim = Player(hp: 100, attrs: Attrs());

        statuses.Apply(victim, "status.bleed", "test", magnitude: 5);
        var full = statuses.Find(victim, "status.bleed")!.ExpiresTick;

        var other = Player(hp: 100, attrs: Attrs());
        statuses.Apply(other, "status.bleed", "test", magnitude: 5, durationMultiplier: 0.5);
        var halved = statuses.Find(other, "status.bleed")!.ExpiresTick;

        Assert.True(halved < full, $"half duration must expire sooner ({halved} vs {full})");
    }
}
