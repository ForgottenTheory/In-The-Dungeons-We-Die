using Dungeons.Events;
using Dungeons.Randomness;
using Dungeons.Rules;
using Xunit;

namespace Dungeons.Tests.Rules;

/// <summary>
/// The event bus and the declarative rule interpreter.
///
/// <para>Everything a Prefix or Suffix does routes through here, so these tests are about the
/// properties the whole content design depends on: rules are pure data, evaluation is
/// deterministic, and content referencing systems that don't exist yet degrades visibly rather
/// than silently.</para>
/// </summary>
public class TriggerRuleEngineTests
{
    private sealed class RecordingHandler : IEffectHandler
    {
        public RecordingHandler(string kind) => Kind = kind;
        public string Kind { get; }
        public List<EffectInvocation> Calls { get; } = new();
        public void Execute(EffectInvocation invocation) => Calls.Add(invocation);
    }

    private sealed class Harness : IDisposable
    {
        public long Tick;
        public GameEventBus Bus { get; } = new();
        public TriggerRuleEngine Engine { get; }
        public RecordingHandler Damage { get; } = new(RuleVocabulary.AreaDamage);

        public Harness(int seed = 1)
        {
            Engine = new TriggerRuleEngine(Bus, new SeededRandom(seed), () => Tick);
            Engine.Register(Damage);
        }

        public void Dispose() => Engine.Dispose();
    }

    private static TriggerRule Kneecaps(int cooldown = 0, double chance = 1.0) => new()
    {
        Id = "strike",
        Event = GameEvents.DamageDealt,
        When = new[] { new ConditionSpec { Kind = RuleVocabulary.HasTag, Text = "heavy" } },
        Effect = new EffectSpec { Kind = RuleVocabulary.AreaDamage, Amount = 0.4, ScalesWith = "amount" },
        CooldownTicks = cooldown,
        Chance = chance,
    };

    private static GameEvent HeavyHit(double damage = 50) =>
        new(GameEvents.DamageDealt, Source: "player", Target: "goblin", Amount: damage,
            Tags: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "heavy", "crushing" });

    // ---- The basic contract ------------------------------------------------------------------

    [Fact]
    public void ARuleFiresWhenItsEventAndConditionsMatch()
    {
        using var harness = new Harness();
        harness.Engine.Attach(Kneecaps(), "suffix.exploding_kneecaps");

        harness.Bus.Publish(HeavyHit(50));

        var call = Assert.Single(harness.Damage.Calls);
        Assert.Equal(20, call.Magnitude);                       // 0.4 × 50
        Assert.Equal("suffix.exploding_kneecaps", call.Source);
    }

    [Fact]
    public void ARuleDoesNotFireForTheWrongEventOrAFailedCondition()
    {
        using var harness = new Harness();
        harness.Engine.Attach(Kneecaps(), "suffix.exploding_kneecaps");

        harness.Bus.Publish(new GameEvent(GameEvents.Healed, Amount: 50));                 // wrong event
        harness.Bus.Publish(new GameEvent(GameEvents.DamageDealt, Amount: 50));            // no 'heavy' tag

        Assert.Empty(harness.Damage.Calls);
    }

    [Fact]
    public void ConditionsAreAndedAndCanBeNegated()
    {
        using var harness = new Harness();
        harness.Engine.Attach(new TriggerRule
        {
            Id = "r",
            Event = GameEvents.DamageDealt,
            When = new[]
            {
                new ConditionSpec { Kind = RuleVocabulary.HasTag, Text = "heavy" },
                new ConditionSpec { Kind = RuleVocabulary.AmountAtLeast, Value = 40 },
                new ConditionSpec { Kind = RuleVocabulary.HasTag, Text = "spell", Negate = true },
            },
            Effect = new EffectSpec { Kind = RuleVocabulary.AreaDamage, Amount = 5 },
        }, "test");

        harness.Bus.Publish(HeavyHit(39));                          // amount too low
        Assert.Empty(harness.Damage.Calls);

        harness.Bus.Publish(HeavyHit(40));                          // all three pass
        Assert.Single(harness.Damage.Calls);

        harness.Bus.Publish(HeavyHit(80).With("spell"));            // negated condition blocks it
        Assert.Single(harness.Damage.Calls);
    }

    /// <summary>"40% of the damage that triggered it" without an expression parser.</summary>
    [Fact]
    public void EffectMagnitudeCanScaleWithTheTriggeringEvent()
    {
        var scaling = new EffectSpec { Kind = RuleVocabulary.Damage, Amount = 0.4, ScalesWith = "amount" };
        var flat = new EffectSpec { Kind = RuleVocabulary.Damage, Amount = 7 };

        Assert.Equal(20, scaling.Magnitude(HeavyHit(50)));
        Assert.Equal(7, flat.Magnitude(HeavyHit(50)));
    }

    // ---- Cooldowns and chance -------------------------------------------------------------------

    [Fact]
    public void CooldownsSuppressRefiringUntilTheyExpire()
    {
        using var harness = new Harness();
        harness.Engine.Attach(Kneecaps(cooldown: 60), "suffix.exploding_kneecaps");

        harness.Bus.Publish(HeavyHit());
        harness.Bus.Publish(HeavyHit());
        Assert.Single(harness.Damage.Calls);

        harness.Tick = 59;
        harness.Bus.Publish(HeavyHit());
        Assert.Single(harness.Damage.Calls);

        harness.Tick = 60;
        harness.Bus.Publish(HeavyHit());
        Assert.Equal(2, harness.Damage.Calls.Count);
    }

    /// <summary>Two sources with the same rule id must not share a cooldown.</summary>
    [Fact]
    public void CooldownsAreTrackedPerSource()
    {
        using var harness = new Harness();
        harness.Engine.Attach(Kneecaps(cooldown: 60), "suffix.a");
        harness.Engine.Attach(Kneecaps(cooldown: 60), "suffix.b");

        harness.Bus.Publish(HeavyHit());

        Assert.Equal(2, harness.Damage.Calls.Count);
    }

    [Fact]
    public void ChanceIsReproducibleFromTheSeed()
    {
        static int FiresIn(int seed)
        {
            using var harness = new Harness(seed);
            harness.Engine.Attach(Kneecaps(chance: 0.5), "test");
            for (var i = 0; i < 40; i++)
                harness.Bus.Publish(HeavyHit());
            return harness.Damage.Calls.Count;
        }

        Assert.Equal(FiresIn(7), FiresIn(7));
        Assert.InRange(FiresIn(7), 1, 39);   // actually probabilistic, not stuck on/off
    }

    // ---- Degrading visibly -----------------------------------------------------------------------

    /// <summary>
    /// Suffixes reference statuses, summons and Realm mechanics that don't exist yet. Those must
    /// be authorable now and visibly inert — not crash, and not silently succeed.
    /// </summary>
    [Fact]
    public void EffectsWithNoRegisteredHandlerAreRecordedAsUnhandled()
    {
        using var harness = new Harness();
        harness.Engine.Attach(new TriggerRule
        {
            Id = "r",
            Event = GameEvents.Killed,
            Effect = new EffectSpec { Kind = RuleVocabulary.SpawnEntity, Text = "entity.swarmling", Amount = 2 },
        }, "prefix.infested");

        harness.Bus.Publish(new GameEvent(GameEvents.Killed, Source: "player"));

        var unhandled = Assert.Single(harness.Engine.Unhandled);
        Assert.Equal(RuleVocabulary.SpawnEntity, unhandled.Kind);
        Assert.Single(harness.Engine.Fired);
    }

    [Fact]
    public void AnUnknownConditionKindThrowsRatherThanSilentlyPassing()
    {
        Assert.Throws<NotSupportedException>(() =>
            TriggerRuleEngine.Evaluate(new ConditionSpec { Kind = "vibes" }, HeavyHit()));
    }

    // ---- Bus behaviour ------------------------------------------------------------------------------

    /// <summary>
    /// Synchronous, ordered delivery is a determinism requirement, not a convenience — a queued
    /// or async bus would make combat outcomes depend on scheduling.
    /// </summary>
    [Fact]
    public void HandlersRunInSubscriptionOrderAndSynchronously()
    {
        var bus = new GameEventBus();
        var order = new List<int>();

        bus.Subscribe(_ => order.Add(1));
        bus.Subscribe(_ => order.Add(2));
        bus.Subscribe(GameEvents.Killed, _ => order.Add(3));

        bus.Publish(new GameEvent(GameEvents.Killed));

        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    /// <summary>A rule that causes an event must not re-enter the handler mid-flight.</summary>
    [Fact]
    public void EventsRaisedByHandlersAreQueuedNotReentrant()
    {
        var bus = new GameEventBus();
        var order = new List<string>();

        bus.Subscribe(e =>
        {
            order.Add("enter:" + e.Kind);
            if (e.Kind == GameEvents.Killed)
                bus.Publish(new GameEvent(GameEvents.ItemReceived));
            order.Add("exit:" + e.Kind);
        });

        bus.Publish(new GameEvent(GameEvents.Killed));

        Assert.Equal(
            new[] { "enter:Killed", "exit:Killed", "enter:ItemReceived", "exit:ItemReceived" },
            order);
    }

    [Fact]
    public void UnsubscribingDuringDeliveryIsSafe()
    {
        var bus = new GameEventBus();
        var hits = 0;
        IDisposable? subscription = null;
        subscription = bus.Subscribe(_ => { hits++; subscription!.Dispose(); });

        bus.Publish(new GameEvent(GameEvents.Killed));
        bus.Publish(new GameEvent(GameEvents.Killed));

        Assert.Equal(1, hits);
    }

    [Fact]
    public void DetachingRulesStopsThemFiring()
    {
        using var harness = new Harness();
        harness.Engine.Attach(Kneecaps(), "test");
        harness.Engine.DetachAll();

        harness.Bus.Publish(HeavyHit());

        Assert.Empty(harness.Damage.Calls);
    }

    // ---- Vocabulary integrity -----------------------------------------------------------------------

    /// <summary>Every condition kind in the vocabulary must actually evaluate — an entry the
    /// interpreter doesn't handle would pass validation and then throw at runtime.</summary>
    [Fact]
    public void EveryDeclaredConditionKindIsImplemented()
    {
        foreach (var kind in RuleVocabulary.Conditions)
        {
            var exception = Record.Exception(() =>
                TriggerRuleEngine.Evaluate(new ConditionSpec { Kind = kind, Text = "x", Value = 1 }, HeavyHit()));

            Assert.Null(exception);
        }
    }

    [Fact]
    public void TheEventVocabularyIsCompleteAndUnique()
    {
        var declared = typeof(GameEvents).GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(declared.Count, declared.Distinct().Count());
        foreach (var kind in declared)
            Assert.Contains(kind, GameEvents.All);
        Assert.Equal(declared.Count, GameEvents.All.Count);
    }
}
