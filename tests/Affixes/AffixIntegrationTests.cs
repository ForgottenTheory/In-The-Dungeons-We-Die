using Dungeons.Affixes;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Presentation;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Affixes;

/// <summary>
/// R4b end to end: fabrication computes an item potential, mints innates and rolled modifiers, the
/// reveal card speaks them, and the whole thing survives a save round-trip (v6).
/// </summary>
public class AffixIntegrationTests
{
    private static (EquipmentAssemblyEngine Engine, ContentBundle Content, Inventory Inventory) Harness(int seed = 99)
    {
        var content = new ContentBundle
        {
            Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
            Properties = TestPaths.LoadStore<PropertyDefinition>("properties"),
            Traits = TestPaths.LoadStore<TraitDefinition>("traits"),
            Forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms"),
            Equipment = TestPaths.LoadStore<EquipmentDefinition>("equipment"),
            Moves = TestPaths.LoadStore<Dungeons.Combat.MoveDefinition>("moves"),
            Affixes = TestPaths.LoadStore<AffixDefinition>("affixes"),
            ModifierKeys = TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
            MoveModifiers = TestPaths.LoadStore<Dungeons.Combat.MoveModifierDefinition>("move_modifiers"),
        };

        var inventory = new Inventory();
        var engine = new EquipmentAssemblyEngine(
            content, () => inventory, new MaterialStateResolver(content.Properties),
            new InstanceIdSource(), new SeededRandom(seed));

        inventory.Add("material.iron_ingot", 8);
        inventory.Add("material.leather", 4);

        return (engine, content, inventory);
    }

    private static EquipmentAssemblyRequest Longsword => new("form.longsword", new Dictionary<string, string>
    {
        ["edge"] = "material.iron_ingot",
        ["core"] = "material.iron_ingot",
        ["binding"] = "material.leather",
    });

    [Fact]
    public void FabricationMintsAGenomeInnatesAndRolledModifiers()
    {
        var (engine, _, _) = Harness();
        var outcome = engine.Assemble(Longsword);

        Assert.True(outcome.Success, outcome.Failure.ToString());
        var item = outcome.Item!;

        Assert.NotNull(item.Potential);
        Assert.Equal("form.longsword", item.Potential!.BlueprintId);
        Assert.True(item.Potential.InfluenceOf("hardness") > 50, "the edge carries iron's hardness into materialInfluence");

        // Iron is hard enough for the Keen Edge innate — guaranteed, not rolled (D-21).
        Assert.Contains(item.Affixes, a => a.AffixId == "affix.innate_keen_edge");
    }

    /// <summary>§2.2's whole point: same materials, different form, different item potential.</summary>
    [Fact]
    public void TheSameMaterialPressesDifferentlyInDifferentForms()
    {
        var (engine, _, _) = Harness();

        var sword = engine.Preview(Longsword);
        var vest = engine.Preview(new EquipmentAssemblyRequest("form.vest",
            new Dictionary<string, string> { ["shell"] = "material.leather" }));

        Assert.True(sword.Potential.InfluenceOf("hardness") > vest.Potential.InfluenceOf("hardness"));
    }

    [Fact]
    public void TheProjectionPromisesInnatesButNeverRolls()
    {
        var (engine, _, _) = Harness();

        var projection = engine.Preview(Longsword);
        Assert.Contains(projection.Innates, a => a.AffixId == "affix.innate_keen_edge");

        var outcome = engine.Assemble(Longsword);

        // Every promised innate is on the minted item, identically.
        foreach (var innate in projection.Innates)
            Assert.Contains(outcome.Item!.Affixes, a => a == innate);
    }

    [Fact]
    public void TheRevealCardSpeaksInnatesAndModifiers()
    {
        var (engine, content, _) = Harness();
        var outcome = engine.Assemble(Longsword);
        var definition = content.Equipment.GetById(outcome.Item!.BaseDefinitionId);

        var card = SemanticFormat.Item(ItemReadings.From(outcome.Item, definition, content));

        Assert.Contains("Innate: ", card);
        Assert.Contains("critical chance", card); // Keen Edge's line, $roll substituted
        Assert.DoesNotContain("$roll", card);     // §8 parity: never raw
    }

    [Fact]
    public void ThePreviewTranslatesTheGenomeIntoSupportedFamilies()
    {
        var (engine, content, _) = Harness();
        var projection = engine.Preview(Longsword);

        var supports = ItemReadings.Supports(projection.Potential, content);
        Assert.NotEmpty(supports);

        var line = SemanticFormat.Supports(supports);
        Assert.StartsWith("Supports: ", line);
        Assert.Contains("T", line); // every entry carries its ceiling
    }

    [Fact]
    public void GenomeAndAffixesSurviveTheSaveRoundTrip()
    {
        var (engine, _, inventory) = Harness();
        var outcome = engine.Assemble(Longsword);
        var minted = outcome.Item!;

        var save = Dungeons.Persistence.SaveMapper.Capture(
            null, inventory,
            new Dungeons.Professions.ProfessionSystem(
                new DataStore<Dungeons.Professions.ProfessionActionDefinition>(), inventory, new SeededRandom(1)),
            new DiscoverySystem(), new Dictionary<string, int>(),
            savedAtTick: 1);

        var loaded = new Dungeons.Persistence.SaveSerializer()
            .Deserialize(new Dungeons.Persistence.SaveSerializer().Serialize(save));

        var restored = loaded.StashInstances.Single(i => i.InstanceId == minted.InstanceId);
        Assert.NotNull(restored.Genome);
        Assert.Equal(minted.Potential!.MaterialInfluence["hardness"], restored.Genome!.Pressure["hardness"]);
        Assert.Equal(minted.Affixes.Count, restored.Affixes.Count);
        Assert.Equal(minted.Affixes[0].AffixId, restored.Affixes[0].AffixId);
        Assert.Equal(minted.Affixes[0].Roll, restored.Affixes[0].Roll);
    }

    [Fact]
    public void TheShippedAffixContentValidatesClean()
    {
        var (_, content, _) = Harness();
        var problems = ContentValidator.Validate(content)
            .Where(p => p.Category == "affix")
            .ToList();

        Assert.Empty(problems);
    }
}
