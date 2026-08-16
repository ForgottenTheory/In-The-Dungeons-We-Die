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

namespace Dungeons.Tests.Rules;

/// <summary>
/// E3c — effects stop landing in <c>Unhandled</c> and start changing the fight.
///
/// <para>E0 made combat raise events, E1–E2 gave them real damage and statuses, E3a gave rules
/// targets and a proc budget. All of it fired into nothing: every effect a shipped Prefix or
/// Suffix declared was recorded and discarded. These tests pin the slice that closes that —
/// and, more importantly, pin the two rules that break silently if a handler forgets them: the
/// causal chain must survive a handler, and an ailment tick must never proc.</para>
/// </summary>
public class EffectHandlerTests
{
    private static readonly AbilityDefinition Strike = Ability("ability.strike", DamageType.Slashing, 8, 2, 8, 15, stamina: 5);
    private static readonly AbilityDefinition Slash = Ability("ability.goblin_slash", DamageType.Slashing, 6, 8, 8, 20);

    private sealed record Harness(
        CombatEncounter Encounter,
        TickEngine Tick,
        GameEventBus Bus,
        TriggerRuleEngine Engine,
        GaugeController Gauges,
        List<GameEvent> Events);

    private static Harness Build(IEnumerable<GaugeDefinition>? gauges = null, double roll = 0.99)
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
            tick, new CombatCalculator(rng), Abilities(Strike, Slash), rng, bus, "ability.strike",
            statuses, gaugeController);

        var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => tick.CurrentTick)
            .RegisterCombatHandlers(encounter, rng);

        return new Harness(encounter, tick, bus, engine, gaugeController, events);
    }

    private static TriggerRule Rule(
        string id, string @event, string kind, double amount = 0, string text = "",
        EffectTarget target = EffectTarget.TriggerTarget, string scalesWith = "") => new()
    {
        Id = id,
        Event = @event,
        Target = target,
        Effect = new EffectSpec { Kind = kind, Amount = amount, Text = text, ScalesWith = scalesWith },
    };

    private static void StartFight(Harness h, int playerHp = 100, int enemyHp = 50) =>
        h.Encounter.Start(
            Player(hp: playerHp, attrs: Attrs(con: 5), stamina: 50),
            new[] { Enemy("Raider", enemyHp, Attrs(str: 6), "ability.goblin_slash") });

    // --- The payoff the slice exists for --------------------------------------------------

    /// <summary>
    /// <b>Galvanic's Charge finally accumulates.</b> Real prefix content out of
    /// <c>game/data</c>, a real fight, and a meter that moves — with no per-Base wiring
    /// anywhere. Until this slice the feed fired and its <c>grantResource</c> was discarded.
    /// </summary>
    [Fact]
    public void GalvanicsChargeAccumulatesFromARealAttack()
    {
        var galvanic = TestPaths.LoadStore<PrefixDefinition>("prefixes").GetById("prefix.galvanic");
        var h = Build(new[] { galvanic.Gauge! });

        foreach (var feed in galvanic.Gauge!.Feeds)
            h.Engine.Attach(feed, galvanic.Id);

        StartFight(h);
        h.Encounter.Attack();

        // 1.2 × the 5 stamina the attack cost (`scales_with: "amount"`).
        Assert.Equal(6.0, h.Gauges.Current("Charge"), 3);

        // …and nothing about it is Unhandled any more.
        Assert.DoesNotContain(h.Engine.Unhandled, u => u.Kind == RuleVocabulary.GrantResource);
    }

    /// <summary>A gauge that only ever climbs is a resource, not a meter. Decay starts after the
    /// authored grace period and not one tick before it.</summary>
    [Fact]
    public void AGaugeDecaysOnlyAfterItsGracePeriodLapses()
    {
        var gauge = new GaugeDefinition
        {
            Name = "Charge", Max = 100, DecayPerTick = 0.5, DecayGraceTicks = 60,
        };
        var pool = new GaugePool(gauge);

        pool.Add(50, tick: 0);

        pool.Advance(60);
        Assert.Equal(50, pool.Current, 3);      // grace covers exactly 60 ticks

        pool.Advance(80);
        Assert.Equal(40, pool.Current, 3);      // 20 ticks × 0.5

        pool.Add(10, tick: 80);                 // gaining restarts the grace period
        pool.Advance(120);
        Assert.Equal(50, pool.Current, 3);
    }

    /// <summary>
    /// Exploding Kneecaps' Guard expression, end to end: a real block, a real detonation, and an
    /// enemy that actually loses health for it.
    /// </summary>
    [Fact]
    public void ExplodingKneecapsDetonatesAgainstTheAttackerForRealDamage()
    {
        var kneecaps = TestPaths.LoadStore<SuffixDefinition>("suffixes").GetById("suffix.exploding_kneecaps");
        var guard = kneecaps.Expressions.Single(e => e.Channel == ExpressionChannel.Guard);

        var h = Build();
        h.Engine.Attach(guard.Rule, kneecaps.Id);
        StartFight(h);

        var raider = h.Encounter.Enemies[0];
        var before = raider.Health.Current;

        h.Tick.Advance(8);
        h.Encounter.Block();
        h.Tick.Advance(8);

        Assert.True(raider.Health.Current < before,
            "blocking should have detonated the blast against the attacker");
    }

    // --- The chain must survive a handler ---------------------------------------------------

    /// <summary>
    /// <b>The load-bearing invariant of E3c.</b> A handler-raised event carries the chain, so a
    /// proc's proc is depth 2 and stops there. If a handler dropped the context the chain would
    /// restart at 0 and the budget would bound nothing at all — the failure this test exists to
    /// make impossible to reintroduce quietly.
    /// </summary>
    [Fact]
    public void AProcsProcInheritsTheChainAndStopsAtTheDepthBudget()
    {
        var h = Build();

        // Each rule's damage raises DamageDealt, which is what the next one listens for.
        h.Engine.Attach(Rule("first", GameEvents.MoveExecuted, RuleVocabulary.Damage, 3,
            target: EffectTarget.AllEnemies), "test.first");
        h.Engine.Attach(Rule("second", GameEvents.DamageDealt, RuleVocabulary.Damage, 2,
            target: EffectTarget.AllEnemies), "test.second");
        h.Engine.Attach(Rule("third", GameEvents.DamageDealt, RuleVocabulary.Damage, 1,
            target: EffectTarget.AllEnemies), "test.third");

        StartFight(h);
        h.Encounter.Attack();
        h.Tick.Advance(12);   // the player's swing only; the enemy's lands at 16

        var chained = h.Engine.Fired.Where(f => f.Source.StartsWith("test.", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(chained);

        // The proof of inheritance: one chain id carries BOTH generations. If a handler dropped
        // the context, the depth-2 effects would appear under a fresh chain at depth 1 instead —
        // which reads as working right up until nothing ever stops.
        var byChain = chained.GroupBy(f => f.Context.ChainId).ToList();
        Assert.Contains(byChain, g =>
            g.Any(f => f.Context.Depth == 1) && g.Any(f => f.Context.Depth == 2));

        // And the budget holds everywhere: a proc's proc is the last generation.
        Assert.DoesNotContain(chained, f => f.Context.Depth > ProcSafety.MaxDepth);
        Assert.Empty(h.Engine.Aborted);
    }

    /// <summary>
    /// Proc-safety rule 4. A twenty-second Poison ticking every half second would otherwise proc
    /// every rule in the build dozens of times from a single application — the cheapest infinite
    /// engine in the design, closed by one flag.
    /// </summary>
    [Fact]
    public void AnAilmentTickCannotProcAnything()
    {
        var h = Build();
        h.Engine.Attach(Rule("leech", GameEvents.DamageTaken, RuleVocabulary.Heal, 5,
            target: EffectTarget.Self), "test.leech");

        StartFight(h);

        var raider = h.Encounter.Enemies[0];
        h.Encounter.ApplyStatus(raider, "status.bleed", CombatEncounter.SelfId, magnitude: 4);
        h.Tick.Advance(200);

        Assert.Contains(h.Events, e => e.Kind == GameEvents.DamageTaken && e.HasTag("ailment"));
        Assert.DoesNotContain(h.Engine.Fired, f => f.Trigger.HasTag("ailment"));
    }

    // --- The handlers ------------------------------------------------------------------------

    [Fact]
    public void DamageActsOnTheSelectedTargetAndNotTheOther()
    {
        var h = Build();
        h.Engine.Attach(Rule("riposte", GameEvents.DamageTaken, RuleVocabulary.Damage, 7,
            target: EffectTarget.TriggerTarget), "test.riposte");

        StartFight(h);
        var raider = h.Encounter.Enemies[0];
        var playerBefore = h.Encounter.Player.Health.Current;
        var raiderBefore = raider.Health.Current;

        h.Tick.Advance(20);   // the enemy lands a hit; DamageTaken targets the attacker

        Assert.True(h.Encounter.Player.Health.Current < playerBefore, "the enemy's hit should land");
        Assert.True(raider.Health.Current < raiderBefore, "the riposte should reach the attacker");
    }

    [Fact]
    public void HealRestoresTheSelectedTarget()
    {
        var h = Build();
        h.Engine.Attach(Rule("mend", GameEvents.DamageTaken, RuleVocabulary.Heal, 6,
            target: EffectTarget.Self), "test.mend");

        StartFight(h);
        h.Encounter.Player.Health.Reduce(40);
        var wounded = h.Encounter.Player.Health.Current;

        h.Tick.Advance(20);

        Assert.True(h.Encounter.Player.Health.Current > wounded - 40 + 6 - 1,
            "the mend should have restored health on top of whatever the hit took");
        Assert.Contains(h.Events, e => e.Kind == GameEvents.Healed);
    }

    [Fact]
    public void ApplyStatusLandsTheAuthoredStatus()
    {
        var h = Build();
        h.Engine.Attach(Rule("chill", GameEvents.MoveExecuted, RuleVocabulary.ApplyStatus,
            amount: 3, text: "status.chill", target: EffectTarget.TriggerTarget), "test.chill");

        StartFight(h);
        h.Encounter.Attack();
        h.Tick.Advance(20);

        Assert.True(h.Encounter.Statuses!.Has(h.Encounter.Enemies[0], "status.chill"));
    }

    /// <summary>A status applied by a proc must carry the chain, or the <c>StatusApplied</c> it
    /// raises restarts the budget at zero.</summary>
    [Fact]
    public void AProcAppliedStatusCarriesTheChainOntoStatusApplied()
    {
        var h = Build();
        h.Engine.Attach(Rule("chill", GameEvents.MoveExecuted, RuleVocabulary.ApplyStatus,
            amount: 3, text: "status.chill", target: EffectTarget.TriggerTarget), "test.chill");

        StartFight(h);
        h.Encounter.Attack();
        h.Tick.Advance(20);

        var applied = h.Events.First(e => e.Kind == GameEvents.StatusApplied);
        Assert.NotNull(applied.ChainId);
        Assert.Equal(1, applied.Depth);
    }

    [Fact]
    public void GrantResourceFillsAGaugeAndFallsBackToAPool()
    {
        var h = Build(new[] { new GaugeDefinition { Name = "Momentum", Max = 100 } });
        h.Engine.Attach(Rule("build", GameEvents.MoveExecuted, RuleVocabulary.GrantResource, 12, "Momentum"), "test.build");
        h.Engine.Attach(Rule("wind", GameEvents.MoveExecuted, RuleVocabulary.GrantResource, 9, "Stamina"), "test.wind");

        StartFight(h);
        var staminaBefore = h.Encounter.Player.Stamina.Current;
        h.Encounter.Attack();
        h.Tick.Advance(12);

        Assert.Equal(12, h.Gauges.Current("Momentum"), 3);

        // The attack cost 5; the grant put 9 back, so the pool is up on net.
        Assert.True(h.Encounter.Player.Stamina.Current > staminaBefore - 5);
    }

    [Fact]
    public void AreaDamageReachesEveryLivingEnemy()
    {
        var h = Build();
        h.Engine.Attach(Rule("blast", GameEvents.MoveExecuted, RuleVocabulary.AreaDamage, 5), "test.blast");

        h.Encounter.Start(
            Player(hp: 100, attrs: Attrs(con: 5), stamina: 50),
            new[]
            {
                Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash"),
                Enemy("Skirmisher", 50, Attrs(str: 6), "ability.goblin_slash"),
            });

        var before = h.Encounter.Enemies.Select(e => e.Health.Current).ToList();
        h.Encounter.Attack();
        h.Tick.Advance(12);

        Assert.All(h.Encounter.Enemies.Zip(before),
            pair => Assert.True(pair.First.Health.Current < pair.Second, $"{pair.First.Name} should have been caught"));
    }

    [Fact]
    public void InterruptCutsACommittedAction()
    {
        var h = Build();
        h.Engine.Attach(Rule("stagger", GameEvents.ActionTelegraphed, RuleVocabulary.Interrupt,
            target: EffectTarget.TriggerSource), "test.stagger");

        StartFight(h);
        h.Tick.Advance(2);

        Assert.Contains(h.Events, e => e.Kind == GameEvents.ActionInterrupted);
        Assert.Null(h.Encounter.ActionOf(h.Encounter.Enemies[0]));
    }

    /// <summary>An effect that fires on the killing blow must not land on a corpse — the
    /// resolver filters the dead rather than each handler remembering to.</summary>
    [Fact]
    public void EffectsSkipDeadTargets()
    {
        var h = Build();
        h.Engine.Attach(Rule("finisher", GameEvents.MoveExecuted, RuleVocabulary.Damage, 500,
            target: EffectTarget.AllEnemies), "test.finisher");

        StartFight(h, enemyHp: 20);
        h.Encounter.Attack();
        h.Tick.Advance(20);

        Assert.False(h.Encounter.Enemies[0].IsAlive);
        Assert.False(h.Encounter.IsActive);   // the encounter ended rather than looping on a corpse
    }

    /// <summary>
    /// A published event's tags are a snapshot, not a live view.
    ///
    /// <para>Combat hands one <c>ActionInFlight</c> tag set to every event an action raises, and
    /// resolution later adds <c>blocked</c>/<c>critical</c> to it. Because a <see cref="GameEvent"/>
    /// holds the reference, mutating that set retroactively rewrote events published before any
    /// of it was true — invisible to live matching on a synchronous bus, and visible to
    /// everything that records events. Found by rendering a fight and reading the trace.</para>
    /// </summary>
    [Fact]
    public void AnEventsTagsCannotBeRewrittenAfterItIsPublished()
    {
        var h = Build();
        StartFight(h);

        h.Tick.Advance(8);
        h.Encounter.Block();
        h.Tick.Advance(20);

        var telegraphed = h.Events.First(e => e.Kind == GameEvents.ActionTelegraphed);
        Assert.False(telegraphed.HasTag("blocked"),
            "the telegraph happened before the block; its tags must not have acquired one");
        Assert.Contains(h.Events, e => e.Kind == GameEvents.Blocked && e.HasTag("blocked"));
    }

    /// <summary>Effect kinds belonging to systems that do not exist stay visibly inert, which is
    /// the entire job of the Unhandled list (DECISIONS D23).</summary>
    [Fact]
    public void EffectsForUnbuiltSystemsStillLandInUnhandled()
    {
        var h = Build();
        h.Engine.Attach(Rule("summon", GameEvents.MoveExecuted, RuleVocabulary.SpawnEntity, 1, "entity.wisp"), "test.summon");

        StartFight(h);
        h.Encounter.Attack();
        h.Tick.Advance(12);

        Assert.Contains(h.Engine.Unhandled, u => u.Kind == RuleVocabulary.SpawnEntity);
    }
}
