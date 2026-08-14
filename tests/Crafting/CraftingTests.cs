using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Crafting;

public class CraftingTests
{
    private static DataStore<CraftingInteractionDefinition> Interactions(params CraftingInteractionDefinition[] items)
    {
        var store = new DataStore<CraftingInteractionDefinition>();
        foreach (var item in items)
            store.Add(item);
        return store;
    }

    private static DataStore<MaterialDefinition> Materials(params MaterialDefinition[] items)
    {
        var store = new DataStore<MaterialDefinition>();
        foreach (var item in items)
            store.Add(item);
        return store;
    }

    private static CraftingInteractionDefinition Barkbound(int herbloreLevel = 2) => new()
    {
        Id = "interaction.barkbound_iron",
        Name = "Barkbound Iron",
        Inputs = new[] { new ItemStack("material.iron_ingot", 1), new ItemStack("material.oak_bark", 1) },
        ProfessionRequirements = new[] { new ProfessionRequirement { ProfessionId = "profession.herblore", Level = herbloreLevel } },
        ResultItemId = "material.barkbound_iron",
        ResultQuantity = 1,
        DiscoveryId = "discovery.barkbound_iron",
    };

    private static readonly MaterialDefinition BarkboundMaterial = new()
    {
        Id = "material.barkbound_iron",
        Name = "Barkbound Iron",
        Properties = new[] { new MaterialProperty { Property = "toxin_resistance", Value = 0.05 } },
    };

    private static (CraftingExperimentSystem sys, Inventory inv, DiscoverySystem disc) Build(int herbloreLevel)
    {
        var inv = new Inventory();
        var disc = new DiscoverySystem();
        var sys = new CraftingExperimentSystem(
            Interactions(Barkbound()),
            Materials(BarkboundMaterial),
            inv,
            disc,
            professionLevel: _ => herbloreLevel);
        return (sys, inv, disc);
    }

    [Fact]
    public void Experiment_Succeeds_ConsumesInputs_ProducesResult_RecordsDiscovery()
    {
        var (sys, inv, disc) = Build(herbloreLevel: 2);
        inv.Add("material.iron_ingot", 1);
        inv.Add("material.oak_bark", 1);

        var outcome = sys.Experiment(new[] { "material.iron_ingot", "material.oak_bark" });

        Assert.True(outcome.Success);
        Assert.True(outcome.WasNewDiscovery);
        Assert.Equal("material.barkbound_iron", outcome.ResultItemId);
        Assert.Equal(0, inv.GetQuantity("material.iron_ingot"));
        Assert.Equal(0, inv.GetQuantity("material.oak_bark"));
        Assert.Equal(1, inv.GetQuantity("material.barkbound_iron"));
        Assert.True(disc.IsDiscovered("discovery.barkbound_iron"));
        Assert.Contains(outcome.ResultProperties, p => p.Property == "toxin_resistance");
    }

    [Fact]
    public void Experiment_SecondTime_IsNotANewDiscovery()
    {
        var (sys, inv, _) = Build(herbloreLevel: 2);
        inv.Add("material.iron_ingot", 2);
        inv.Add("material.oak_bark", 2);

        var first = sys.Experiment(new[] { "material.iron_ingot", "material.oak_bark" });
        var second = sys.Experiment(new[] { "material.iron_ingot", "material.oak_bark" });

        Assert.True(first.WasNewDiscovery);
        Assert.True(second.Success);
        Assert.False(second.WasNewDiscovery); // recipe now known, just re-crafted
        Assert.Equal(2, inv.GetQuantity("material.barkbound_iron"));
    }

    [Fact]
    public void Experiment_ProfessionTooLow_FailsAndConsumesNothing()
    {
        var (sys, inv, disc) = Build(herbloreLevel: 1); // needs 2
        inv.Add("material.iron_ingot", 1);
        inv.Add("material.oak_bark", 1);

        var outcome = sys.Experiment(new[] { "material.iron_ingot", "material.oak_bark" });

        Assert.False(outcome.Success);
        Assert.Equal(ExperimentFailure.ProfessionTooLow, outcome.Failure);
        Assert.Equal("profession.herblore", outcome.UnmetProfessionId);
        Assert.Equal(2, outcome.UnmetRequiredLevel);
        Assert.Equal(1, inv.GetQuantity("material.iron_ingot")); // untouched
        Assert.False(disc.IsDiscovered("discovery.barkbound_iron"));
    }

    [Fact]
    public void Experiment_MissingInputs_Fails()
    {
        var (sys, inv, _) = Build(herbloreLevel: 2);
        inv.Add("material.iron_ingot", 1); // no oak bark in inventory

        var outcome = sys.Experiment(new[] { "material.iron_ingot", "material.oak_bark" });

        Assert.False(outcome.Success);
        Assert.Equal(ExperimentFailure.MissingInputs, outcome.Failure);
    }

    [Fact]
    public void Experiment_NoMatchingInteraction_Fails()
    {
        var (sys, _, _) = Build(herbloreLevel: 2);
        var outcome = sys.Experiment(new[] { "material.oak_log", "material.sageleaf" });
        Assert.Equal(ExperimentFailure.NoMatch, outcome.Failure);
    }

    [Fact]
    public void DiscoverySystem_RecordsOnce_AndRaisesEvent()
    {
        var disc = new DiscoverySystem();
        var fired = 0;
        disc.Discovered += _ => fired++;

        Assert.True(disc.Record("discovery.x"));
        Assert.False(disc.Record("discovery.x"));
        Assert.Equal(1, fired);
        Assert.Equal(1, disc.Count);
    }

    [Fact]
    public void MaterialProperties_ParseFromJson_AndItemStackInputsDeserialize()
    {
        // Verifies System.Text.Json reads material properties and ItemStack inputs.
        var materials = new DataStore<MaterialDefinition>();
        materials.LoadOne("""
            {
              "id": "material.oak_bark",
              "name": "Oak Bark",
              "tags": ["plant", "oak"],
              "properties": [ { "property": "toxin_resistance", "value": 0.05 } ]
            }
            """);
        Assert.Equal(0.05, materials.GetById("material.oak_bark").GetProperty("toxin_resistance"));

        var interactions = new DataStore<CraftingInteractionDefinition>();
        interactions.LoadOne("""
            {
              "id": "interaction.test",
              "name": "Test",
              "inputs": [ { "itemId": "material.iron_ingot", "quantity": 2 } ],
              "resultItemId": "material.barkbound_iron"
            }
            """);
        var loaded = interactions.GetById("interaction.test");
        Assert.Single(loaded.Inputs);
        Assert.Equal("material.iron_ingot", loaded.Inputs[0].ItemId);
        Assert.Equal(2, loaded.Inputs[0].Quantity);
    }
}
