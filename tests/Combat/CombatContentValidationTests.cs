using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Tests;
using Xunit;

namespace Dungeons.Tests.Combat;

/// <summary>
/// Validates the shipped combat content: actors reference real abilities and loot
/// materials, abilities have sane timing, and the JSON (AttributeSet, nested timing,
/// resources) deserializes as expected.
/// </summary>
public class CombatContentValidationTests
{
    private static DataStore<T> Load<T>(string subfolder) where T : IDefinition
    {
        return TestPaths.LoadStore<T>(subfolder);
    }

    [Fact]
    public void ActorsReferenceKnownAbilitiesAndLoot()
    {
        var actors = Load<ActorDefinition>("actors");
        var abilities = Load<AbilityDefinition>("abilities");
        var materials = Load<MaterialDefinition>("materials");

        Assert.True(actors.Count >= 2); // Goblin Raider + Brute
        Assert.True(abilities.Contains("ability.strike")); // player basic attack

        foreach (var actor in actors.GetAll())
        {
            Assert.NotEmpty(actor.AbilityIds);
            foreach (var abilityId in actor.AbilityIds)
                Assert.True(abilities.Contains(abilityId), $"{actor.Id} references unknown ability {abilityId}");
            if (!string.IsNullOrEmpty(actor.LootItemId))
                Assert.True(materials.Contains(actor.LootItemId!), $"{actor.Id} drops unknown material {actor.LootItemId}");
            Assert.True(actor.Resources.Health > 0);
        }
    }

    [Fact]
    public void AbilitiesHaveForwardTimingAndParseNestedFields()
    {
        var abilities = Load<AbilityDefinition>("abilities");
        foreach (var ability in abilities.GetAll())
        {
            Assert.True(ability.Timing.TimeToImpactTicks >= 1, $"{ability.Id} has no time-to-impact");
            Assert.True(ability.Timing.RecoveryTicks >= 0);
            Assert.True(ability.BaseValue > 0);
        }
    }

    [Fact]
    public void GoblinBrute_HasLongTelegraph_ForReadability()
    {
        var actors = Load<ActorDefinition>("actors");
        var abilities = Load<AbilityDefinition>("abilities");

        var brute = actors.GetById("actor.goblin_brute");
        Assert.Equal(12, brute.Attributes.Strength); // AttributeSet deserialized
        var smash = abilities.GetById(brute.AbilityIds[0]);
        // The Brute's whole point: a big readable wind-up.
        Assert.True(smash.Timing.TimeToImpactTicks >= 40);
    }

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
        // real, now that the three physical resistances collapsed into one lane.
        var brute = Load<ActorDefinition>("actors").GetById("actor.goblin_brute");

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
