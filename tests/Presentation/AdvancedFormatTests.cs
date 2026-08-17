using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// The §2F numeric voice — the old player-facing wording of <c>CraftFormat</c>, preserved
/// verbatim behind the Advanced toggle. The theorycrafter keeps every number the player used
/// to be forced to read.
/// </summary>
public class AdvancedFormatTests
{
    private static CraftPreview Projection(
        double cost = 12,
        double spread = 0,
        int remaining = 78,
        double destructionChance = 0,
        string name = "Emberlit Iron",
        bool firstDiscovery = false) =>
        new(
            CraftFailure.None,
            new WorkabilityProjection(cost, spread, remaining, destructionChance),
            ProjectedMaterialStrength: 49,
            ProjectedName: name,
            WouldBeFirstDiscovery: firstDiscovery,
            Preview: ReactionLog.Empty);

    [Fact]
    public void TheNumericSummaryKeepsResultCostAndRemainingIntegrity()
    {
        var text = AdvancedFormat.Projection(Projection(), "Iron Ingot");

        Assert.Contains("Emberlit Iron", text);
        Assert.Contains("Potency 49", text);
        Assert.Contains("Integrity → 78", text);
        Assert.Contains("cost 12", text);
        Assert.DoesNotContain("⚠", text);
    }

    [Fact]
    public void CertainDestructionIsStillStatedOutright()
    {
        var text = AdvancedFormat.Projection(
            Projection(cost: 40, remaining: 0, destructionChance: 1.0), "Tempestforged Iron");

        Assert.Contains("⚠", text);
        Assert.Contains("DESTROY", text);
        Assert.Contains("byproducts", text);
        Assert.DoesNotContain("%", text);
    }

    [Fact]
    public void RiskKeepsItsPercentageAndSpread()
    {
        var text = AdvancedFormat.Projection(
            Projection(cost: 18, spread: 10, remaining: 2, destructionChance: 0.35), "Emberlit Iron");

        Assert.Contains("35% chance of destroying", text);
        Assert.Contains("± 10", text);
    }

    [Fact]
    public void ProcessAndChannelKeepTheirNumbers()
    {
        var processes = TestPaths.LoadStore<CraftingActionDefinition>("processes");

        var forge = AdvancedFormat.Process(processes.GetById("process.forge_infusion"), "Smithing");
        Assert.Contains("severity 0.55", forge);
        Assert.Contains("Smithing L15", forge);

        var channel = AdvancedFormat.AffectedQualities(processes.GetById("process.forge_infusion"));
        Assert.Contains("heat 0.8", channel);
    }

    [Fact]
    public void TheNumericMaterialSummaryKeepsMetaFieldsAndTags()
    {
        var properties = TestPaths.LoadStore<PropertyDefinition>("properties");
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");
        var iron = materials.GetById("material.iron_ingot");

        var text = AdvancedFormat.Material(iron, new MaterialStateResolver(properties).StateOf(iron));

        Assert.Contains("potency", text);
        Assert.Contains("integrity", text);
        Assert.Contains("form:metal", text);
    }
}
