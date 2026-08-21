using Dungeons.Characters;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting.Identity;
using Dungeons.Events;
using Dungeons.Randomness;
using Dungeons.Rules;
using Dungeons.Simulation;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Combat;

/// <summary>
/// The retaliation seam and the Barrier/status machinery, end to end. Originally pinned
/// through a worn affix (R4c); re-grounded on an identity sentence in Phase 7 (D54) — the
/// machinery is the same, only the grantor changed, which is exactly D50's one-language claim.
/// </summary>
public class RetaliationAndBarrierTests
{
    [Fact]
    public void AWornRetaliationSentenceStrikesBackWhenTheOwnerIsHit()
    {
        var content = new ContentBundle
        {
            SignatureTriggers = TestPaths.LoadStore<SignatureTriggerDefinition>("signature_triggers"),
            SignatureBehaviors = TestPaths.LoadStore<SignatureBehaviorDefinition>("signature_behaviors"),
            SignaturePayloads = TestPaths.LoadStore<SignaturePayloadDefinition>("signature_payloads"),
            Statuses = TestPaths.LoadStore<StatusDefinition>("statuses"),
            ModifierKeys = TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
            Moves = TestPaths.LoadStore<MoveDefinition>("moves"),
            MoveModifiers = TestPaths.LoadStore<MoveModifierDefinition>("move_modifiers"),
        };

        var tick = new TickEngine();
        var bus = new GameEventBus();
        var statuses = new StatusController(content.Statuses, bus, () => tick.CurrentTick);
        var gauges = new GaugeController(Array.Empty<Dungeons.Characters.Composition.GaugeDefinition>());
        var modifiers = new CombatantModifiers(
            content.ModifierKeys,
            isOwner: c => c.Team == CombatTeam.Player,
            buildModifiers: () => Array.Empty<Dungeons.Modifiers.ModifierContribution>(),
            statuses, gauges);

        var rng = new FakeRandom(0.99); // no crits, no procs-by-chance
        var slash = Move("move.slash", DamageType.Slashing, 8, 8, 6, 10);
        var encounter = new CombatEncounter(
            tick, new HitPipeline(rng, modifiers), Moves(slash), rng, bus, statuses, gauges, modifiers);

        var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => tick.CurrentTick)
            .RegisterCombatHandlers(encounter, rng);

        // A worn item's retaliation sentence, compiled to grants exactly as GameRoot attaches
        // them from worn gear (Phase 3's equip seam): when struck, strike back.
        var sentence = new ItemEffectSentence(
            ItemEffectCategory.Generated, "on_being_struck", "retaliate", "slam", 4.0, 1.0);
        var compiled = new ItemEffectResolver(content).CompileAll(new[] { sentence });
        Assert.NotEmpty(compiled.Rules);
        foreach (var rule in compiled.Rules)
            engine.Attach(rule, "Thorned (test vest)");

        var player = Player(hp: 200, attrs: Attrs(), stamina: 50);
        var raider = Enemy("Raider", 100, Attrs(), slash);
        encounter.Start(player, new[] { raider });

        var enemyBefore = encounter.Enemies[0].Health.Current;

        // Let the raider's slash telegraph, wind up and land — the player never acts.
        tick.Advance(40);

        Assert.True(player.Health.Current < 200, "the slash must actually land for retaliation to answer");
        Assert.True(encounter.Enemies[0].Health.Current < enemyBefore,
            $"the attacker should bleed for the retaliation ({enemyBefore} → {encounter.Enemies[0].Health.Current})");
    }

    /// <summary>Barrier absorbs before Health does, and shattering raises
    /// <c>BarrierBroken</c> — the HitPipeline debt R4c-2 closed, still closed.</summary>
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

    /// <summary>The receiver's `status.duration.mult` shortens landed statuses.</summary>
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
