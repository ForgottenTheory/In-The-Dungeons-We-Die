using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Events;
using Dungeons.Randomness;
using Dungeons.Rules;
using Dungeons.Simulation;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Rules;

/// <summary>
/// E3c-3 — conditions that read the world.
///
/// <para>Every condition through E3c was a pure function of the <c>GameEvent</c>, which is why
/// the evaluator could be static. "Only while the target is Chilled" and "only below 30%
/// Stamina" are not answerable from an event, and writing that state into every event instead
/// would mean every publisher guessing what every future condition might want.</para>
///
/// <para>The rule that matters most here is the failure mode: a condition the engine cannot
/// evaluate is recorded in <c>UnevaluatedConditions</c> rather than quietly returning false.
/// A rule whose condition can never pass is exactly as dead as one whose effect goes
/// nowhere.</para>
/// </summary>
public class StatefulConditionTests
{
    private static readonly MoveDefinition Strike = Move("move.strike", DamageType.Slashing, 10, 2, 8, 15, stamina: 5);
    private static readonly MoveDefinition Slash = Move("move.goblin_slash", DamageType.Slashing, 6, 8, 8, 20);

    private sealed record Harness(
        CombatEncounter Encounter, TickEngine Tick, TriggerRuleEngine Engine,
        GaugeController Gauges, List<GameEvent> Events);

    private static Harness Build(
        IEnumerable<GaugeDefinition>? gauges = null,
        IEnumerable<string>? equipped = null,
        bool withWorld = true)
    {
        var tick = new TickEngine();
        var bus = new GameEventBus();
        var events = new List<GameEvent>();
        bus.Subscribe(events.Add);

        var statuses = new StatusController(
            TestPaths.LoadStore<StatusDefinition>("statuses"), bus, () => tick.CurrentTick);
        var gaugeController = new GaugeController(gauges ?? Array.Empty<GaugeDefinition>());

        var rng = new FakeRandom(0.99);
        var encounter = new CombatEncounter(
            tick, new HitPipeline(rng), Moves(Strike, Slash), rng, bus,
            statuses, gaugeController);

        IConditionWorld? world = withWorld
            ? new CombatConditionWorld(encounter, () => equipped ?? Array.Empty<string>())
            : null;

        var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => tick.CurrentTick, world)
            .RegisterCombatHandlers(encounter, rng);

        return new Harness(encounter, tick, engine, gaugeController, events);
    }

    private static TriggerRule Gated(string id, string @event, ConditionSpec condition) => new()
    {
        Id = id,
        Event = @event,
        Target = EffectTarget.Self,
        When = new[] { condition },
        Effect = new EffectSpec { Kind = RuleVocabulary.Heal, Amount = 1 },
    };

    private static ConditionSpec Cond(string kind, string text = "", double value = 0, bool negate = false) =>
        new() { Kind = kind, Text = text, Value = value, Negate = negate };

    private static void StartFight(Harness h, int playerHp = 200, int stamina = 50) =>
        h.Encounter.Start(
            Player(hp: playerHp, attrs: Attrs(con: 5), stamina: stamina),
            new[] { Enemy("Raider", 200, Attrs(str: 6), Slash) });

    private static bool Fired(Harness h, string source) =>
        h.Engine.Fired.Any(f => f.Source == source);

    // --- The failure mode, first ------------------------------------------------------------

    /// <summary>
    /// <b>The load-bearing test.</b> Without a world a stateful condition is unanswerable, and
    /// the engine says so instead of returning false and leaving a rule mysteriously dead.
    /// </summary>
    [Fact]
    public void AConditionWithNoWorldIsRecordedRatherThanSilentlyFailing()
    {
        var h = Build(withWorld: false);
        h.Engine.Attach(Gated("gated", GameEvents.MoveExecuted, Cond(RuleVocabulary.SelfHasStatus, "status.chill")), "test.gated");

        StartFight(h);
        h.Encounter.Attack();
        h.Tick.Advance(12);

        Assert.False(Fired(h, "test.gated"));
        Assert.Contains(h.Engine.UnevaluatedConditions, u => u.Contains(RuleVocabulary.SelfHasStatus));
    }

    /// <summary>Event-only conditions never need a world, so the engine stays usable without
    /// one — which is what keeps the static evaluator honest for the roster tests.</summary>
    [Fact]
    public void EventOnlyConditionsStillEvaluateWithoutAWorld()
    {
        var h = Build(withWorld: false);
        h.Engine.Attach(Gated("plain", GameEvents.MoveExecuted, Cond(RuleVocabulary.HasTag, "melee")), "test.plain");

        StartFight(h);
        h.Encounter.Attack();
        h.Tick.Advance(12);

        Assert.True(Fired(h, "test.plain"));
        Assert.Empty(h.Engine.UnevaluatedConditions);
    }

    // --- The conditions ----------------------------------------------------------------------

    [Fact]
    public void TargetHasStatusGatesOnTheTargetsActualStatuses()
    {
        var h = Build();
        h.Engine.Attach(Gated("follow_up", GameEvents.MoveExecuted,
            Cond(RuleVocabulary.TargetHasStatus, "status.chill")), "test.follow_up");

        StartFight(h);
        h.Encounter.Attack();
        h.Tick.Advance(40);   // resolves, and the player's recovery elapses
        Assert.False(Fired(h, "test.follow_up"));

        h.Encounter.ApplyStatus(h.Encounter.Enemies[0], "status.chill", CombatEncounter.SelfId);

        // The second swing has to actually be accepted — a refused Attack() would leave no
        // player-sourced MoveExecuted and the test would pass or fail for the wrong reason.
        Assert.True(h.Encounter.Attack(), "the player should be off recovery by now");
        h.Tick.Advance(20);

        Assert.True(Fired(h, "test.follow_up"));
    }

    [Fact]
    public void SelfHasStatusReadsTheRuleOwnerRatherThanTheEventTarget()
    {
        var h = Build();
        h.Engine.Attach(Gated("desperate", GameEvents.MoveExecuted,
            Cond(RuleVocabulary.SelfHasStatus, "status.chill")), "test.desperate");

        StartFight(h);
        h.Encounter.ApplyStatus(h.Encounter.Enemies[0], "status.chill", CombatEncounter.SelfId);
        h.Encounter.Attack();
        h.Tick.Advance(12);

        // The *enemy* is chilled, not us.
        Assert.False(Fired(h, "test.desperate"));

        h.Encounter.ApplyStatus(h.Encounter.Player, "status.chill", CombatEncounter.SelfId);
        h.Encounter.Attack();
        h.Tick.Advance(30);

        Assert.True(Fired(h, "test.desperate"));
    }

    [Fact]
    public void ResourceBelowAndAboveReadTheOwnersPools()
    {
        var h = Build();
        h.Engine.Attach(Gated("winded", GameEvents.MoveExecuted,
            Cond(RuleVocabulary.ResourceBelow, "stamina", 0.5)), "test.winded");

        StartFight(h, stamina: 50);
        h.Encounter.Attack();          // costs 5 of 50 — still well above half
        h.Tick.Advance(12);
        Assert.False(Fired(h, "test.winded"));

        h.Encounter.Player.Stamina.Reduce(40);
        h.Encounter.Attack();
        h.Tick.Advance(30);
        Assert.True(Fired(h, "test.winded"));
    }

    [Fact]
    public void EquippedTagReadsWhatIsActuallyWorn()
    {
        var bare = Build(equipped: Array.Empty<string>());
        bare.Engine.Attach(Gated("shielded", GameEvents.MoveExecuted,
            Cond(RuleVocabulary.EquippedTag, "shield")), "test.shielded");
        StartFight(bare);
        bare.Encounter.Attack();
        bare.Tick.Advance(12);
        Assert.False(Fired(bare, "test.shielded"));

        var armed = Build(equipped: new[] { "shield", "heavy" });
        armed.Engine.Attach(Gated("shielded", GameEvents.MoveExecuted,
            Cond(RuleVocabulary.EquippedTag, "shield")), "test.shielded");
        StartFight(armed);
        armed.Encounter.Attack();
        armed.Tick.Advance(12);
        Assert.True(Fired(armed, "test.shielded"));
    }

    /// <summary>
    /// A lane is something the hit already knows, so this reads the <c>lane:</c> tag combat puts
    /// on the event rather than asking the world. Slashing resolves to the <c>physical</c> lane —
    /// which is the D-02 collapse showing up in content for the first time.
    /// </summary>
    [Fact]
    public void HitHasLaneReadsTheLaneCombatTaggedOnTheEvent()
    {
        var h = Build();
        h.Engine.Attach(Gated("grounded", GameEvents.MoveExecuted,
            Cond(RuleVocabulary.HitHasLane, DamageLanes.Physical)), "test.grounded");
        h.Engine.Attach(Gated("scorched", GameEvents.MoveExecuted,
            Cond(RuleVocabulary.HitHasLane, DamageLanes.Heat)), "test.scorched");

        StartFight(h);
        h.Encounter.Attack();
        h.Tick.Advance(12);

        Assert.True(Fired(h, "test.grounded"));
        Assert.False(Fired(h, "test.scorched"));

        // No world was consulted for either — a lane is event data.
        Assert.Empty(h.Engine.UnevaluatedConditions);
    }

    [Fact]
    public void GaugeAtLeastCanNameOneGaugeAmongSeveral()
    {
        var h = Build(gauges: new[]
        {
            new GaugeDefinition { Name = "Charge", Max = 100 },
            new GaugeDefinition { Name = "Momentum", Max = 100 },
        });

        h.Engine.Attach(Gated("discharge", GameEvents.MoveExecuted,
            Cond(RuleVocabulary.GaugeAtLeast, "Charge", 0.6)), "test.discharge");

        StartFight(h);
        h.Gauges.Add("Momentum", 90, h.Tick.CurrentTick);   // the *other* meter is full
        h.Encounter.Attack();
        h.Tick.Advance(12);

        Assert.False(Fired(h, "test.discharge"));

        h.Gauges.Add("Charge", 70, h.Tick.CurrentTick);
        h.Encounter.Attack();
        h.Tick.Advance(30);

        Assert.True(Fired(h, "test.discharge"));
    }

    /// <summary>An unnamed <c>gaugeAtLeast</c> keeps reading the event's fullest-meter value, so
    /// the seven authored conditions that predate the naming keep working untouched.</summary>
    [Fact]
    public void AnUnnamedGaugeConditionStillReadsTheEventAndNeedsNoWorld()
    {
        var h = Build(gauges: new[] { new GaugeDefinition { Name = "Charge", Max = 100 } }, withWorld: false);
        h.Engine.Attach(Gated("surge", GameEvents.MoveExecuted,
            Cond(RuleVocabulary.GaugeAtLeast, value: 0.6)), "test.surge");

        StartFight(h);
        h.Gauges.Add("Charge", 70, h.Tick.CurrentTick);
        h.Encounter.Attack();
        h.Tick.Advance(12);

        Assert.True(Fired(h, "test.surge"));
        Assert.Empty(h.Engine.UnevaluatedConditions);
    }

    /// <summary>A *named* gauge without a world would have to fall back to the fullest meter,
    /// which is a plausible wrong answer — so it refuses instead.</summary>
    [Fact]
    public void ANamedGaugeConditionWithoutAWorldIsRecordedRatherThanGuessed()
    {
        var h = Build(gauges: new[] { new GaugeDefinition { Name = "Charge", Max = 100 } }, withWorld: false);
        h.Engine.Attach(Gated("discharge", GameEvents.MoveExecuted,
            Cond(RuleVocabulary.GaugeAtLeast, "Charge", 0.6)), "test.discharge");

        StartFight(h);
        h.Gauges.Add("Charge", 70, h.Tick.CurrentTick);
        h.Encounter.Attack();
        h.Tick.Advance(12);

        Assert.False(Fired(h, "test.discharge"));
        Assert.Contains(h.Engine.UnevaluatedConditions, u => u.Contains(RuleVocabulary.GaugeAtLeast));
    }

    /// <summary>`negate` covers "has" and "lacks" with one kind, and must keep doing so for the
    /// stateful ones — otherwise the vocabulary doubles.</summary>
    [Fact]
    public void NegationWorksOnStatefulConditionsToo()
    {
        var h = Build();
        h.Engine.Attach(Gated("unchilled", GameEvents.MoveExecuted,
            Cond(RuleVocabulary.TargetHasStatus, "status.chill", negate: true)), "test.unchilled");

        StartFight(h);
        h.Encounter.Attack();
        h.Tick.Advance(12);
        Assert.True(Fired(h, "test.unchilled"));
    }

    /// <summary>Every condition kind code knows is in the validated vocabulary, or content that
    /// uses it would fail to load.</summary>
    [Fact]
    public void EveryWorldConditionIsInTheValidatedVocabulary()
    {
        foreach (var kind in RuleVocabulary.WorldConditions)
            Assert.Contains(kind, RuleVocabulary.Conditions);

        Assert.Contains(RuleVocabulary.HitHasLane, RuleVocabulary.Conditions);

        // …and `hitHasLane` is deliberately NOT a world condition: a hit knows its own lanes.
        Assert.DoesNotContain(RuleVocabulary.HitHasLane, RuleVocabulary.WorldConditions);
    }
}
