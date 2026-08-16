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
/// E0 — combat publishes to the event bus (docs/effect-foundation.md §10).
///
/// <para>Before this slice <c>CombatEncounter</c> raised <b>zero</b> events, so every combat
/// hook on every Prefix and Suffix in the game was dead. These tests pin the two things that
/// matter: the event <i>shapes</i> (so E1–E4 can build on them), and the claim that shipped
/// class content actually fires against a real fight.</para>
///
/// <para>E0 deliberately raises only vocabulary that <b>already existed</b> in
/// <see cref="GameEvents"/>. The additions in §3.1 arrive with the packet pipeline in E1.</para>
/// </summary>
public class CombatEventTests
{
    private static readonly AbilityDefinition Strike = Ability("ability.strike", DamageType.Slashing, 8, 2, 8, 15, stamina: 5);
    private static readonly AbilityDefinition Slash = Ability("ability.goblin_slash", DamageType.Slashing, 6, 8, 8, 20);
    private static readonly AbilityDefinition Smash = Ability("ability.goblin_smash", DamageType.Crushing, 18, 20, 20, 35);

    private sealed record Recorder(List<GameEvent> Events)
    {
        public IEnumerable<GameEvent> OfKind(string kind) => Events.Where(e => e.Kind == kind);
        public GameEvent First(string kind) => OfKind(kind).First();
        public bool Any(string kind) => OfKind(kind).Any();
        public IReadOnlyList<string> Kinds => Events.Select(e => e.Kind).ToList();
    }

    private static (CombatEncounter enc, TickEngine tick, GameEventBus bus, Recorder log) Build(double roll = 0.99)
    {
        var tick = new TickEngine();
        var bus = new GameEventBus();
        var recorded = new List<GameEvent>();
        bus.Subscribe(recorded.Add);

        var enc = new CombatEncounter(
            tick, new CombatCalculator(new FakeRandom(roll)), Abilities(Strike, Slash, Smash),
            new FakeRandom(roll), bus, "ability.strike");

        return (enc, tick, bus, new Recorder(recorded));
    }

    // --- Shapes -------------------------------------------------------------

    [Fact]
    public void StartingAnEncounter_RaisesEncounterStarted()
    {
        var (enc, _, _, log) = Build();
        enc.Start(Player(hp: 100, attrs: Attrs(con: 5)), new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });

        var started = log.First(GameEvents.EncounterStarted);
        Assert.Equal(CombatEncounter.SelfId, started.Source);
        Assert.Equal(1, started.Amount); // one enemy
    }

    [Fact]
    public void AnEnemyHit_RaisesMoveExecutedThenDamageDealtAndDamageTaken()
    {
        var (enc, tick, _, log) = Build();
        var player = Player(hp: 100, attrs: Attrs(con: 5));
        enc.Start(player, new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });

        tick.Advance(16); // telegraph 8 + windup 8

        // Ordering is the specification: the action resolves, then its outcomes.
        var order = log.Kinds
            .Where(k => k is GameEvents.MoveExecuted or GameEvents.DamageDealt or GameEvents.DamageTaken)
            .ToList();
        Assert.Equal(new[] { GameEvents.MoveExecuted, GameEvents.DamageDealt, GameEvents.DamageTaken }, order);

        var dealt = log.First(GameEvents.DamageDealt);
        Assert.Equal("Raider", dealt.Source);
        Assert.Equal(CombatEncounter.SelfId, dealt.Target);
        Assert.Equal(8, dealt.Amount);

        // DamageTaken is the same hit from the defender's side — so "when you take damage"
        // hooks can reach the attacker.
        var taken = log.First(GameEvents.DamageTaken);
        Assert.Equal(CombatEncounter.SelfId, taken.Source);
        Assert.Equal("Raider", taken.Target);
    }

    [Fact]
    public void EveryCombatEvent_CarriesTheConditionValuesShippedContentReads()
    {
        var (enc, tick, _, log) = Build();
        var player = Player(hp: 100, attrs: Attrs(con: 5));
        enc.Start(player, new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });
        tick.Advance(16);

        // `selfHealthBelow` / `selfHealthAbove` and `firstInEncounter` are shipped condition
        // kinds; they read these names. Without them the conditions silently evaluate to 0.
        foreach (var raised in log.Events)
        {
            Assert.True(raised.Value("self_health_fraction") > 0, $"{raised.Kind} lacks self_health_fraction");
            Assert.True(raised.Value("encounter_index") >= 1, $"{raised.Kind} lacks encounter_index");
        }

        Assert.Equal(0.92, log.First(GameEvents.DamageDealt).Value("self_health_fraction"), 3);
    }

    [Fact]
    public void EncounterIndex_CountsPerKind_SoFirstInEncounterWorks()
    {
        var (enc, tick, _, log) = Build();
        enc.Start(Player(hp: 500, attrs: Attrs(con: 5)), new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });

        tick.Advance(16);      // first hit
        tick.Advance(36);      // recovery 20 + telegraph/windup 16 → second hit

        var hits = log.OfKind(GameEvents.DamageDealt).ToList();
        Assert.Equal(2, hits.Count);
        Assert.Equal(1, hits[0].Value("encounter_index"));
        Assert.Equal(2, hits[1].Value("encounter_index"));
    }

    // --- Tags ---------------------------------------------------------------

    [Fact]
    public void OverheadSmash_IsTaggedHeavy_AndASlashIsNot()
    {
        var (enc, tick, _, log) = Build();
        enc.Start(Player(hp: 200, attrs: Attrs(con: 5)), new[] { Enemy("Brute", 60, Attrs(str: 12), "ability.goblin_smash") });
        tick.Advance(40); // telegraph 20 + windup 20

        var smash = log.First(GameEvents.DamageDealt);
        Assert.True(smash.HasTag("heavy"), "Overhead Smash (40 ticks to impact) must be heavy");
        Assert.True(smash.HasTag("crushing"));
        Assert.True(smash.HasTag("attack"));

        var (enc2, tick2, _, log2) = Build();
        enc2.Start(Player(hp: 200, attrs: Attrs(con: 5)), new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });
        tick2.Advance(16);

        var slash = log2.First(GameEvents.DamageDealt);
        Assert.False(slash.HasTag("heavy"), "Rusty Slash (16 ticks to impact) must not be heavy");
        Assert.True(slash.HasTag("light"));
        Assert.True(slash.HasTag("slashing"));
    }

    [Fact]
    public void ACriticalHit_IsTaggedCritical()
    {
        var (enc, tick, _, log) = Build(roll: 0.0); // FakeRandom(0) → always crit
        enc.Start(Player(hp: 200, attrs: Attrs(con: 5)), new[] { Enemy("Raider", 50, Attrs(str: 6, luck: 40), "ability.goblin_slash") });
        tick.Advance(16);

        Assert.True(log.First(GameEvents.DamageDealt).HasTag("critical"));
    }

    // --- Defensive events ---------------------------------------------------

    [Fact]
    public void Blocking_RaisesBlockedFromTheDefendersPerspective()
    {
        var (enc, tick, _, log) = Build();
        var player = Player(hp: 100, attrs: Attrs(con: 5), stamina: 50);
        enc.Start(player, new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });

        tick.Advance(8);
        enc.Block();       // 16-tick stance covers the impact at 16
        tick.Advance(8);

        var blocked = log.First(GameEvents.Blocked);

        // Exploding Kneecaps' Guard expression detonates "against the attacker", so the
        // attacker must be reachable from the event.
        Assert.Equal(CombatEncounter.SelfId, blocked.Source);
        Assert.Equal("Raider", blocked.Target);
        Assert.True(blocked.HasTag("blocked"));

        // A blocked hit still deals (reduced) damage — it is mitigation, not avoidance.
        Assert.True(log.Any(GameEvents.DamageDealt));
    }

    [Fact]
    public void Dodging_RaisesDodged_AndNoDamageEvents()
    {
        var (enc, tick, _, log) = Build();
        var player = Player(hp: 100, attrs: Attrs(con: 5), stamina: 50);
        enc.Start(player, new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });

        tick.Advance(10);
        enc.Dodge();       // 10-tick stance covers the impact at 16
        tick.Advance(6);

        Assert.True(log.Any(GameEvents.Dodged));
        Assert.False(log.Any(GameEvents.DamageDealt));
        Assert.False(log.Any(GameEvents.DamageTaken));
    }

    [Fact]
    public void SpendingStaminaOnAnAttackOrAStance_RaisesResourceSpent()
    {
        var (enc, _, _, log) = Build();
        var player = Player(hp: 100, attrs: Attrs(con: 5), stamina: 50);
        enc.Start(player, new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });

        enc.Attack();
        var attackSpend = log.OfKind(GameEvents.ResourceSpent).First();
        Assert.Equal(5, attackSpend.Amount);
        Assert.True(attackSpend.HasTag("stamina"));

        enc.Block();
        var blockSpend = log.OfKind(GameEvents.ResourceSpent).Last();
        Assert.Equal(CombatTuning.BlockStaminaCost, blockSpend.Amount);
        Assert.True(blockSpend.HasTag("block"));
    }

    [Fact]
    public void KillingAnEnemy_RaisesKilledAndDefeatedAndEncounterEnded()
    {
        var (enc, tick, _, log) = Build();
        var player = Player(hp: 100, attrs: Attrs(str: 40, con: 5), stamina: 50);
        enc.Start(player, new[] { Enemy("Raider", 1, Attrs(str: 6), "ability.goblin_slash") });

        enc.Attack();
        tick.Advance(10);

        var killed = log.First(GameEvents.Killed);
        Assert.Equal(CombatEncounter.SelfId, killed.Source);
        Assert.Equal("Raider", killed.Target);

        // Defeated is the same moment from the victim's side.
        Assert.Equal("Raider", log.First(GameEvents.Defeated).Source);

        Assert.True(log.First(GameEvents.EncounterEnded).HasTag("victory"));
    }

    // --- The point of the slice --------------------------------------------

    [Fact]
    public void ShippedPrefixHooks_FireAgainstARealFight()
    {
        // The claim E0 exists to prove: authored class content, loaded from game/data,
        // reacts to a real encounter with no per-Base or per-content wiring.
        var prefixes = TestPaths.LoadStore<PrefixDefinition>("prefixes");
        var galvanic = prefixes.GetById("prefix.galvanic");

        var (enc, tick, bus, _) = Build();
        var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => tick.CurrentTick);

        // Galvanic's gauge feed: Charge accumulates from ANY resource spend. It never names a
        // Base — that is the rule that keeps the roster at 25 mechanics instead of 375.
        foreach (var feed in galvanic.Gauge!.Feeds)
            engine.Attach(feed, galvanic.Id);
        foreach (var rule in galvanic.Rules)
            engine.Attach(rule, galvanic.Id);

        var player = Player(hp: 100, attrs: Attrs(con: 5), stamina: 50);
        enc.Start(player, new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });
        enc.Attack();

        var charged = engine.Fired.Where(f => f.Trigger.Kind == GameEvents.ResourceSpent).ToList();
        Assert.NotEmpty(charged);

        // Magnitude scales with the amount spent (`scales_with: "amount"`), so a 5-stamina
        // attack charges 1.2 × 5 = 6.
        Assert.Equal(6.0, charged[0].Magnitude, 3);

        // …and it lands in Unhandled, because `grantResource` has no handler until E3.
        // Visibly inert rather than silently missing (DECISIONS D23).
        Assert.Contains(engine.Unhandled, u => u.Kind == "grantResource");
    }

    [Fact]
    public void ExplodingKneecapsGuardExpression_FiresOnABlock()
    {
        var suffixes = TestPaths.LoadStore<SuffixDefinition>("suffixes");
        var kneecaps = suffixes.GetById("suffix.exploding_kneecaps");
        var guard = kneecaps.Expressions.Single(e => e.Channel == ExpressionChannel.Guard);

        var (enc, tick, bus, _) = Build();
        var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => tick.CurrentTick);
        engine.Attach(guard.Rule, kneecaps.Id);

        var player = Player(hp: 100, attrs: Attrs(con: 5), stamina: 50);
        enc.Start(player, new[] { Enemy("Raider", 50, Attrs(str: 6), "ability.goblin_slash") });

        tick.Advance(8);
        enc.Block();
        tick.Advance(8);

        var detonation = Assert.Single(engine.Fired);
        Assert.Equal("areaDamage", detonation.Kind);

        // 50% of the blocked damage (`scales_with: "amount"`).
        Assert.True(detonation.Magnitude > 0, "the blast must scale off the damage that was blocked");
    }
}
