using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Tests;
using Xunit;

namespace Dungeons.Tests.Combat;

/// <summary>
/// Validates the shipped combat content: actors grant real moves and loot materials, moves have
/// sane timing and namespaced tags, and the JSON (AttributeSet, nested timing, packets,
/// resources) deserializes as expected.
/// </summary>
public class CombatContentValidationTests
{
    private static DataStore<T> Load<T>(string subfolder) where T : IDefinition
    {
        return TestPaths.LoadStore<T>(subfolder);
    }

    [Fact]
    public void ActorsReferenceKnownMovesAndLoot()
    {
        var actors = Load<ActorDefinition>("actors");
        var moves = Load<MoveDefinition>("moves");
        var materials = Load<MaterialDefinition>("materials");

        Assert.True(actors.Count >= 2); // Goblin Raider + Brute

        foreach (var actor in actors.GetAll())
        {
            Assert.NotEmpty(actor.Moves);
            foreach (var grant in actor.Moves)
                Assert.True(moves.Contains(grant.Id), $"{actor.Id} grants unknown move {grant.Id}");
            if (!string.IsNullOrEmpty(actor.LootItemId))
                Assert.True(materials.Contains(actor.LootItemId!), $"{actor.Id} drops unknown material {actor.LootItemId}");
            Assert.True(actor.Resources.Health > 0);
        }
    }

    [Fact]
    public void MovesHaveForwardTimingAndParseNestedFields()
    {
        var moves = Load<MoveDefinition>("moves");
        Assert.True(moves.Count >= 8);

        foreach (var move in moves.GetAll())
        {
            Assert.True(move.Timing.TimeToImpactTicks >= 1, $"{move.Id} has no time-to-impact");
            Assert.True(move.Timing.RecoveryTicks >= 0);
            Assert.NotEmpty(move.Tags);
        }

        // The packet shape deserializes: Fireball is Magic with a heat aspect.
        var fireball = moves.GetById("move.fireball");
        var packet = Assert.Single(fireball.Packets);
        Assert.Equal(DamageType.Magic, packet.Type);
        Assert.Equal("heat", packet.Aspect);
        Assert.Equal(0.2, Assert.Single(fireball.Effects).Chance, 3);
    }

    [Fact]
    public void GoblinBrute_HasLongTelegraph_ForReadability()
    {
        var moves = Load<MoveDefinition>("moves");

        // Post-framework (M2′c) the Brute's strength composes: goblin 7 + brute role +5.
        var brute = ResolveShipped("actor.goblin_brute");
        Assert.Equal(12, brute.Attributes.Strength);
        var smash = moves.GetById(brute.Moves[0].Id);
        // The Brute's whole point: a big readable wind-up.
        Assert.True(smash.Timing.TimeToImpactTicks >= 40);
    }

    private static ResolvedActor ResolveShipped(string actorId) =>
        ActorResolver.Resolve(
            Load<ActorDefinition>("actors").GetById(actorId),
            Load<EnemyFamilyDefinition>("enemy_families"),
            Load<CombatRoleDefinition>("enemy_roles"),
            Load<AiProfileDefinition>("ai_profiles"));

    [Fact]
    public void ShippedActorVulnerabilities_AreValidDamageTypesInRange()
    {
        // D-02 moved per-type weakness from player gear onto the enemy. The failure mode is
        // authoring a *lane* here ("physical") instead of a damage type, which would silently
        // never match — hence a closed-vocabulary check rather than a free-form map.
        foreach (var actor in Load<ActorDefinition>("actors").GetAll())
        {
            foreach (var (type, multiplier) in actor.Vulnerable)
            {
                Assert.True(Enum.TryParse<DamageType>(type, ignoreCase: true, out _),
                    $"{actor.Id} is vulnerable to '{type}', which is not a damage type");
                Assert.InRange(multiplier, CombatTuning.MinVulnerability, CombatTuning.MaxVulnerability);
            }

            foreach (var lane in actor.Resistances.Keys)
                Assert.True(DamageLanes.All.Contains(lane), $"{actor.Id} resists unknown lane '{lane}'");
        }
    }

    [Fact]
    public void TheGoblinBrute_IsSoftToCrushingAndToughAgainstSlashing()
    {
        // The content that makes the Fighter's "swap to the weapon that counters it" identity
        // real, now that the three physical resistances collapsed into one lane. Post-M2′c the
        // pair rides role.brute, so it is asserted on the resolved actor.
        var brute = ResolveShipped("actor.goblin_brute");

        Assert.True(brute.Vulnerable["Crushing"] > 1.0);
        Assert.True(brute.Vulnerable["Slashing"] < 1.0);
    }

    [Fact]
    public void ShippedEquipmentResistances_AreKeyedByLane()
    {
        // The E1 migration hazard: "Slashing": 0.15 used to work and now resists nothing.
        foreach (var equipment in Load<Dungeons.Items.EquipmentDefinition>("equipment").GetAll())
            foreach (var lane in equipment.Armor?.Resistances.Keys ?? Enumerable.Empty<string>())
                Assert.True(DamageLanes.All.Contains(lane),
                    $"{equipment.Id} resists '{lane}', which is a damage type name, not a lane");
    }
}
