using Dungeons.Combat;
using Dungeons.Events;
using Dungeons.Rules;
using Dungeons.Simulation;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Combat;

/// <summary>
/// Auto-combat (Phase 10) — GDD §5.7 and the D-07 consequence in
/// <c>docs/damage-and-defense.md</c> §5.1.1.
///
/// <para>These tests exist to hold <b>two design claims</b>, not to cover a class:</para>
/// <list type="number">
///   <item><b>Automation only chooses.</b> Every effect of an automated fight is produced by the
///   normal encounter — the same telegraph phases, the same stances, the same hit pipeline. If
///   anyone ever adds a shortcut resolver for automated play, the tests that watch the normal
///   machinery fire are what should fail.</item>
///   <item><b>Its disadvantage is reaction latency, never a damage penalty.</b> A pilot must
///   commit its stance <c>reaction_ticks</c> before impact, so it lands the wide windows and can
///   never reach the tight ones. Automated play is therefore weaker in a way a player can see
///   and out-play, and gear that widens windows is worth more to it.</item>
/// </list>
/// </summary>
public class AutoCombatTests
{
    // A heavily telegraphed swing: 20 telegraph + 20 windup = 40 ticks of visible intent, which
    // is what a defender is meant to be able to answer.
    private static readonly MoveDefinition Smash = Move("move.goblin_smash", DamageType.Crushing, 18, 20, 20, 35);

    // Fast and barely telegraphed: 1 + 4 ticks to impact, inside an 8-tick reaction.
    private static readonly MoveDefinition Jab = Move("move.quick_stab", DamageType.Piercing, 6, 1, 4, 20);

    private static readonly MoveDefinition Strike = Move("move.strike", DamageType.Slashing, 8, 2, 8, 15, stamina: 5);
    private static readonly MoveDefinition Cleave = Move("move.cleave", DamageType.Slashing, 10, 4, 10, 20, stamina: 8);

    private static AutoCombatProfileDefinition Profile(
        int reactionTicks = AutoCombatTuning.DefaultReactionTicks,
        DefensiveStance stance = DefensiveStance.Block,
        bool defends = true,
        params AiRuleSpec[] rules) => new()
    {
        Id = "auto.test",
        Name = "Test Brain",
        Description = "For tests.",
        ReactionTicks = reactionTicks,
        Rules = rules.Length > 0 ? rules : new[] { new AiRuleSpec { MoveTag = "action:attack", Weight = 1 } },
        Defence = defends
            ? new[] { new DefenceRuleSpec { Stance = stance, Weight = 1 } }
            : Array.Empty<DefenceRuleSpec>(),
    };

    private sealed class Fixture
    {
        public required TickEngine Tick { get; init; }
        public required CombatEncounter Encounter { get; init; }
        public required Combatant Hero { get; init; }
        public required Combatant Foe { get; init; }
        public required List<GameEvent> Events { get; init; }
    }

    private static Fixture Start(MoveDefinition enemyMove, int heroStamina = 100)
    {
        var tick = new TickEngine();
        var bus = new GameEventBus();
        var seen = new List<GameEvent>();
        bus.Subscribe(seen.Add);

        var encounter = new CombatEncounter(
            tick, new HitPipeline(new FakeRandom(0.99)), Moves(Strike, Cleave, enemyMove),
            new FakeRandom(0.5), bus);

        var hero = Player(hp: 200, stamina: heroStamina, moveset: Set(Strike, Cleave));
        var foe = Enemy("Brute", 500, Attrs(), enemyMove);
        encounter.Start(hero, new[] { foe });

        return new Fixture { Tick = tick, Encounter = encounter, Hero = hero, Foe = foe, Events = seen };
    }

    private static AutoCombatPilot Engage(Fixture fixture, AutoCombatProfileDefinition profile)
    {
        var pilot = new AutoCombatPilot(fixture.Encounter, fixture.Tick, profile, new FakeRandom(0.5));
        pilot.Engage();
        return pilot;
    }

    // --- Claim 1: it only chooses ---------------------------------------------

    /// <summary>
    /// The pilot's attacks go through the whole normal lifecycle: queued, telegraphed, and
    /// resolved by the hit pipeline. A shortcut resolver would show up here as damage with no
    /// <c>ActionQueued</c> in front of it.
    /// </summary>
    [Fact]
    public void AnAutomatedAttackRunsTheNormalActionLifecycle()
    {
        var fixture = Start(Smash);
        Engage(fixture, Profile(defends: false));

        fixture.Tick.Advance(60);

        Assert.Contains(fixture.Events, e => e.Kind == GameEvents.ActionQueued && e.Source == CombatEncounter.SelfId);
        Assert.Contains(fixture.Events, e => e.Kind == GameEvents.DamageDealt && e.Source == CombatEncounter.SelfId);
        Assert.True(fixture.Foe.Health.Current < fixture.Foe.Health.Max);
        Assert.NotNull(fixture.Encounter.LastHit); // the pipeline produced a real traced hit
    }

    /// <summary>Move costs are paid because <see cref="CombatEncounter.UseMove"/> pays them —
    /// the pilot has no route around them.</summary>
    [Fact]
    public void AutomatedAttacksPayTheirCosts()
    {
        var fixture = Start(Smash);
        Engage(fixture, Profile(defends: false));

        fixture.Tick.Advance(30);

        Assert.True(fixture.Hero.Stamina.Current < fixture.Hero.Stamina.Max);
    }

    /// <summary>
    /// Disengaging hands control back completely: the brain comes off the player combatant, and
    /// nothing is left scheduling decisions.
    /// </summary>
    [Fact]
    public void DisengagingReturnsTheCombatantToTheKeyboard()
    {
        var fixture = Start(Smash);
        var pilot = Engage(fixture, Profile(defends: false));
        Assert.NotEmpty(fixture.Hero.Ai);

        pilot.Disengage();
        var staminaAtHandover = fixture.Hero.Stamina.Current;
        fixture.Tick.Advance(200);

        Assert.Empty(fixture.Hero.Ai);
        Assert.False(pilot.IsEngaged);
        Assert.True(fixture.Hero.Stamina.Current >= staminaAtHandover); // it only regenerated
    }

    // --- Claim 2: latency, not a damage penalty -------------------------------

    /// <summary>
    /// It blocks reliably. A 16-tick block window comfortably contains an 8-tick reaction, which
    /// is exactly what §5.1.1's table promises — automated play functions, it just does not
    /// excel.
    /// </summary>
    [Fact]
    public void ItBlocksATelegraphedAttack()
    {
        var fixture = Start(Smash);
        Engage(fixture, Profile(stance: DefensiveStance.Block));

        fixture.Tick.Advance(45); // past the 40-tick impact

        Assert.Contains(fixture.Events, e => e.Kind == GameEvents.Blocked);
    }

    /// <summary>
    /// And it can never Perfect Block. The stance goes up 8 ticks before impact and the perfect
    /// window is the first 4 ticks of the stance, so the hit lands as a normal block. This is
    /// the whole of automation's handicap: not less damage, just never the best outcome.
    /// </summary>
    [Fact]
    public void ItNeverLandsAPerfectBlock()
    {
        var fixture = Start(Smash);
        Engage(fixture, Profile(stance: DefensiveStance.Block));

        fixture.Tick.Advance(45);

        Assert.Contains(fixture.Events, e => e.Kind == GameEvents.Blocked);
        Assert.DoesNotContain(fixture.Events, e => e.Kind == GameEvents.Blocked && e.HasTag("perfect"));
        Assert.True(fixture.Hero.Health.Current < fixture.Hero.Health.Max); // mitigated, not negated
    }

    /// <summary>The other wide window: a 10-tick dodge still contains an 8-tick reaction, so
    /// automated play avoids outright rather than only mitigating — §5.1.1's "often".</summary>
    [Fact]
    public void ItDodgesATelegraphedAttack()
    {
        var fixture = Start(Smash);
        Engage(fixture, Profile(stance: DefensiveStance.Dodge));

        fixture.Tick.Advance(45);

        Assert.Contains(fixture.Events, e => e.Kind == GameEvents.Dodged);
        Assert.Equal(fixture.Hero.Health.Max, fixture.Hero.Health.Current); // avoided, not reduced
    }

    /// <summary>
    /// <b>The fence that matters most.</b> An automated swing does exactly the damage the same
    /// swing does by hand — no multiplier, no penalty, nothing. D-07 is specifically the decision
    /// that automation's weakness is expressed as latency and never as an arbitrary damage
    /// handicap, and this is what would fail the day someone adds one.
    /// </summary>
    [Fact]
    public void AnAutomatedSwingHitsForExactlyWhatAManualOneDoes()
    {
        var byHand = Start(Smash);
        byHand.Encounter.UseMove("move.strike");
        byHand.Tick.Advance(15);
        var manualDamage = byHand.Foe.Health.Max - byHand.Foe.Health.Current;

        var automated = Start(Smash);
        Engage(automated, Profile(defends: false, rules: new AiRuleSpec { Move = "move.strike", Weight = 1 }));
        automated.Tick.Advance(15);
        var automatedDamage = automated.Foe.Health.Max - automated.Foe.Health.Current;

        Assert.True(manualDamage > 0);
        Assert.Equal(manualDamage, automatedDamage);
    }

    /// <summary>
    /// It never parries either — and not because the pilot is forbidden to, but because a
    /// 3-tick window is unreachable for anything slower than 3 ticks. The validator holds the
    /// other half: no profile may be authored fast enough to reach it.
    /// </summary>
    [Fact]
    public void ItNeverParries()
    {
        var fixture = Start(Smash);
        fixture.Encounter.PlayerCanParry = true; // even with the gear that grants it
        Engage(fixture, Profile(stance: DefensiveStance.Block));

        fixture.Tick.Advance(45);

        Assert.DoesNotContain(fixture.Events, e => e.Kind == GameEvents.Parried);
        Assert.True(AutoCombatTuning.MinimumReactionTicks > CombatTuning.ParryWindowTicks);
    }

    /// <summary>
    /// A fast, barely-telegraphed attack simply beats it. Correct, and it is why the small
    /// untelegraphed-only <c>evade</c> passive survived D-07 at all — automated play needs some
    /// answer to what it cannot react to.
    /// </summary>
    [Fact]
    public void AnAttackFasterThanItsReactionIsNotAnswered()
    {
        var fixture = Start(Jab);
        Engage(fixture, Profile(stance: DefensiveStance.Block));

        fixture.Tick.Advance(8); // impact at tick 5

        Assert.Contains(fixture.Events, e => e.Kind == GameEvents.DamageTaken && e.Source == CombatEncounter.SelfId);
        Assert.DoesNotContain(fixture.Events, e => e.Kind == GameEvents.Blocked);
    }

    /// <summary>
    /// One telegraph buys one stance. Without this the pilot would re-raise its guard on every
    /// poll for the rest of the windup and drain its own stamina answering a single attack.
    /// </summary>
    [Fact]
    public void OneIncomingAttackIsAnsweredOnce()
    {
        var fixture = Start(Smash);
        Engage(fixture, Profile(stance: DefensiveStance.Block, defends: true,
            rules: new AiRuleSpec { Move = "move.nothing", Weight = 1 })); // never attacks: stamina moves only on defence

        fixture.Tick.Advance(45);

        var spentOnBlocking = fixture.Events.Count(e =>
            e.Kind == GameEvents.ResourceSpent && e.HasTag("block"));
        Assert.Equal(1, spentOnBlocking);
    }

    /// <summary>A profile with no defence rules is a legal, genuinely different playstyle — it
    /// trades taking hits for never spending stamina on guard.</summary>
    [Fact]
    public void ABrainWithNoDefenceRulesSimplyTakesTheHit()
    {
        var fixture = Start(Smash);
        Engage(fixture, Profile(defends: false));

        fixture.Tick.Advance(45);

        Assert.DoesNotContain(fixture.Events, e => e.Kind == GameEvents.Blocked);
        Assert.True(fixture.Hero.Health.Current < fixture.Hero.Health.Max);
    }

    // --- Reuse of the existing AI architecture --------------------------------

    /// <summary>
    /// The offensive half is the enemy brain, pointed at the player: the same
    /// <see cref="AiRuleSpec"/> type, the same tag matching, answered by the same
    /// <see cref="CombatEncounter.ChooseMoveFor"/>. That is what GDD §5.7 means by "the player
    /// driven by the same profile shape", and it is why there is no second selection algorithm.
    /// </summary>
    [Fact]
    public void TheOffensiveBrainIsTheEnemyRuleShapeAndRespectsItsConditions()
    {
        var fixture = Start(Smash);

        // Only Cleave is weighted, by id — so Cleave is what it uses, not the first attack.
        Engage(fixture, Profile(defends: false, rules: new AiRuleSpec { Move = "move.cleave", Weight = 1 }));

        var chosen = fixture.Encounter.ChooseMoveFor(fixture.Hero);
        Assert.Equal("move.cleave", chosen!.Id);

        fixture.Tick.Advance(20);
        Assert.Contains(fixture.Events, e => e.Kind == GameEvents.ActionQueued && e.HasTag("action:attack"));
    }

    /// <summary>Same seed, same state, same choice — automation must not make a fight
    /// unreproducible.</summary>
    [Fact]
    public void AutomatedDecisionsAreDeterministicUnderASeed()
    {
        static int DamageAfter(int ticks)
        {
            var fixture = Start(Smash);
            Engage(fixture, Profile());
            fixture.Tick.Advance(ticks);
            return fixture.Foe.Health.Max - fixture.Foe.Health.Current;
        }

        Assert.Equal(DamageAfter(120), DamageAfter(120));
    }

    // --- The shipped brains ---------------------------------------------------

    /// <summary>
    /// Every shipped brain is slower than the tight windows. The validator enforces this; this
    /// asserts the shipped content actually satisfies it, which is the difference between a rule
    /// and a rule that is obeyed.
    /// </summary>
    [Fact]
    public void EveryShippedBrainIsTooSlowForTheTightWindows()
    {
        var profiles = TestPaths.LoadStore<AutoCombatProfileDefinition>("auto_combat").GetAll();

        Assert.NotEmpty(profiles);
        foreach (var profile in profiles)
        {
            Assert.True(profile.ReactionTicks > CombatTuning.PerfectBlockWindowTicks, profile.Id);
            Assert.True(profile.ReactionTicks > CombatTuning.ParryWindowTicks, profile.Id);
        }
    }

    /// <summary>
    /// The shipped brains match moves by TAG, never by id. A player's moveset comes from their
    /// weapon, so an id-matched brain would work with one sword and stand still holding another.
    /// </summary>
    [Fact]
    public void ShippedBrainsMatchMovesByTagSoAnyWeaponWorks()
    {
        var profiles = TestPaths.LoadStore<AutoCombatProfileDefinition>("auto_combat").GetAll();

        foreach (var profile in profiles)
            foreach (var rule in profile.Rules)
                Assert.False(string.IsNullOrEmpty(rule.MoveTag),
                    $"{profile.Id} names move '{rule.Move}' by id; a brain must work with whatever the player is carrying.");
    }
}
