using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Tests;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// Validates the shipped crafting content: interactions reference real materials
/// and real professions, and the flagship Barkbound Iron interaction actually
/// produces its result once inputs and knowledge are supplied.
/// </summary>
public class CraftingContentValidationTests
{
    private static DataStore<T> Load<T>(string subfolder) where T : IDefinition
    {
        return TestPaths.LoadStore<T>(subfolder);
    }

    [Fact]
    public void InteractionsReferenceKnownMaterialsAndProfessions()
    {
        var interactions = Load<CraftingInteractionDefinition>("crafting_interactions");
        var materials = Load<MaterialDefinition>("materials");
        var consumables = Load<Dungeons.Combat.ConsumableDefinition>("consumables");
        var professions = Load<Dungeons.Professions.ProfessionDefinition>("professions");

        Assert.True(interactions.Count >= 1);

        foreach (var interaction in interactions.GetAll())
        {
            foreach (var input in interaction.Inputs)
                Assert.True(materials.Contains(input.ItemId), $"{interaction.Id} input {input.ItemId} missing");
            // A result may be a material or a consumable item.
            Assert.True(materials.Contains(interaction.ResultItemId) || consumables.Contains(interaction.ResultItemId),
                $"{interaction.Id} result {interaction.ResultItemId} missing");
            foreach (var req in interaction.ProfessionRequirements)
                Assert.True(professions.Contains(req.ProfessionId), $"{interaction.Id} requires unknown profession {req.ProfessionId}");
        }
    }

    /// <summary>
    /// The Healing Salve is the last fixed interaction standing. It survives only because
    /// consumables are produced by fabrication (P5c) and there is no emergent path to one yet
    /// — everything else was retired when the reaction engine replaced recipe matching.
    /// </summary>
    [Fact]
    public void HealingSalve_IsStillBrewable_UntilFabricationLands()
    {
        var interactions = Load<CraftingInteractionDefinition>("crafting_interactions");
        var inventory = new Inventory();
        var system = new CraftingExperimentSystem(
            interactions, Load<MaterialDefinition>("materials"), inventory, new DiscoverySystem(), _ => 99);

        var interaction = interactions.GetById("interaction.healing_salve");
        foreach (var input in interaction.Inputs)
            inventory.Add(input.ItemId, input.Quantity);

        var outcome = system.Experiment(interaction.Inputs.Select(i => i.ItemId).ToArray());

        Assert.True(outcome.Success);
        Assert.Equal("consumable.healing_salve", outcome.ResultItemId);
    }

    /// <summary>
    /// Barkbound Iron was the prototype the emergent system replaces. Its fixed recipe is gone
    /// — that combination now goes through the reaction algebra like any other, and hardcoding
    /// it would be exactly the recipe table the design rejects (§0 Decision 1).
    /// </summary>
    [Fact]
    public void OnlyTheConsumableShimRemains()
    {
        var interactions = Load<CraftingInteractionDefinition>("crafting_interactions");

        Assert.False(interactions.Contains("interaction.barkbound_iron"));
        Assert.Equal(1, interactions.Count);
    }
}
