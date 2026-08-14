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
        var store = new DataStore<T>();
        foreach (var file in Directory.GetFiles(Path.Combine(TestPaths.DataDir, subfolder), "*.json"))
            store.LoadOne(File.ReadAllText(file));
        return store;
    }

    [Fact]
    public void InteractionsReferenceKnownMaterialsAndProfessions()
    {
        var interactions = Load<CraftingInteractionDefinition>("crafting_interactions");
        var materials = Load<MaterialDefinition>("materials");
        var professions = Load<Dungeons.Professions.ProfessionDefinition>("professions");

        Assert.True(interactions.Count >= 1);

        foreach (var interaction in interactions.GetAll())
        {
            foreach (var input in interaction.Inputs)
                Assert.True(materials.Contains(input.ItemId), $"{interaction.Id} input {input.ItemId} missing");
            Assert.True(materials.Contains(interaction.ResultItemId), $"{interaction.Id} result {interaction.ResultItemId} missing");
            foreach (var req in interaction.ProfessionRequirements)
                Assert.True(professions.Contains(req.ProfessionId), $"{interaction.Id} requires unknown profession {req.ProfessionId}");
        }
    }

    [Fact]
    public void BarkboundIron_CraftsAndCarriesDerivedProperty()
    {
        var interactions = Load<CraftingInteractionDefinition>("crafting_interactions");
        var materials = Load<MaterialDefinition>("materials");
        var inventory = new Inventory();
        var discoveries = new DiscoverySystem();

        // Provide enough knowledge to satisfy any requirement.
        var system = new CraftingExperimentSystem(interactions, materials, inventory, discoveries, _ => 99);

        var interaction = interactions.GetById("interaction.barkbound_iron");
        foreach (var input in interaction.Inputs)
            inventory.Add(input.ItemId, input.Quantity);

        var outcome = system.Experiment(interaction.Inputs.Select(i => i.ItemId).ToArray());

        Assert.True(outcome.Success);
        Assert.True(outcome.WasNewDiscovery);
        Assert.Equal("material.barkbound_iron", outcome.ResultItemId);
        Assert.Contains(outcome.ResultProperties, p => p.Property == "toxin_resistance" && p.Value > 0);
    }
}
