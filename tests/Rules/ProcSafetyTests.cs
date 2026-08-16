using Dungeons.Events;
using Dungeons.Randomness;
using Dungeons.Rules;
using Xunit;

namespace Dungeons.Tests.Rules;

/// <summary>
/// E3a — the effect vocabulary upgrade and proc safety (docs/effect-foundation.md §3.4, §6).
///
/// <para>The brief's fear, stated exactly: <i>thorns → counts as hit → triggers Shock → Shock
/// triggers retaliation → retaliation triggers thorns → game achieves nuclear fusion.</i> These
/// tests are the proof that it terminates, and they test the <b>mechanisms</b> rather than any
/// particular content, because the content that will exercise them does not exist yet.</para>
/// </summary>
public class ProcSafetyTests
{
    private sealed class Recorder : IEffectHandler
    {
        public Recorder(string kind, GameEventBus? bus = null, string? echo = null)
        {
            Kind = kind;
            _bus = bus;
            _echo = echo;
        }

        private readonly GameEventBus? _bus;
        private readonly string? _echo;

        public string Kind { get; }
        public List<EffectInvocation> Calls { get; } = new();

        public void Execute(EffectInvocation invocation)
        {
            Calls.Add(invocation);

            // A handler that raises another event MUST propagate the chain, or the budget resets
            // and the whole safety model is decorative.
            if (_bus is not null && _echo is not null)
                _bus.Publish(new GameEvent(
                    _echo,
                    Source: "self",
                    Target: "enemy",
                    ChainId: invocation.Context.ChainId,
                    Depth: invocation.Context.Depth));
        }
    }

    private static (GameEventBus bus, TriggerRuleEngine engine, long tick) Build()
    {
        var bus = new GameEventBus();
        return (bus, new TriggerRuleEngine(bus, new SeededRandom(1), () => 0), 0);
    }

    private static TriggerRule Rule(string id, string @event, params EffectSpec[] effects) => new()
    {
        Id = id,
        Event = @event,
        Effects = effects,
        Proc = new ProcRules { OncePerChain = false },
    };

    // --- effects[] ----------------------------------------------------------

    [Fact]
    public void OneChanceRollFiresEveryEffect()
    {
        // The reason effects[] exists. Two rules with duplicated conditions would roll
        // independently — "25% chance to Shock AND restore Stamina" is a different mechanic from
        // "25% Shock, separately 25% Stamina".
        var (bus, engine, _) = Build();
        var shock = new Recorder("applyStatus");
        var stamina = new Recorder("grantResource");
        engine.Register(shock).Register(stamina);

        engine.Attach(Rule("riposte", GameEvents.Blocked,
            new EffectSpec { Kind = "applyStatus", Text = "status.shock" },
            new EffectSpec { Kind = "grantResource", Text = "Stamina", Amount = 8 }), "affix.test");

        bus.Publish(new GameEvent(GameEvents.Blocked, "self", "enemy"));

        Assert.Single(shock.Calls);
        Assert.Single(stamina.Calls);
    }

    [Fact]
    public void TheLegacySingleEffectFormStillWorks()
    {
        // Every shipped prefix and suffix authors `effect`, not `effects`. Both shapes stay valid
        // — a content migration was never worth it for a field rename.
        var (bus, engine, _) = Build();
        var handler = new Recorder("damage");
        engine.Register(handler);

        engine.Attach(new TriggerRule
        {
            Id = "legacy",
            Event = GameEvents.DamageDealt,
            Effect = new EffectSpec { Kind = "damage", Amount = 5 },
        }, "prefix.test");

        bus.Publish(new GameEvent(GameEvents.DamageDealt, "self", "enemy"));

        Assert.Single(handler.Calls);
    }

    // --- Target selectors ---------------------------------------------------

    [Fact]
    public void ARuleCarriesItsTargetSelectorToTheHandler()
    {
        // Before E3, areaDamage had no way to say who it hit. Exploding Kneecaps' Guard
        // expression detonates against the ATTACKER; its Surge expression detonates around
        // YOU — same effect kind, and the target is the difference.
        var (bus, engine, _) = Build();
        var handler = new Recorder("areaDamage");
        engine.Register(handler);

        engine.Attach(new TriggerRule
        {
            Id = "guard",
            Event = GameEvents.Blocked,
            Target = EffectTarget.TriggerSource,
            Effect = new EffectSpec { Kind = "areaDamage", Amount = 10 },
        }, "suffix.kneecaps");

        bus.Publish(new GameEvent(GameEvents.Blocked, "self", "Raider"));

        Assert.Equal(EffectTarget.TriggerSource, handler.Calls[0].Target);
    }

    // --- Depth --------------------------------------------------------------

    [Fact]
    public void AProcMayProcOnceMore_AndThenTheChainStops()
    {
        // Depth 2: hit(0) → thorns(1) → shock(2) → nothing. Depth 1 would break every two-step
        // combination in the catalog; depth 3 multiplies a surface nobody can model.
        var (bus, engine, _) = Build();

        var first = new Recorder("damage", bus, echo: GameEvents.StatusApplied);
        var second = new Recorder("grantResource", bus, echo: GameEvents.ResourceGenerated);
        var third = new Recorder("heal");
        engine.Register(first).Register(second).Register(third);

        engine.Attach(Rule("a", GameEvents.DamageDealt, new EffectSpec { Kind = "damage" }), "src");
        engine.Attach(Rule("b", GameEvents.StatusApplied, new EffectSpec { Kind = "grantResource" }), "src");
        engine.Attach(Rule("c", GameEvents.ResourceGenerated, new EffectSpec { Kind = "heal" }), "src");

        bus.Publish(new GameEvent(GameEvents.DamageDealt, "self", "enemy"));

        Assert.Single(first.Calls);    // depth 1
        Assert.Single(second.Calls);   // depth 2
        Assert.Empty(third.Calls);     // depth 3 — refused
    }

    [Fact]
    public void AnEventFlaggedCanTriggerFalseMatchesNothing()
    {
        // Retaliation damage and ailment ticks set this. Between them, those two cases account
        // for most of the recursion the design has to survive — and this rule stops both before
        // the depth ceiling is ever consulted.
        var (bus, engine, _) = Build();
        var handler = new Recorder("damage");
        engine.Register(handler);

        engine.Attach(Rule("thorns", GameEvents.DamageDealt, new EffectSpec { Kind = "damage" }), "src");

        bus.Publish(new GameEvent(GameEvents.DamageDealt, "self", "enemy", CanTrigger: false));

        Assert.Empty(handler.Calls);
    }

    [Fact]
    public void TheFusionChainTerminates()
    {
        // The brief's example, built as literally as the mechanisms allow: a rule that reacts to
        // damage by dealing damage. Without proc safety this is an infinite loop.
        var (bus, engine, _) = Build();
        var thorns = new Recorder("damage", bus, echo: GameEvents.DamageDealt);
        engine.Register(thorns);

        engine.Attach(new TriggerRule
        {
            Id = "thorns",
            Event = GameEvents.DamageDealt,
            Effect = new EffectSpec { Kind = "damage", Amount = 4 },
            Proc = new ProcRules { OncePerChain = false }, // deliberately the UNSAFE setting
        }, "affix.thorns");

        bus.Publish(new GameEvent(GameEvents.DamageDealt, "Raider", "self"));

        // Bounded by depth alone, even with once-per-chain switched off.
        Assert.Equal(ProcSafety.MaxDepth, thorns.Calls.Count);
        Assert.Empty(engine.Aborted);
    }

    // --- Once per chain -----------------------------------------------------

    [Fact]
    public void OncePerChainKillsPingPong_EvenInsideTheDepthBudget()
    {
        var (bus, engine, _) = Build();
        var handler = new Recorder("damage", bus, echo: GameEvents.DamageDealt);
        engine.Register(handler);

        // The default. Content opts INTO risk; it never opts out of safety by omission.
        engine.Attach(new TriggerRule
        {
            Id = "thorns",
            Event = GameEvents.DamageDealt,
            Effect = new EffectSpec { Kind = "damage", Amount = 4 },
        }, "affix.thorns");

        bus.Publish(new GameEvent(GameEvents.DamageDealt, "Raider", "self"));

        Assert.Single(handler.Calls); // fired once, not MaxDepth times
    }

    [Fact]
    public void OncePerChainIsPerChain_NotForever()
    {
        var (bus, engine, _) = Build();
        var handler = new Recorder("damage");
        engine.Register(handler);

        engine.Attach(new TriggerRule
        {
            Id = "thorns",
            Event = GameEvents.DamageDealt,
            Effect = new EffectSpec { Kind = "damage", Amount = 4 },
        }, "affix.thorns");

        // Two separate incoming hits — two separate chains.
        bus.Publish(new GameEvent(GameEvents.DamageDealt, "Raider", "self"));
        bus.Publish(new GameEvent(GameEvents.DamageDealt, "Raider", "self"));

        Assert.Equal(2, handler.Calls.Count);
    }

    // --- Chain identity -----------------------------------------------------

    [Fact]
    public void ChainIdsAreSequential_BecauseTheSimulationMustReplayFromASeed()
    {
        var (bus, engine, _) = Build();
        var handler = new Recorder("damage");
        engine.Register(handler);
        engine.Attach(Rule("r", GameEvents.DamageDealt, new EffectSpec { Kind = "damage" }), "src");

        bus.Publish(new GameEvent(GameEvents.DamageDealt, "self", "a"));
        bus.Publish(new GameEvent(GameEvents.DamageDealt, "self", "b"));

        Assert.Equal("chain.1", handler.Calls[0].Context.ChainId);
        Assert.Equal("chain.2", handler.Calls[1].Context.ChainId);
    }

    [Fact]
    public void TheContextRemembersWhoStartedTheChainAndWhoFiredThisEffect()
    {
        var (bus, engine, _) = Build();
        var handler = new Recorder("damage");
        engine.Register(handler);
        engine.Attach(Rule("r", GameEvents.DamageDealt, new EffectSpec { Kind = "damage" }), "affix.thorns");

        bus.Publish(new GameEvent(GameEvents.DamageDealt, "Goblin Brute", "self"));

        var context = handler.Calls[0].Context;
        Assert.Equal("Goblin Brute", context.OriginSource);   // who started it
        Assert.Equal("affix.thorns", context.ImmediateSource); // whose rule fired
        Assert.Equal(1, context.Depth);
    }

    // --- Internal cooldowns -------------------------------------------------

    [Fact]
    public void APerTargetIcdLimitsOneTargetWithoutLimitingTheOthers()
    {
        // ICDs were chosen over PoE-style proc coefficients deliberately: "once every 2s" is
        // readable in a tooltip and a proc coefficient is not.
        var bus = new GameEventBus();
        var now = 0L;
        var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => now);
        var handler = new Recorder("damage");
        engine.Register(handler);

        engine.Attach(new TriggerRule
        {
            Id = "arc",
            Event = GameEvents.DamageDealt,
            Effect = new EffectSpec { Kind = "damage" },
            Proc = new ProcRules { OncePerChain = false, IcdTicks = 40 },
        }, "affix.arc");

        bus.Publish(new GameEvent(GameEvents.DamageDealt, "self", "Raider"));
        bus.Publish(new GameEvent(GameEvents.DamageDealt, "self", "Raider"));  // same target, blocked
        bus.Publish(new GameEvent(GameEvents.DamageDealt, "self", "Brute"));   // different target, allowed

        Assert.Equal(2, handler.Calls.Count);

        now = 41;
        bus.Publish(new GameEvent(GameEvents.DamageDealt, "self", "Raider"));
        Assert.Equal(3, handler.Calls.Count);
    }

    // --- The fuse -----------------------------------------------------------

    [Fact]
    public void TheChainFuseAbortsRunawayContent()
    {
        var (bus, engine, _) = Build();
        var handler = new Recorder("damage");
        engine.Register(handler);

        // 100 rules on one event, all firing in one chain — past the 64-effect fuse.
        for (var i = 0; i < 100; i++)
            engine.Attach(Rule($"r{i}", GameEvents.DamageDealt, new EffectSpec { Kind = "damage" }), $"src{i}");

        bus.Publish(new GameEvent(GameEvents.DamageDealt, "self", "enemy"));

        Assert.Equal(ProcSafety.MaxEffectsPerChain, handler.Calls.Count);
        Assert.NotEmpty(engine.Aborted);
    }

    [Fact]
    public void ShippedContentNeverTripsTheFuse()
    {
        // The fuse is a bug surface, not a balance one. If real content ever reaches it, that is
        // a defect rather than a tuning problem — so this asserts it stays theoretical.
        var bus = new GameEventBus();
        var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => 0);

        foreach (var prefix in Dungeons.Tests.TestPaths
                     .LoadStore<Dungeons.Characters.Composition.PrefixDefinition>("prefixes").GetAll())
        {
            engine.AttachAll(prefix.Rules, prefix.Id);
            if (prefix.Gauge is { } gauge)
                engine.AttachAll(gauge.Feeds, prefix.Id);
        }

        foreach (var kind in GameEvents.All)
            bus.Publish(new GameEvent(kind, "self", "enemy"));

        Assert.Empty(engine.Aborted);
    }
}
