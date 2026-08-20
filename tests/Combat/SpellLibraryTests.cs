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
/// Worked examples for the spell-library expansion (docs/spell-library.md): each mechanic
/// combination the library leans on, proven with the SHIPPED content in a real encounter —
/// not with synthetic moves. If one of these fails, a whole family of authored spells is
/// decorative, which is exactly what D30 forbids.
/// </summary>
public class SpellLibraryTests
{
    // Slow enough that the foe never lands inside a test's 40-tick cast window — these tests
    // measure the SPELL's arithmetic, and a stray enemy hit would show up in the deltas.
    private static readonly MoveDefinition EnemySlash =
        Move("move.test_enemy_slash", DamageType.Slashing, 4, 60, 60, 30);

    private sealed record Harness(
        CombatEncounter Encounter, TickEngine Tick, StatusController Statuses, List<GameEvent> Events);

    /// <summary>An encounter over the real shipped stores — moves, statuses, move modifiers.</summary>
    private static Harness Build()
    {
        var tick = new TickEngine();
        var bus = new GameEventBus();
        var events = new List<GameEvent>();
        bus.Subscribe(events.Add);

        var moves = TestPaths.LoadStore<MoveDefinition>("moves");
        moves.Add(EnemySlash);

        var statuses = new StatusController(
            TestPaths.LoadStore<StatusDefinition>("statuses"), bus, () => tick.CurrentTick);

        var rng = new FakeRandom(0.99);
        var gauges = new GaugeController(Array.Empty<GaugeDefinition>());
        var modifiers = new CombatantModifiers(
            TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
            isOwner: c => c.Team == CombatTeam.Player,
            buildModifiers: () => Array.Empty<Dungeons.Modifiers.ModifierContribution>(),
            statuses, gauges);
        var encounter = new CombatEncounter(
            tick, new HitPipeline(rng), moves, rng, bus,
            statuses, gauges, modifiers,
            moveModifiers: TestPaths.LoadStore<MoveModifierDefinition>("move_modifiers"));

        new TriggerRuleEngine(bus, new SeededRandom(1), () => tick.CurrentTick,
                new CombatConditionWorld(encounter))
            .RegisterCombatHandlers(encounter, rng);
        encounter.ConditionWorld = new CombatConditionWorld(encounter);

        return new Harness(encounter, tick, statuses, events);
    }

    /// <summary>A caster: enough mana for any one spell, and the real moves asked for.</summary>
    private static Combatant Caster(DataStore<MoveDefinition> moves, params string[] moveIds) => new(
        "Caster", CombatTeam.Player,
        new ResourcePool(ResourceType.Health, 300),
        new ResourcePool(ResourceType.Stamina, 100),
        new ResourcePool(ResourceType.Mana, 100, current: 60),
        moveIds.Select(id => Resolved(moves.GetById(id)))
            .Concat(Set(Move("move.test_swing", DamageType.Slashing, 8, 1, 4, 8)))
            .ToList(),
        () => Attrs());

    private static Combatant Foe(double resolve = 0) => new(
        "Foe", CombatTeam.Enemy,
        new ResourcePool(ResourceType.Health, 500),
        new ResourcePool(ResourceType.Stamina, 100),
        new ResourcePool(ResourceType.Mana, 0),
        Set(EnemySlash),
        () => Attrs())
    { Resolve = resolve };

    /// <summary>Queues the move and advances far enough for it to land and recover.</summary>
    private static void Cast(Harness h, string moveId)
    {
        Assert.True(h.Encounter.UseMove(moveId), $"{moveId} was refused at queue time");
        h.Tick.Advance(40);
    }

    [Fact]
    public void AWeaponImbueCastMakesTheNextAttackCarryTheElement()
    {
        var h = Build();
        var moves = TestPaths.LoadStore<MoveDefinition>("moves");
        h.Encounter.Start(Caster(moves, "move.weapon_flame"), new[] { Foe() });

        Cast(h, "move.test_swing");
        Assert.DoesNotContain(h.Events, e => e.Tags?.Contains("lane:heat") == true);

        Cast(h, "move.weapon_flame");
        Cast(h, "move.test_swing");

        // The imbue movemod matched action:attack and added heat as extra — the hit now
        // arrives in the heat lane alongside its steel (D-01: addAsExtra, never a relabel).
        Assert.Contains(h.Events, e => e.Tags?.Contains("lane:heat") == true);
    }

    [Fact]
    public void ABlessingGrantsItsModifierKeyForItsDuration()
    {
        var h = Build();
        var moves = TestPaths.LoadStore<MoveDefinition>("moves");
        var caster = Caster(moves, "move.strength");
        h.Encounter.Start(caster, new[] { Foe() });

        Cast(h, "move.strength");

        var granted = h.Encounter.Modifiers!.Timed.On(caster).ToList();
        Assert.Contains(granted, contribution => contribution.Key == "attr.strength" && contribution.Value == 4);

        // The grant names 500 ticks; long after that the sweep must have dropped it.
        h.Tick.Advance(700);
        Assert.DoesNotContain(
            h.Encounter.Modifiers!.Timed.On(caster),
            contribution => contribution.Key == "attr.strength");
    }

    [Fact]
    public void AControlSpellBuildsResolveInsteadOfLandingDirectly()
    {
        // Fear contributes buildup toward the target's Resolve (D-08). Against a stout target
        // one cast is resisted outright; against a shaken one the same cast lands.
        var stout = Build();
        var moves = TestPaths.LoadStore<MoveDefinition>("moves");
        var stoutFoe = Foe(resolve: 200);
        stout.Encounter.Start(Caster(moves, "move.fear"), new[] { stoutFoe });
        Cast(stout, "move.fear");
        Assert.False(stout.Statuses.Has(stoutFoe, "status.fear"));
        Assert.Contains(stout.Events, e => e.Kind == GameEvents.ControlResisted);

        var shaken = Build();
        var shakenFoe = Foe(resolve: 25);
        shaken.Encounter.Start(Caster(moves, "move.fear"), new[] { shakenFoe });
        Cast(shaken, "move.fear");
        Assert.True(shaken.Statuses.Has(shakenFoe, "status.fear"));
    }

    [Fact]
    public void AHealthCostMovePaysOutInMana()
    {
        var h = Build();
        var moves = TestPaths.LoadStore<MoveDefinition>("moves");
        var caster = Caster(moves, "move.dark_pact");
        h.Encounter.Start(caster, new[] { Foe() });

        var healthBefore = caster.Health.Current;
        var manaBefore = caster.Mana.Current;

        Cast(h, "move.dark_pact");

        Assert.Equal(healthBefore - 12, caster.Health.Current); // the pact's price
        Assert.Equal(manaBefore + 20, caster.Mana.Current);     // the pact's payout
    }

    [Fact]
    public void ConjureWeaponAddsTheSpectralBladeToTheUsableSet()
    {
        var h = Build();
        var moves = TestPaths.LoadStore<MoveDefinition>("moves");
        h.Encounter.Start(Caster(moves, "move.conjure_weapon"), new[] { Foe() });

        Assert.DoesNotContain(h.Encounter.PlayerMoves, m => m.Id == "move.spectral_blade");

        Cast(h, "move.conjure_weapon");

        Assert.Contains(h.Encounter.PlayerMoves, m => m.Id == "move.spectral_blade");
        Assert.True(h.Encounter.UseMove("move.spectral_blade"));
    }
}
