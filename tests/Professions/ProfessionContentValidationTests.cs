using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Professions;

/// <summary>
/// Validates the real shipped profession content: every action points at a known
/// profession, every referenced item id exists as a material, and every action can
/// actually execute once inputs are supplied. Broken content fails loudly here
/// (docs/json-schema.md §21).
/// </summary>
public class ProfessionContentValidationTests
{
    private static DataStore<T> Load<T>(string subfolder) where T : IDefinition
    {
        return TestPaths.LoadStore<T>(subfolder);
    }

    [Fact]
    public void ActionsReferenceKnownProfessionsAndMaterials()
    {
        var professions = Load<ProfessionDefinition>("professions");
        var actions = Load<ProfessionActionDefinition>("profession_actions");
        var materials = Load<MaterialDefinition>("materials");

        Assert.True(professions.Count >= 3); // docs/vertical-slice.md §6
        Assert.True(actions.Count >= 3);

        foreach (var action in actions.GetAll())
        {
            Assert.True(professions.Contains(action.ProfessionId), $"{action.Id} references unknown profession {action.ProfessionId}");

            foreach (var io in action.Inputs.Concat(action.Outputs))
                Assert.True(materials.Contains(io.ItemId), $"{action.Id} references unknown material {io.ItemId}");
            foreach (var bonus in action.BonusOutputs)
                Assert.True(materials.Contains(bonus.ItemId), $"{action.Id} references unknown material {bonus.ItemId}");
        }
    }

    [Fact]
    public void EveryActionExecutesOnceInputsAreProvided()
    {
        var actions = Load<ProfessionActionDefinition>("profession_actions");
        var inventory = new Inventory();
        var system = new ProfessionSystem(actions, inventory, new SeededRandom(1));

        foreach (var action in actions.GetAll())
        {
            // Supply any required inputs, then execute.
            foreach (var input in action.Inputs)
                inventory.Add(input.ItemId, input.Quantity);

            var outcome = system.Execute(action.Id);
            Assert.True(outcome.Success, $"{action.Id} failed with {outcome.Failure}");
        }
    }

    [Fact]
    public void FlatJson_BindsToUnifiedItemStackAndItemChance()
    {
        // Guards the unified item+quantity shapes: the flat JSON
        // ({ itemId, quantity } / { itemId, chance, quantity }) must still bind to
        // ItemStack / ItemChance value types, quantities and chances included.
        var actions = Load<ProfessionActionDefinition>("profession_actions");

        var chop = actions.GetById("action.chop_oak");
        var log = Assert.Single(chop.Outputs);
        Assert.Equal("material.oak_log", log.ItemId);
        Assert.Equal(1, log.Quantity);

        var bark = Assert.Single(chop.BonusOutputs);
        Assert.Equal("material.oak_bark", bark.ItemId);
        Assert.Equal(0.25, bark.Chance);
        Assert.Equal(1, bark.Quantity);
        Assert.Equal(new Dungeons.Items.ItemStack("material.oak_bark", 1), bark.Stack);

        var smelt = actions.GetById("action.smelt_iron");
        var ore = Assert.Single(smelt.Inputs);
        Assert.Equal("material.iron_ore", ore.ItemId);
        Assert.Equal(2, ore.Quantity);
    }
}
