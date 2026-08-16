using Dungeons.Characters;
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
/// M2′c — the enemy framework: family → role → actor folding, tag-matched AI rules,
/// avoid-repeat weighting, and the whole thing running deterministically under a seed.
/// </summary>
public class EnemyFrameworkTests
{
    // --- The fold -------------------------------------------------------------

    private static DataStore<EnemyFamilyDefinition> Families(params EnemyFamilyDefinition[] items)
    {
        var store = new DataStore<EnemyFamilyDefinition>();
        foreach (var item in items) store.Add(item);
        return store;
    }

    private static DataStore<CombatRoleDefinition> Roles(params CombatRoleDefinition[] items)
    {
        var store = new DataStore<CombatRoleDefinition>();
        foreach (var item in items) store.Add(item);
        return store;
    }

    private static DataStore<AiProfileDefinition> Profiles(params AiProfileDefinition[] items)
    {
        var store = new DataStore<AiProfileDefinition>();
        foreach (var item in items) store.Add(item);
        return store;
    }

    [Fact]
    public void TheFoldLayersFamilyRoleAndActor()
    {
        var family = new EnemyFamilyDefinition
        {
            Id = "family.test", Name = "Test Family",
            Tags = new[] { "family:test" },
            Attributes = AttributeSet.Uniform(5),
            Resources = new ActorResources { Health = 30, Mana = 0, Stamina = 40 },
            Resistances = new() { ["toxin"] = 0.25, ["heat"] = 0.10 },
            Resolve = 50,
        };
        var role = new CombatRoleDefinition
        {
            Id = "role.test", Name = "Test Role",
            AttributeTweaks = new AttributeSet { Strength = 5, Dexterity = -3 },
            ResourceTweaks = new ResourceDelta { Health = 30 },
            Resistances = new() { ["heat"] = 0.30 },
            Vulnerable = new() { ["Crushing"] = 1.25 },
            Armor = 12,
            Resolve = 80,
            AiProfile = "ai.test",
        };
        var profile = new AiProfileDefinition
        {
            Id = "ai.test", Name = "Test Brain", AvoidRepeatWeight = 0.4,
            Rules = new[] { new AiRuleSpec { MoveTag = "action:attack", Weight = 3 } },
        };
        var actor = new ActorDefinition
        {
            Id = "actor.test", Name = "Test",
            Family = "family.test", Role = "role.test",
            AttributeTweaks = new AttributeSet { Luck = 2 },
            ResourceTweaks = new ResourceDelta { Mana = 20 },
            Resistances = new() { ["toxin"] = 0.50 },
            Moves = new[] { new MoveGrantSpec { Id = "move.a" } },
            Ai = new[] { new AiRuleSpec { Move = "move.a", Weight = 1 } },
            Tags = new[] { "unique:test" },
        };

        var resolved = ActorResolver.Resolve(actor, Families(family), Roles(role), Profiles(profile));

        Assert.Equal(10, resolved.Attributes.Strength);       // 5 + 5
        Assert.Equal(2, resolved.Attributes.Dexterity);       // 5 − 3
        Assert.Equal(7, resolved.Attributes.Luck);            // 5 + 2 (actor delta)
        Assert.Equal(60, resolved.Resources.Health);          // 30 + 30
        Assert.Equal(20, resolved.Resources.Mana);            // 0 + 20 (actor delta)
        Assert.Equal(0.50, resolved.Resistances["toxin"]);    // actor beats family
        Assert.Equal(0.30, resolved.Resistances["heat"]);     // role beats family
        Assert.Equal(1.25, resolved.Vulnerable["Crushing"]);  // from the role
        Assert.Equal(12, resolved.Armor);
        Assert.Equal(80, resolved.Resolve);                   // role beats family
        Assert.Contains("family:test", resolved.Tags);
        Assert.Contains("unique:test", resolved.Tags);
        Assert.Equal(0.4, resolved.AvoidRepeatWeight);
        Assert.Equal(2, resolved.Ai.Count);                   // profile rule + inline extra
        Assert.Equal("action:attack", resolved.Ai[0].MoveTag); // profile first, extras after
    }

    [Fact]
    public void AStandaloneActorResolvesExactlyAsAuthored()
    {
        var actor = new ActorDefinition
        {
            Id = "actor.solo", Name = "Solo",
            Attributes = AttributeSet.Uniform(6),
            Resources = new ActorResources { Health = 44, Stamina = 20 },
            Moves = new[] { new MoveGrantSpec { Id = "move.a" } },
            Resolve = 20,
        };

        var resolved = ActorResolver.Resolve(actor, Families(), Roles(), Profiles());

        Assert.Equal(6, resolved.Attributes.Strength);
        Assert.Equal(44, resolved.Resources.Health);
        Assert.Equal(20, resolved.Resolve);
        Assert.Equal(0, resolved.Armor);
        Assert.Equal(1.0, resolved.AvoidRepeatWeight);
        Assert.Empty(resolved.Ai);
    }

    // --- Tag rules and avoid-repeat in a live encounter ------------------------

    private sealed record Harness(CombatEncounter Encounter, TickEngine Tick);

    private static Harness Build(DataStore<MoveDefinition> moves, IRandomSource rng)
    {
        var tick = new TickEngine();
        var bus = new GameEventBus();
        var statuses = new StatusController(
            TestPaths.LoadStore<StatusDefinition>("statuses"), bus, () => tick.CurrentTick);
        var encounter = new CombatEncounter(tick, new HitPipeline(rng), moves, rng, bus, statuses);
        encounter.ConditionWorld = new CombatConditionWorld(encounter);
        return new Harness(encounter, tick);
    }

    private static Combatant TaggedEnemy(double avoidRepeat, params MoveDefinition[] moves) => new(
        "Subject", CombatTeam.Enemy,
        new ResourcePool(ResourceType.Health, 500),
        new ResourcePool(ResourceType.Stamina, 500),
        new ResourcePool(ResourceType.Mana, 0),
        Set(moves),
        () => Attrs(),
        ai: new[] { new AiRuleSpec { MoveTag = "action:attack", Weight = 5 } })
    {
        AvoidRepeatWeight = avoidRepeat,
    };

    /// <summary>One tag rule, two matching moves — both are candidates, and the same seed makes
    /// the same choice every run (the determinism the sim's replay depends on).</summary>
    [Fact]
    public void ATagRuleExpandsToEveryMatchingMove_Deterministically()
    {
        var fast = Move("move.fast", DamageType.Piercing, 2, 1, 2, 4);
        var slow = Move("move.slow", DamageType.Crushing, 8, 4, 4, 8);

        string ChosenFirst()
        {
            var h = Build(Moves(fast, slow), new SeededRandom(77));
            h.Encounter.Start(Player(hp: 4000), new[] { TaggedEnemy(1.0, fast, slow) });
            h.Tick.Advance(2); // decision fires on the first tick after start
            return Assert.Single(h.Encounter.Intents, i => i.Attacker.Team == CombatTeam.Enemy).Move.Id;
        }

        Assert.Equal(ChosenFirst(), ChosenFirst());
    }

    /// <summary>avoid_repeat_weight 0 means never twice running: with two equal moves the enemy
    /// must alternate, whatever the rolls say.</summary>
    [Fact]
    public void AvoidRepeatZeroForcesAlternation()
    {
        var jab = Move("move.jab", DamageType.Piercing, 1, 1, 1, 2);
        var hook = Move("move.hook", DamageType.Crushing, 1, 1, 1, 2);

        var h = Build(Moves(jab, hook), new SeededRandom(5));
        var enemy = TaggedEnemy(0.0, jab, hook);
        h.Encounter.Start(Player(hp: 4000), new[] { enemy });

        // Each distinct ExecuteTick is one decision; collect the first six.
        var seen = new List<string>();
        long lastExecuteTick = -1;
        for (var i = 0; i < 400 && seen.Count < 6; i++)
        {
            h.Tick.Advance(1);
            var intent = h.Encounter.Intents.FirstOrDefault(x => x.Attacker == enemy);
            if (intent is not null && intent.ExecuteTick != lastExecuteTick)
            {
                lastExecuteTick = intent.ExecuteTick;
                seen.Add(intent.Move.Id);
            }
        }

        Assert.True(seen.Count >= 4, $"expected several decisions, saw {seen.Count}");
        for (var i = 1; i < seen.Count; i++)
            Assert.NotEqual(seen[i - 1], seen[i]);
    }

    // --- The shipped Hexer: the framework proof --------------------------------

    /// <summary>The Hexer is pure configuration — family + caster role + library moves. Its
    /// first decision under this seed is the poison opener, chosen by the shared caster brain
    /// because the player carries no poison yet.</summary>
    [Fact]
    public void TheShippedHexerOpensWithVenomBolt()
    {
        var moves = TestPaths.LoadStore<MoveDefinition>("moves");
        var actor = TestPaths.LoadStore<ActorDefinition>("actors").GetById("actor.goblin_hexer");
        var resolved = ActorResolver.Resolve(
            actor,
            TestPaths.LoadStore<EnemyFamilyDefinition>("enemy_families"),
            TestPaths.LoadStore<CombatRoleDefinition>("enemy_roles"),
            TestPaths.LoadStore<AiProfileDefinition>("ai_profiles"));

        Assert.Equal(60, resolved.Resources.Mana); // 0 (goblin) + 40 (caster) + 20 (actor tweak)
        Assert.Equal(0.25, resolved.Resistances["toxin"]); // goblin biology survives the fold

        var conflicts = new MovesetBuilder(moves).Build(
            resolved.Moves.Select(m => new MoveGrant(m, resolved.Name)),
            Array.Empty<MoveModifierGrant>(), out var moveset);
        Assert.Empty(conflicts);

        var h = Build(moves, new FakeRandom(0.01));
        h.Encounter.Start(Player(hp: 4000), new[] { Combatant.FromActor(resolved, moveset) });
        h.Tick.Advance(2);

        var intent = Assert.Single(h.Encounter.Intents, i => i.Attacker.Team == CombatTeam.Enemy);
        Assert.Equal("move.venom_bolt", intent.Move.Id);
    }
}
