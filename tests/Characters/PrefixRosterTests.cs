using Dungeons.Characters.Composition;
using Dungeons.Content;
using Dungeons.Events;
using Dungeons.Randomness;
using Dungeons.Rules;
using Xunit;

namespace Dungeons.Tests.Characters;

/// <summary>
/// The Prefix roster (docs/classes.md §4).
///
/// <para>The claim under test is the compositional one: <b>each Prefix is authored once,
/// against events, and adapts to any Base without anyone authoring the combination.</b> If
/// that holds, the roster costs 25 mechanics; if it leaks, it costs 375.</para>
/// </summary>
public class PrefixRosterTests
{
    private static DataStore<PrefixDefinition> Prefixes() =>
        TestPaths.LoadStore<PrefixDefinition>("prefixes");

    private static DataStore<BaseClassDefinition> Bases() =>
        TestPaths.LoadStore<BaseClassDefinition>("classes");

    [Fact]
    public void AllTwentyFivePrefixesShip()
    {
        var prefixes = Prefixes();

        foreach (var id in new[]
        {
            "prefix.trickster", "prefix.galvanic", "prefix.explosive", "prefix.venomous",
            "prefix.gravitic", "prefix.vampiric", "prefix.clockwork", "prefix.spectral",
            "prefix.sylvan", "prefix.abyssal", "prefix.radiant", "prefix.seismic",
            "prefix.chrono", "prefix.psionic", "prefix.crystalline", "prefix.bureaucratic",
            "prefix.recursive", "prefix.dissonant", "prefix.parasitic", "prefix.infested",
            "prefix.masochistic", "prefix.mnemonic", "prefix.biomechanical", "prefix.glitched",
            "prefix.quantum",
        })
        {
            Assert.True(prefixes.Contains(id), $"missing Prefix '{id}'.");
        }

        Assert.Equal(25, prefixes.Count);
    }

    // ---- The compositional rule ---------------------------------------------------------------

    /// <summary>
    /// The rule the whole roster's tractability rests on. A single "just this once" exception
    /// here is how 25 authored mechanics quietly becomes 375.
    /// </summary>
    [Fact]
    public void NoPrefixNamesABase()
    {
        var baseIds = Bases().GetAll().Select(b => b.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseNames = Bases().GetAll().Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var prefix in Prefixes().GetAll())
        {
            var strings = prefix.Tags
                .Concat(prefix.Rules.Select(r => r.Effect.Text))
                .Concat(prefix.Rules.SelectMany(r => r.When.Select(c => c.Text)))
                .Where(s => !string.IsNullOrEmpty(s));

            foreach (var text in strings)
            {
                Assert.DoesNotContain(text, baseIds);
                Assert.DoesNotContain(text, baseNames);
            }
        }
    }

    /// <summary>Every prefix hook must listen for a real event, or it can never fire.</summary>
    [Fact]
    public void EveryHookListensForAKnownEvent()
    {
        foreach (var prefix in Prefixes().GetAll())
        {
            var hooks = prefix.Rules.Concat(prefix.Gauge?.Feeds ?? Enumerable.Empty<TriggerRule>());

            foreach (var rule in hooks)
                Assert.Contains(rule.Event, GameEvents.All);
        }
    }

    [Fact]
    public void EveryPrefixStatesOneMechanicAndActuallyDoesSomething()
    {
        foreach (var prefix in Prefixes().GetAll())
        {
            Assert.False(string.IsNullOrWhiteSpace(prefix.Mechanic), $"{prefix.Id} states no mechanic.");
            Assert.True(
                prefix.Rules.Count > 0 || prefix.Gauge is not null || prefix.Modifiers.Count > 0,
                $"{prefix.Id} does nothing.");
        }
    }

    /// <summary>"One recognizable mechanic, not ten little bonuses." Four hooks is generous;
    /// beyond that a prefix has stopped being one idea.</summary>
    [Fact]
    public void NoPrefixSprawlsIntoManySeparateEffects()
    {
        foreach (var prefix in Prefixes().GetAll())
            Assert.True(prefix.Rules.Count <= 4, $"{prefix.Id} has {prefix.Rules.Count} rules — it is becoming a grab bag.");
    }

    /// <summary>A build runs at most one Base gauge and one Prefix gauge. More than two meters
    /// stops being readable, which is the failure the single-Base structure exists to avoid.</summary>
    [Fact]
    public void AtMostOneGaugePerPrefix()
    {
        var withGauges = Prefixes().GetAll().Count(p => p.Gauge is not null);

        Assert.InRange(withGauges, 4, 12);
        foreach (var prefix in Prefixes().GetAll().Where(p => p.Gauge is not null))
            Assert.False(string.IsNullOrWhiteSpace(prefix.Gauge!.Name), $"{prefix.Id} has an unnamed gauge.");
    }

    // ---- The adaptation actually works -------------------------------------------------------------

    /// <summary>
    /// The worked example, executed rather than asserted in prose: Galvanic hooks
    /// <c>ResourceSpent</c> and nothing else, so every Base charges it by doing whatever that
    /// Base does — swinging, casting, blocking, bleeding.
    /// </summary>
    [Theory]
    [InlineData("stamina", "melee")]   // a Juggernaut swinging
    [InlineData("mana", "spell")]      // a Wizard releasing a hold
    [InlineData("guard", "defensive")] // a Bastion absorbing
    [InlineData("health", "pact")]     // a Vitalist paying in blood
    public void GalvanicChargesFromAnyKindOfResourceSpend(string resource, string moveTag)
    {
        var galvanic = Prefixes().GetById("prefix.galvanic");
        var feed = Assert.Single(galvanic.Gauge!.Feeds);

        var bus = new GameEventBus();
        using var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => 0);
        engine.Attach(feed, galvanic.Id);

        bus.Publish(new GameEvent(
            GameEvents.ResourceSpent, Source: "player", Amount: 10,
            Tags: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { resource, moveTag }));

        var fired = Assert.Single(engine.Fired);
        Assert.Equal("Charge", fired.Effect.Text);
        Assert.Equal(12, fired.Magnitude);   // 1.2 × 10 spent, whatever the resource was
    }

    /// <summary>Seismic keys off commitment (long windup), not off being a martial — so a
    /// Wizard's held spell qualifies exactly as a Juggernaut's overhead does.</summary>
    [Fact]
    public void SeismicRewardsCommitmentRegardlessOfBuild()
    {
        var seismic = Prefixes().GetById("prefix.seismic");
        var rule = Assert.Single(seismic.Rules);

        var committed = new GameEvent(GameEvents.MoveExecuted, Amount: 40,
            Values: new Dictionary<string, double> { ["windup_ticks"] = 30 });
        var quick = new GameEvent(GameEvents.MoveExecuted, Amount: 40,
            Values: new Dictionary<string, double> { ["windup_ticks"] = 6 });

        Assert.True(rule.When.All(c => TriggerRuleEngine.Evaluate(c, committed)));
        Assert.False(rule.When.All(c => TriggerRuleEngine.Evaluate(c, quick)));
    }

    /// <summary>Masochistic's meter is fed by suffering and destroyed by comfort — both
    /// directions have to be wired, or it is just a damage-taken bonus.</summary>
    [Fact]
    public void MasochisticGainsFromHarmAndLosesToHealing()
    {
        var gauge = Prefixes().GetById("prefix.masochistic").Gauge!;

        Assert.Contains(gauge.Feeds, f => f.Event == GameEvents.DamageTaken && f.Effect.Kind == RuleVocabulary.GrantResource);
        Assert.Contains(gauge.Feeds, f => f.Event == GameEvents.Healed && f.Effect.Kind == RuleVocabulary.DrainResource);
    }

    /// <summary>
    /// Content referencing systems that don't exist yet must be visibly inert, not silently
    /// missing — several prefixes summon, apply statuses and reposition today.
    /// </summary>
    [Fact]
    public void PrefixesReferencingUnbuiltSystemsFireIntoTheUnhandledList()
    {
        var infested = Prefixes().GetById("prefix.infested");

        var bus = new GameEventBus();
        using var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => 0);
        engine.AttachAll(infested.Rules, infested.Id);

        bus.Publish(new GameEvent(GameEvents.DamageDealt, Source: "player", Amount: 10));

        var unhandled = Assert.Single(engine.Unhandled);
        Assert.Equal(RuleVocabulary.SpawnEntity, unhandled.Kind);
        Assert.Equal("entity.swarmling", unhandled.Effect.Text);
    }

    /// <summary>Prefix mechanics should span the event surface rather than all keying off
    /// damage — otherwise every build plays the same regardless of which one you took.</summary>
    [Fact]
    public void PrefixHooksSpanTheEventVocabulary()
    {
        var events = Prefixes().GetAll()
            .SelectMany(p => p.Rules.Concat(p.Gauge?.Feeds ?? Enumerable.Empty<TriggerRule>()))
            .Select(r => r.Event)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(events.Count >= 10, $"prefixes only hook {events.Count} distinct events — they are converging.");
        Assert.Contains(GameEvents.Blocked, events);
        Assert.Contains(GameEvents.Killed, events);
        Assert.Contains(GameEvents.ResourceSpent, events);
        Assert.Contains(GameEvents.CraftCompleted, events);   // reaches outside combat
    }
}
