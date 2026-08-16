using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Events;
using Dungeons.Modifiers;
using Dungeons.Randomness;
using Dungeons.Rules;
using Dungeons.Simulation;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Combat;

/// <summary>
/// E3c-2 — modifiers stop being decorative.
///
/// <para>Four systems were authoring modifier contributions and none of them reached a fight:
/// <c>ResolvedBuild.Modifiers</c> had no consumer, <c>StatusController.ModifierTotal</c> was
/// called only by its own tests, gauge bands were declared and ignored, and
/// <c>grantModifier</c> — the third most-authored effect — landed in <c>Unhandled</c>. The
/// common cause was that nothing in combat ever <i>read</i> a modifier. These tests pin each
/// contributor arriving, and pin the numbers they produce.</para>
/// </summary>
public class ModifierReadPathTests
{
    private static readonly AbilityDefinition Strike = Ability("ability.strike", DamageType.Slashing, 10, 2, 8, 15, stamina: 5);
    private static readonly AbilityDefinition Slash = Ability("ability.goblin_slash", DamageType.Slashing, 6, 8, 8, 20);

    private sealed record Harness(
        CombatEncounter Encounter,
        TickEngine Tick,
        GameEventBus Bus,
        TriggerRuleEngine Engine,
        CombatantModifiers Modifiers,
        GaugeController Gauges);

    private static DataStore<ModifierKeyDefinition> Keys() =>
        TestPaths.LoadStore<ModifierKeyDefinition>("modifier_keys");

    private static Harness Build(
        IEnumerable<ModifierContribution>? buildModifiers = null,
        IEnumerable<GaugeDefinition>? gauges = null,
        double roll = 0.99)
    {
        var tick = new TickEngine();
        var bus = new GameEventBus();
        var statuses = new StatusController(
            TestPaths.LoadStore<StatusDefinition>("statuses"), bus, () => tick.CurrentTick);
        var gaugeController = new GaugeController(gauges ?? Array.Empty<GaugeDefinition>());

        var modifiers = new CombatantModifiers(
            Keys(),
            isOwner: c => c.Team == CombatTeam.Player,
            buildModifiers: () => buildModifiers ?? Array.Empty<ModifierContribution>(),
            statuses, gaugeController);

        var rng = new FakeRandom(roll);
        var encounter = new CombatEncounter(
            tick, new CombatCalculator(rng, modifiers), Abilities(Strike, Slash), rng, bus,
            "ability.strike", statuses, gaugeController, modifiers);

        var engine = new TriggerRuleEngine(bus, new SeededRandom(1), () => tick.CurrentTick)
            .RegisterCombatHandlers(encounter, rng);

        return new Harness(encounter, tick, bus, engine, modifiers, gaugeController);
    }

    private static void StartFight(Harness h) =>
        h.Encounter.Start(
            Player(hp: 200, attrs: Attrs(con: 5), stamina: 50),
            new[] { Enemy("Raider", 200, Attrs(str: 6), "ability.goblin_slash") });

    // --- The contributors that were inert ----------------------------------------------------

    /// <summary>
    /// <b>Chill finally slows something.</b> The status is literally
    /// <c>{ key: combat.windup.mult, value: 1.25 }</c> and has been authored, validated and
    /// completely without effect since E2 — because nothing read it.
    /// </summary>
    [Fact]
    public void ChillLengthensAnEnemysWindup()
    {
        var h = Build();
        StartFight(h);
        var raider = h.Encounter.Enemies[0];

        // Slash telegraphs for 8, then winds up for 8.
        h.Tick.Advance(8);
        var unchilled = h.Encounter.ActionOf(raider)!.PhaseEndsTick - h.Tick.CurrentTick;

        h.Encounter.Interrupt(raider);
        h.Encounter.ApplyStatus(raider, "status.chill", CombatEncounter.SelfId);
        h.Tick.Advance(28);   // recovery, then a fresh telegraph

        var chilled = h.Encounter.ActionOf(raider)!.PhaseEndsTick - h.Tick.CurrentTick;

        Assert.Equal(8, unchilled);
        Assert.Equal(10, chilled);   // 8 × 1.25
    }

    /// <summary>Corroded strips armour per stack — the defensive half of the same gap.</summary>
    [Fact]
    public void CorrodedStripsArmourThroughTheReadPath()
    {
        var h = Build();
        var armoured = Enemy("Raider", 200, Attrs(str: 6), "ability.goblin_slash",
            armor: new ArmorProfile { Armor = 12, Resistances = new Dictionary<string, double>() });

        h.Encounter.Start(Player(hp: 200, attrs: Attrs(con: 5), stamina: 50), new[] { armoured });

        // `Combatant.Armour` already folds in the attribute component, so the baseline is the
        // combatant's own number rather than the profile's.
        var clean = h.Encounter.Modifiers!.Resolve(armoured, ModifierKeys.Armor, ModifierContext.None, armoured.Armour);
        h.Encounter.ApplyStatus(armoured, "status.corroded", CombatEncounter.SelfId);
        var corroded = h.Encounter.Modifiers.Resolve(armoured, ModifierKeys.Armor, ModifierContext.None, armoured.Armour);

        Assert.Equal(armoured.Armour, clean, 3);
        Assert.Equal(clean - 3, corroded, 3);   // −3 per stack
    }

    /// <summary>A gauge band is what the meter's <i>level</i> does. Galvanic's is +15% magic
    /// damage at 60% Charge, and it contributed to nothing until this slice.</summary>
    [Fact]
    public void AGaugeBandContributesOnceTheMeterIsHighEnough()
    {
        var galvanic = TestPaths.LoadStore<PrefixDefinition>("prefixes").GetById("prefix.galvanic");
        var h = Build(gauges: new[] { galvanic.Gauge! });
        StartFight(h);

        var band = galvanic.Gauge!.Bands[0];
        var player = h.Encounter.Player;

        Assert.Equal(1.0, h.Modifiers.Resolve(player, band.Modifier, ModifierContext.None), 3);

        h.Gauges.Add("Charge", 70, h.Tick.CurrentTick);   // over the 0.6 threshold

        Assert.Equal(band.Value, h.Modifiers.Resolve(player, band.Modifier, ModifierContext.None), 3);
    }

    /// <summary>Build modifiers belong to the character running the build, not to everyone in
    /// the fight.</summary>
    [Fact]
    public void BuildModifiersReachThePlayerAndNotTheEnemy()
    {
        var h = Build(buildModifiers: new[]
        {
            new ModifierContribution(ModifierKeys.DamageMult, 1.5, "The Galvanic"),
        });
        StartFight(h);

        Assert.Equal(1.5, h.Modifiers.Resolve(h.Encounter.Player, ModifierKeys.DamageMult, ModifierContext.None), 3);
        Assert.Equal(1.0, h.Modifiers.Resolve(h.Encounter.Enemies[0], ModifierKeys.DamageMult, ModifierContext.None), 3);
    }

    // --- grantModifier -----------------------------------------------------------------------

    [Fact]
    public void GrantModifierAppliesTheAuthoredKeyToTheSelectedTarget()
    {
        var h = Build();
        h.Engine.Attach(new TriggerRule
        {
            Id = "surge",
            Event = GameEvents.MoveExecuted,
            Target = EffectTarget.Self,
            Effect = new EffectSpec
            {
                Kind = RuleVocabulary.GrantModifier,
                Text = ModifierKeys.DamageMult,
                Amount = 1.2,
                DurationTicks = 40,
            },
        }, "test.surge");

        StartFight(h);
        h.Encounter.Attack();
        h.Tick.Advance(12);

        Assert.Equal(1.2, h.Modifiers.Resolve(h.Encounter.Player, ModifierKeys.DamageMult, ModifierContext.None), 3);
        Assert.DoesNotContain(h.Engine.Unhandled, u => u.Kind == RuleVocabulary.GrantModifier);
    }

    /// <summary>
    /// A grant runs out on the sweep.
    ///
    /// <para>Granted directly rather than through a rule, deliberately: every combatant swinging
    /// raises <c>MoveExecuted</c>, so a rule hooked to it re-grants faster than the duration
    /// expires and the modifier never appears to lapse. That is correct behaviour and it makes
    /// expiry untestable through the rule.</para>
    /// </summary>
    [Fact]
    public void AGrantExpiresWhenItsDurationRunsOut()
    {
        var h = Build();
        StartFight(h);
        var player = h.Encounter.Player;

        h.Modifiers.Timed.Grant(player, ModifierKeys.DamageMult, 1.2, "test", durationTicks: 40, nowTick: h.Tick.CurrentTick);
        Assert.Equal(1.2, h.Modifiers.Resolve(player, ModifierKeys.DamageMult, ModifierContext.None), 3);

        h.Tick.Advance(20);
        Assert.Equal(1.2, h.Modifiers.Resolve(player, ModifierKeys.DamageMult, ModifierContext.None), 3);

        h.Tick.Advance(40);   // past 40 ticks; the sweep drops it
        Assert.Equal(1.0, h.Modifiers.Resolve(player, ModifierKeys.DamageMult, ModifierContext.None), 3);
    }

    /// <summary>A duration of 0 lasts the encounter — the one authored grant that omits it is a
    /// permanent armour bonus.</summary>
    [Fact]
    public void AGrantWithNoDurationLastsTheEncounter()
    {
        var h = Build();
        StartFight(h);
        var player = h.Encounter.Player;

        h.Modifiers.Timed.Grant(player, ModifierKeys.Armor, 5, "test", durationTicks: 0, nowTick: h.Tick.CurrentTick);
        h.Tick.Advance(5000);

        Assert.Equal(5, h.Modifiers.Resolve(player, ModifierKeys.Armor, ModifierContext.None, 0), 3);
    }

    // --- The pipeline actually uses them -----------------------------------------------------

    [Fact]
    public void IncreasedDamageScalesTheHitAndShowsInTheTrace()
    {
        var plain = Build();
        StartFight(plain);
        plain.Encounter.Attack();
        plain.Tick.Advance(12);
        var baseline = plain.Encounter.LastHit!.Amount;

        var buffed = Build(buildModifiers: new[]
        {
            new ModifierContribution(ModifierKeys.DamageMult, 1.5, "The Galvanic"),
        });
        StartFight(buffed);
        buffed.Encounter.Attack();
        buffed.Tick.Advance(12);

        Assert.True(buffed.Encounter.LastHit!.Amount > baseline,
            $"increased damage should raise the hit ({baseline} → {buffed.Encounter.LastHit.Amount})");
        Assert.Contains(buffed.Encounter.LastHit.Log.Lines, e => e.Stage == HitStages.Increased);
    }

    /// <summary>Block strength scales what the guard <i>eats</i>. Multiplying the damage that
    /// gets through instead would make "+30% block strength" increase the damage taken.</summary>
    [Fact]
    public void BlockStrengthMitigatesMoreRatherThanLess()
    {
        var h = Build(buildModifiers: new[]
        {
            new ModifierContribution(ModifierKeys.BlockMult, 1.3, "equip.tower_shield"),
        });
        StartFight(h);

        h.Tick.Advance(8);
        h.Encounter.Block();
        h.Tick.Advance(10);

        var blocked = h.Encounter.LastHit!;
        Assert.True(blocked.Blocked);

        // 1 − (1 − 0.4) × 1.3 = 0.22, against the unbuffed 0.4.
        var line = Assert.Single(blocked.Log.Lines, e => e.Stage == HitStages.Block);
        Assert.Contains("0.22", line.Detail);
    }

    // --- The context the read path builds ----------------------------------------------------

    /// <summary>
    /// One swing carries several move tags at once. A context that held a single value per
    /// dimension would match at most one of them, silently dropping every move-tag modifier on
    /// any move with more than one tag — which is every move.
    /// </summary>
    [Fact]
    public void AContextSuppliesEveryTagTheMoveCarries()
    {
        var context = ModifierContext.For(ScopeDimensions.MoveTag, "attack", "melee", "light");

        Assert.True(context.Matches(new ModifierScope(ScopeDimensions.MoveTag, "melee")));
        Assert.True(context.Matches(new ModifierScope(ScopeDimensions.MoveTag, "light")));
        Assert.False(context.Matches(new ModifierScope(ScopeDimensions.MoveTag, "spell")));

        // …and an unscoped contribution still applies to everything.
        Assert.True(context.Matches(null));
    }

    /// <summary>The scoped-key guard survives the read path: a key needing a dimension the
    /// caller did not supply still throws rather than answering.</summary>
    [Fact]
    public void TheScopedKeyGuardStillFiresThroughCombatantModifiers()
    {
        var h = Build();
        StartFight(h);

        Assert.Throws<InvalidOperationException>(
            () => h.Modifiers.Resolve(h.Encounter.Player, "profession.yield.mult", ModifierContext.None));
    }
}
