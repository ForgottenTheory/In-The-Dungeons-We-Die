using Dungeons.Actions;
using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Events;
using Dungeons.Randomness;
using Dungeons.Rules;
using Dungeons.Simulation;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Combat;

/// <summary>
/// E4 in motion — moves executing inside a real encounter: costs and cooldowns at queue time,
/// riders through the handler registry, stagger against Resolve, AI choosing by rule, and the
/// Mnemonic loop closing end to end.
/// </summary>
public class MoveSystemTests
{
    private static readonly MoveDefinition Slash = Move("move.goblin_slash", DamageType.Slashing, 6, 8, 8, 20);

    private sealed record Harness(
        CombatEncounter Encounter, TickEngine Tick, TriggerRuleEngine Engine,
        GaugeController Gauges, List<GameEvent> Events);

    private static Harness Build(
        DataStore<MoveDefinition>? moves = null,
        IEnumerable<GaugeDefinition>? gauges = null,
        DataStore<MoveModifierDefinition>? moveModifiers = null,
        double roll = 0.99)
    {
        var tick = new TickEngine();
        var bus = new GameEventBus();
        var events = new List<GameEvent>();
        bus.Subscribe(events.Add);

        var statuses = new StatusController(
            TestPaths.LoadStore<StatusDefinition>("statuses"), bus, () => tick.CurrentTick);
        var gaugeController = new GaugeController(gauges ?? Array.Empty<GaugeDefinition>());

        var rng = new FakeRandom(roll);
        var encounter = new CombatEncounter(
            tick, new HitPipeline(rng), moves ?? Moves(Slash), rng, bus,
            statuses, gaugeController, moveModifiers: moveModifiers);

        var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => tick.CurrentTick,
                new CombatConditionWorld(encounter))
            .RegisterCombatHandlers(encounter, rng);

        encounter.ConditionWorld = new CombatConditionWorld(encounter);

        return new Harness(encounter, tick, engine, gaugeController, events);
    }

    // --- Queue-time gating (§2.3: the Queue phase) --------------------------------------------

    [Fact]
    public void AMoveOnCooldownIsRefusedUntilItComesBack()
    {
        var burst = Move("move.burst", DamageType.Slashing, 10, 1, 2, 3);
        var withCooldown = new MoveDefinition
        {
            Id = burst.Id, Name = burst.Name, Tags = burst.Tags, Timing = burst.Timing,
            Packets = burst.Packets, CooldownTicks = 100,
        };

        var h = Build();
        h.Encounter.Start(
            Player(moveset: Set(withCooldown)),
            new[] { Enemy("Raider", 200, Attrs(str: 6), Slash) });

        Assert.True(h.Encounter.UseMove("move.burst"));
        h.Tick.Advance(30);   // impact + recovery elapse; cooldown has not

        Assert.False(h.Encounter.UseMove("move.burst"));
        h.Tick.Advance(80);
        Assert.True(h.Encounter.UseMove("move.burst"));
    }

    [Fact]
    public void GaugeCostsSpendTheGauge_AndCannotFeedThemselves()
    {
        var discharge = new MoveDefinition
        {
            Id = "move.discharge", Name = "Discharge",
            Tags = new[] { "action:attack", "delivery:melee" },
            Timing = new ActionTiming { TelegraphTicks = 1, WindupTicks = 2, RecoveryTicks = 4 },
            Costs = new[] { new ActionCost { Resource = "Charge", Amount = 30 } },
            Packets = new[] { new Packet(DamageType.Magic, 15) },
        };

        var h = Build(gauges: new[] { new GaugeDefinition { Name = "Charge", Max = 100 } });

        // The Galvanic-style feed: ANY ResourceSpent charges the meter — except gauge spends,
        // which carry the `gauge` tag exactly so this loop cannot close.
        h.Engine.Attach(new TriggerRule
        {
            Id = "feed", Event = GameEvents.ResourceSpent,
            When = new[] { new ConditionSpec { Kind = RuleVocabulary.HasTag, Text = "gauge", Negate = true } },
            Target = EffectTarget.Self,
            Effect = new EffectSpec { Kind = RuleVocabulary.GrantResource, Text = "Charge", Amount = 1.2, ScalesWith = "amount" },
        }, "prefix.galvanic");

        h.Encounter.Start(
            Player(moveset: Set(discharge)),
            new[] { Enemy("Raider", 200, Attrs(str: 6), Slash) });

        h.Gauges.Add("Charge", 50, h.Tick.CurrentTick);

        Assert.True(h.Encounter.UseMove("move.discharge"));
        Assert.Equal(20, h.Gauges.Current("Charge"), 3);   // 50 − 30, and NOT re-fed 36

        h.Gauges.Add("Charge", 5, h.Tick.CurrentTick);     // 25 total — below the cost
        h.Tick.Advance(10);
        Assert.False(h.Encounter.UseMove("move.discharge"));
    }

    // --- Execution: riders, stagger, immunity to interrupts -----------------------------------

    /// <summary>Fireball end to end from shipped content: mana paid, heat packet resolved, and
    /// the 20% Burn rider actually rolling through the registered handlers.</summary>
    [Fact]
    public void FireballBurnsWhenTheRiderChanceLands()
    {
        var moves = TestPaths.LoadStore<MoveDefinition>("moves");
        var fireball = MovesetBuilder.Apply(
            moves.GetById("move.fireball"), Array.Empty<MoveOpSpec>(), new[] { "Wizard" });

        // roll 0.10 < 0.2 rider chance → the Burn lands (and crit fails at 0.10? crit chance is
        // luck-based and Attrs(luck: 0) rolls none).
        var h = Build(moves, roll: 0.10);

        var caster = new Combatant(
            "Hero", CombatTeam.Player,
            new ResourcePool(ResourceType.Health, 100),
            new ResourcePool(ResourceType.Stamina, 50),
            new ResourcePool(ResourceType.Mana, 40),
            new[] { fireball },
            () => Attrs());

        h.Encounter.Start(caster, new[] { Enemy("Raider", 200, Attrs(str: 6), Slash) });

        Assert.True(h.Encounter.UseMove("move.fireball"));
        h.Tick.Advance(25);

        Assert.Equal(22, caster.Mana.Current);   // 40 − 18
        Assert.True(h.Encounter.Statuses!.Has(h.Encounter.Enemies[0], "status.burn"),
            "the 20% rider should have landed on a 0.10 roll");
    }

    /// <summary>Shield Bash's stagger is control buildup against Resolve — enough of it lands a
    /// Stun without the move dealing meaningful damage (D-08).</summary>
    [Fact]
    public void StaggerAccumulatesTowardStunThroughResolve()
    {
        var bash = new MoveDefinition
        {
            Id = "move.bash", Name = "Bash",
            Tags = new[] { "action:attack", "delivery:melee" },
            Timing = new ActionTiming { TelegraphTicks = 1, WindupTicks = 2, RecoveryTicks = 4 },
            Packets = new[] { new Packet(DamageType.Crushing, 2) },
            StaggerPower = 30,
        };

        var h = Build();
        var raider = Enemy("Raider", 300, Attrs(str: 6), Slash);
        h.Encounter.Start(Player(moveset: Set(bash), stamina: 200), new[] { raider });

        // Default Resolve is 50: two bashes build 60 and the second crosses it.
        Assert.True(h.Encounter.UseMove("move.bash"));
        h.Tick.Advance(10);
        Assert.False(h.Encounter.Statuses!.Has(raider, "status.stun"));

        Assert.True(h.Encounter.UseMove("move.bash"));
        h.Tick.Advance(10);
        Assert.True(h.Encounter.Statuses.Has(raider, "status.stun"));
    }

    [Fact]
    public void AnUninterruptibleMoveShrugsOffTheInterrupt()
    {
        var resolute = new MoveDefinition
        {
            Id = "move.resolute", Name = "Resolute Swing",
            Tags = new[] { "action:attack", "delivery:melee" },
            Timing = new ActionTiming { TelegraphTicks = 10, WindupTicks = 10, RecoveryTicks = 5 },
            Packets = new[] { new Packet(DamageType.Crushing, 5) },
            Interruptible = false,
        };

        var h = Build();
        h.Encounter.Start(
            Player(moveset: Set(resolute), stamina: 100),
            new[] { Enemy("Raider", 200, Attrs(str: 6), Slash) });

        Assert.True(h.Encounter.UseMove("move.resolute"));
        Assert.False(h.Encounter.Interrupt(h.Encounter.Player));
        Assert.NotNull(h.Encounter.ActionOf(h.Encounter.Player));
    }

    // --- Enemy AI (§5.2) ----------------------------------------------------------------------

    /// <summary>The AI profile picks by condition: an execute rule gated on the target being
    /// stunned outweighs the default swing exactly when the gate opens.</summary>
    [Fact]
    public void AiPrefersItsGatedMoveWhenTheConditionOpens()
    {
        var poke = Move("move.poke", DamageType.Piercing, 2, 2, 2, 6);
        var execute = Move("move.execute", DamageType.Slashing, 30, 2, 2, 6);

        var brute = new Combatant(
            "Brute", CombatTeam.Enemy,
            new ResourcePool(ResourceType.Health, 300),
            new ResourcePool(ResourceType.Stamina, 100),
            new ResourcePool(ResourceType.Mana, 0),
            Set(poke, execute),
            () => Attrs(str: 8),
            ai: new[]
            {
                new AiRuleSpec
                {
                    When = new[] { new ConditionSpec { Kind = RuleVocabulary.TargetHasStatus, Text = "status.stun" } },
                    Move = "move.execute", Weight = 100,
                },
                new AiRuleSpec { Move = "move.poke", Weight = 1 },
            });

        var h = Build();
        h.Encounter.Start(Player(hp: 500, attrs: Attrs(con: 5)), new[] { brute });

        // Ungated: only the poke rule passes, so the first committed action is the poke.
        Assert.Equal("move.poke", h.Encounter.ActionOf(brute)!.Move.Id);

        // Stun the player; after the brute's recovery, the gate is open and weight 100 wins.
        h.Encounter.Interrupt(brute);
        h.Encounter.ApplyStatus(h.Encounter.Player, "status.stun", "Brute", magnitude: 100, durationOverride: 600);

        // Recovery is 6 ticks; probe at 7, while the fresh decision is still in telegraph —
        // probing later can land between actions, where there is nothing in flight to read.
        h.Tick.Advance(7);

        Assert.Equal("move.execute", h.Encounter.ActionOf(brute)!.Move.Id);
    }

    /// <summary>Same seed, same state ⇒ same choice (§6: AI determinism).</summary>
    [Fact]
    public void AiChoiceIsDeterministicUnderTheSeed()
    {
        static string FirstChoice()
        {
            var a = Move("move.a", DamageType.Slashing, 3, 4, 4, 8);
            var b = Move("move.b", DamageType.Crushing, 3, 4, 4, 8);

            var tick = new TickEngine();
            var bus = new GameEventBus();
            var rng = new SeededRandom(0xBEEF);
            var enc = new CombatEncounter(tick, new HitPipeline(rng), Moves(a, b), rng, bus);

            var enemy = new Combatant(
                "E", CombatTeam.Enemy,
                new ResourcePool(ResourceType.Health, 100),
                new ResourcePool(ResourceType.Stamina, 100),
                new ResourcePool(ResourceType.Mana, 0),
                Set(a, b),
                () => Attrs(),
                ai: new[]
                {
                    new AiRuleSpec { Move = "move.a", Weight = 1 },
                    new AiRuleSpec { Move = "move.b", Weight = 3 },
                });

            enc.Start(Player(), new[] { enemy });
            return enc.ActionOf(enemy)!.Move.Id;
        }

        Assert.Equal(FirstChoice(), FirstChoice());
    }

    // --- triggerMove and the Mnemonic loop (§3.4) ---------------------------------------------

    [Fact]
    public void TriggerMoveExecutesImmediatelyAtTheChainsNextDepth()
    {
        var bolt = Move("move.bolt", DamageType.Magic, 7, 4, 6, 8);
        var h = Build(Moves(Slash, bolt));

        h.Engine.Attach(new TriggerRule
        {
            Id = "storm", Event = GameEvents.MoveExecuted,
            When = new[] { new ConditionSpec { Kind = RuleVocabulary.SourceIsSelf, Text = "self" } },
            Effect = new EffectSpec { Kind = RuleVocabulary.TriggerMove, Text = "move.bolt" },
        }, "test.storm");

        var h2 = h;
        h2.Encounter.Start(Player(stamina: 100), new[] { Enemy("Raider", 200, Attrs(str: 6), Slash) });
        var raider = h2.Encounter.Enemies[0];
        var before = raider.Health.Current;

        h2.Encounter.Attack();
        h2.Tick.Advance(12);

        // The strike landed AND the bolt landed — immediately, with no second windup. The rule
        // fired at depth 1 and the triggered move executes at depth+1 = 2 (§3.4), which is the
        // ceiling — so nothing can proc off the bolt at all.
        var boltHits = h2.Events.Where(e => e.Kind == GameEvents.DamageDealt && e.ChainId is not null).ToList();
        Assert.NotEmpty(boltHits);
        Assert.All(boltHits, e => Assert.Equal(2, e.Depth));
        Assert.True(raider.Health.Current < before - 7);

        // …and its own MoveExecuted cannot re-trigger the rule (once per chain + depth).
        Assert.Single(h2.Engine.Fired, f => f.Kind == RuleVocabulary.TriggerMove);
    }

    /// <summary>
    /// <b>The Mnemonic, end to end.</b> Swing → the store rule remembers the move → Recall
    /// replays it instantly and consumes the memory. This is the fantasy the fourteenth
    /// dangling status id was waiting on, closed.
    /// </summary>
    [Fact]
    public void MnemonicStoresYourLastMoveAndRecallReplaysIt()
    {
        var shipped = TestPaths.LoadStore<MoveDefinition>("moves");
        var prefixes = TestPaths.LoadStore<PrefixDefinition>("prefixes");
        var mnemonic = prefixes.GetById("prefix.mnemonic");

        var h = Build(shipped);
        foreach (var rule in mnemonic.Rules)
            h.Engine.Attach(rule, mnemonic.Id);

        // The stored move must be one TriggerMove can find in the store — a shipped move, not a
        // test-local one. That is also the honest shape: the memory holds an id, not a body.
        var strike = MovesetBuilder.Apply(
            shipped.GetById("move.iron_slash"), Array.Empty<MoveOpSpec>(), new[] { "Iron Sword" });
        var recall = MovesetBuilder.Apply(
            shipped.GetById("move.recall"), Array.Empty<MoveOpSpec>(), new[] { mnemonic.Name });

        h.Encounter.Start(
            Player(moveset: new[] { strike, recall }, stamina: 100),
            new[] { Enemy("Raider", 200, Attrs(str: 6), Slash) });
        var player = h.Encounter.Player;
        var raider = h.Encounter.Enemies[0];

        // Recall with nothing stored: refused by its requirement.
        Assert.False(h.Encounter.UseMove("move.recall"));

        h.Encounter.Attack();
        h.Tick.Advance(12);

        var stored = h.Encounter.Statuses!.Find(player, CombatEncounter.RecalledMoveStatusId);
        Assert.NotNull(stored);
        Assert.Equal("move.iron_slash", stored!.StoredMoveId);

        var before = raider.Health.Current;
        h.Tick.Advance(20);   // recovery

        Assert.True(h.Encounter.UseMove("move.recall"));
        h.Tick.Advance(5);    // recall itself: telegraph 0, windup 1

        Assert.True(raider.Health.Current < before, "the replayed strike should land");

        // The recall consumed the memory — and then the REPLAYED strike's own MoveExecuted
        // re-stored itself, because "remembers your most recent move" honestly includes the
        // replay. The loop is bounded by Recall's 150-tick cooldown, not by forgetting.
        var restored = h.Encounter.Statuses.Find(player, CombatEncounter.RecalledMoveStatusId);
        Assert.NotNull(restored);
        Assert.Equal("move.iron_slash", restored!.StoredMoveId);
    }

    // --- grantMove / modifyMove ----------------------------------------------------------------

    [Fact]
    public void GrantMoveAddsAMoveUntilItExpires()
    {
        var gift = Move("move.gift", DamageType.Magic, 5, 1, 2, 4);
        var h = Build(Moves(Slash, gift));

        h.Encounter.Start(Player(stamina: 100), new[] { Enemy("Raider", 200, Attrs(str: 6), Slash) });

        Assert.False(h.Encounter.UseMove("move.gift"));

        h.Encounter.GrantMove(h.Encounter.Player, "move.gift", "test", durationTicks: 40);
        Assert.Contains(h.Encounter.PlayerMoves, m => m.Id == "move.gift");

        h.Tick.Advance(60);   // past expiry; the sweep collects it
        Assert.DoesNotContain(h.Encounter.PlayerMoves, m => m.Id == "move.gift");
    }

    /// <summary>`modifyMove` applies at execution, so "the next attack is empowered" works on a
    /// cached moveset.</summary>
    [Fact]
    public void ModifyMoveEmpowersTheNextExecution()
    {
        var strike = Move("move.strike", DamageType.Slashing, 10, 1, 2, 4, stamina: 2);
        var modifiers = new DataStore<MoveModifierDefinition>();
        modifiers.Add(new MoveModifierDefinition
        {
            Id = "mod.empower",
            Match = new MoveMatch { TagsAll = new[] { "action:attack" } },
            Ops = new[] { new MoveOpSpec { Op = "scaleDamage", Value = 2.0 } },
        });

        var h = Build(Moves(strike), moveModifiers: modifiers);
        var raider = Enemy("Raider", 300, Attrs(str: 6), Slash);
        h.Encounter.Start(Player(moveset: Set(strike), stamina: 100), new[] { raider });

        h.Encounter.UseMove("move.strike");
        h.Tick.Advance(10);
        var plainDamage = 300 - raider.Health.Current;

        h.Encounter.AttachMoveModifier(h.Encounter.Player, "mod.empower", "test", durationTicks: 200);
        h.Tick.Advance(10);
        var beforeEmpowered = raider.Health.Current;
        h.Encounter.UseMove("move.strike");
        h.Tick.Advance(10);
        var empoweredDamage = beforeEmpowered - raider.Health.Current;

        Assert.True(empoweredDamage > plainDamage * 1.5,
            $"empowered {empoweredDamage} should roughly double plain {plainDamage}");
    }
}
