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
        var store = new DataStore<T>();
        foreach (var file in Directory.GetFiles(Path.Combine(TestPaths.DataDir, subfolder), "*.json"))
            store.LoadOne(File.ReadAllText(file));
        return store;
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
}
