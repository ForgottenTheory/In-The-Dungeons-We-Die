using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Presentation;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// End to end: a real projection through the real engine, translated into the reading the
/// pre-commit panel renders. Pins the D30 seam — <see cref="CraftProjection"/> now exposes its
/// typed movements and projected profile, and the semantic layer only ever translates them.
/// </summary>
public class CraftReadingTests
{
    private static (ReactionEngine Engine, ContentBundle Content, MaterialProfileResolver Profiles, Inventory Inventory) Harness()
    {
        var content = new ContentBundle
        {
            Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
            Properties = TestPaths.LoadStore<PropertyDefinition>("properties"),
            Processes = TestPaths.LoadStore<ProcessDefinition>("processes"),
            Byproducts = TestPaths.LoadStore<ByproductDefinition>("byproducts"),
            NameGrammar = TestPaths.LoadStore<NameWordDefinition>("name_grammar"),
            Traits = TestPaths.LoadStore<TraitDefinition>("traits"),
            Essences = TestPaths.LoadStore<EssenceDefinition>("essences"),
        };

        var inventory = new Inventory();
        var profiles = new MaterialProfileResolver(content.Properties);

        var engine = new ReactionEngine(
            content,
            () => inventory,
            profiles,
            new EmergentRegistry(content.Materials),
            new NameGenerator(content.Materials, content.Properties, content.NameGrammar),
            new TagDeriver(content.Properties),
            new ByproductResolver(content.Byproducts),
            new TraitResolver(content.Traits),
            _ => 99,
            new SeededRandom(12345));

        return (engine, content, profiles, inventory);
    }

    [Fact]
    public void AProjectionExposesTypedMovementsAndTheReadingTranslatesThem()
    {
        var (engine, content, profiles, inventory) = Harness();
        inventory.Add("material.iron_ingot", 10);
        inventory.Add("material.ember_core", 10);

        var projection = engine.Project(new CraftRequest(
            "process.forge_infusion", "material.iron_ingot", new[] { "material.ember_core" }));

        Assert.True(projection.CanCraft, projection.Failure.ToString());
        Assert.NotEmpty(projection.StepResults);
        Assert.NotNull(projection.Projected);

        var iron = content.Materials.GetById("material.iron_ingot");
        var reading = CraftReadings.From(projection, iron.Name, profiles.Resolve(iron), content);

        // Forge infusion drives heat into cold iron — the reading must say so without numbers.
        Assert.Contains(reading.Strengthening, m => m.Property == "heat" && m.Trend is Trend.Rising or Trend.Emerging);
        Assert.Equal(Risk.Of(projection.Integrity), reading.Risk);

        var glossary = new PropertyGlossary(content.Properties);
        var text = SemanticFormat.Projection(reading, glossary);

        Assert.Contains("Aiming at:", text);
        Assert.Contains("▲ Heat", text);
        Assert.Contains("Risk:", text);
    }

    /// <summary>Same projection, same reading — the semantic layer is deterministic (D30).</summary>
    [Fact]
    public void TheReadingIsDeterministic()
    {
        var (engine, content, profiles, inventory) = Harness();
        inventory.Add("material.iron_ingot", 10);
        inventory.Add("material.ember_core", 10);

        var request = new CraftRequest(
            "process.forge_infusion", "material.iron_ingot", new[] { "material.ember_core" });
        var iron = content.Materials.GetById("material.iron_ingot");
        var glossary = new PropertyGlossary(content.Properties);

        string Render() => SemanticFormat.Projection(
            CraftReadings.From(engine.Project(request), iron.Name, profiles.Resolve(iron), content),
            glossary);

        Assert.Equal(Render(), Render());
    }

    [Fact]
    public void AFailedProjectionReadsAsItsFailure()
    {
        var (engine, content, profiles, _) = Harness();
        var iron = content.Materials.GetById("material.iron_ingot");

        var projection = engine.Project(new CraftRequest(
            "process.steep", "material.iron_ingot", new[] { "material.ember_core" }, Quantity: 0));
        var reading = CraftReadings.From(projection, iron.Name, profiles.Resolve(iron), content);

        Assert.False(reading.CanCraft);
        Assert.Empty(reading.Strengthening);
    }
}
