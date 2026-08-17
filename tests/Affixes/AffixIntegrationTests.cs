using Dungeons.Affixes;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Presentation;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Affixes;

/// <summary>
/// R4b end to end: fabrication computes a genome, mints innates and rolled modifiers, the
/// reveal card speaks them, and the whole thing survives a save round-trip (v6).
/// </summary>
public class AffixIntegrationTests
{
    private static (FabricationEngine Engine, ContentBundle Content, Inventory Inventory) Harness(int seed = 99)
    {
        var content = new ContentBundle
        {
            Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
            Properties = TestPaths.LoadStore<PropertyDefinition>("properties"),
            Traits = TestPaths.LoadStore<TraitDefinition>("traits"),
            Forms = TestPaths.LoadStore<FormTemplateDefinition>("forms"),
            Equipment = TestPaths.LoadStore<EquipmentDefinition>("equipment"),
            Moves = TestPaths.LoadStore<Dungeons.Combat.MoveDefinition>("moves"),
            Affixes = TestPaths.LoadStore<AffixDefinition>("affixes"),
            ModifierKeys = TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
            MoveModifiers = TestPaths.LoadStore<Dungeons.Combat.MoveModifierDefinition>("move_modifiers"),
        };

        var inventory = new Inventory();
        var engine = new FabricationEngine(
            content, () => inventory, new MaterialProfileResolver(content.Properties),
            new InstanceIdSource(), new SeededRandom(seed));

        inventory.Add("material.iron_ingot", 8);
        inventory.Add("material.leather", 4);

        return (engine, content, inventory);
    }

    private static FabricationRequest Longsword => new("form.longsword", new Dictionary<string, string>
    {
        ["edge"] = "material.iron_ingot",
        ["core"] = "material.iron_ingot",
        ["binding"] = "material.leather",
    });

    [Fact]
    public void FabricationMintsAGenomeInnatesAndRolledModifiers()
    {
        var (engine, _, _) = Harness();
        var outcome = engine.Fabricate(Longsword);

        Assert.True(outcome.Success, outcome.Failure.ToString());
        var item = outcome.Item!;

        Assert.NotNull(item.Genome);
        Assert.Equal("form.longsword", item.Genome!.FormId);
        Assert.True(item.Genome.PressureOf("hardness") > 50, "the edge carries iron's hardness into pressure");

        // Iron is hard enough for the Keen Edge innate — guaranteed, not rolled (D-21).
        Assert.Contains(item.Affixes, a => a.AffixId == "affix.innate_keen_edge");
    }

    /// <summary>§2.2's whole point: same materials, different form, different genome.</summary>
    [Fact]
    public void TheSameMaterialPressesDifferentlyInDifferentForms()
    {
        var (engine, _, _) = Harness();

        var sword = engine.Project(Longsword);
        var vest = engine.Project(new FabricationRequest("form.vest",
            new Dictionary<string, string> { ["shell"] = "material.leather" }));

        Assert.True(sword.Genome.PressureOf("hardness") > vest.Genome.PressureOf("hardness"));
    }

    [Fact]
    public void TheProjectionPromisesInnatesButNeverRolls()
    {
        var (engine, _, _) = Harness();

        var projection = engine.Project(Longsword);
        Assert.Contains(projection.Innates, a => a.AffixId == "affix.innate_keen_edge");

        var outcome = engine.Fabricate(Longsword);

        // Every promised innate is on the minted item, identically.
        foreach (var innate in projection.Innates)
            Assert.Contains(outcome.Item!.Affixes, a => a == innate);
    }

    [Fact]
    public void TheRevealCardSpeaksInnatesAndModifiers()
    {
        var (engine, content, _) = Harness();
        var outcome = engine.Fabricate(Longsword);
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
        var projection = engine.Project(Longsword);

        var supports = ItemReadings.Supports(projection.Genome, content);
        Assert.NotEmpty(supports);

        var line = SemanticFormat.Supports(supports);
        Assert.StartsWith("Supports: ", line);
        Assert.Contains("T", line); // every entry carries its ceiling
    }

    [Fact]
    public void GenomeAndAffixesSurviveTheSaveRoundTrip()
    {
        var (engine, _, inventory) = Harness();
        var outcome = engine.Fabricate(Longsword);
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
        Assert.Equal(minted.Genome!.Pressure["hardness"], restored.Genome!.Pressure["hardness"]);
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
