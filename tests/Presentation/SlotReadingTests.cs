using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// §2E contextual meaning for fabrication — why a material suits a slot — derived from the
/// form's own stat_map, apertures and tag gates against real shipped content.
/// </summary>
public class SlotReadingTests
{
    private static readonly DataStore<PropertyDefinition> Properties =
        TestPaths.LoadStore<PropertyDefinition>("properties");

    private static readonly DataStore<MaterialDefinition> Materials =
        TestPaths.LoadStore<MaterialDefinition>("materials");

    private static readonly DataStore<EquipmentBlueprintDefinition> Forms =
        TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms");

    private static readonly DataStore<TraitDefinition> Traits =
        TestPaths.LoadStore<TraitDefinition>("traits");

    private static SlotReading Read(string formId, string slot, string materialId)
    {
        var materialStates = new MaterialStateResolver(Properties);
        var material = Materials.GetById(materialId);

        return SlotReadings.For(Forms.GetById(formId), slot, material, materialStates.StateOf(material), Traits);
    }

    [Fact]
    public void IronIsEligibleForTheEdgeAndTheReadingSaysWhy()
    {
        var reading = Read("form.longsword", "edge", "material.iron_ingot");

        Assert.True(reading.Eligible);
        Assert.Equal("form:metal", reading.EligibleVia);

        // The longsword edge reads hardness heavily, and iron answers with a Strong tier —
        // that pairing is the whole "strong structural fit for this blade edge" sentence.
        var hardness = reading.Reads.Single(r => r.Property == "hardness" && !r.SharedAcrossSlots);
        Assert.Equal(ReadWeight.Heavy, hardness.Weight);
        Assert.True(hardness.MaterialTier >= PropertyTier.Strong);
    }

    [Fact]
    public void IronIsRejectedByTheBindingByTagLaw()
    {
        var reading = Read("form.longsword", "binding", "material.iron_ingot");

        Assert.False(reading.Eligible);
        Assert.Null(reading.EligibleVia);
    }

    /// <summary>A shared ("*") stat read weighs a slot by its mass share — the edge carries
    /// most of a longsword's mass, the binding almost none.</summary>
    [Fact]
    public void SharedReadsScaleWithMassShare()
    {
        var edge = Read("form.longsword", "edge", "material.iron_ingot");
        var edgeMass = edge.Reads.Single(r => r.Property == "mass" && r.SharedAcrossSlots);
        Assert.Equal(ReadWeight.Heavy, edgeMass.Weight); // 1.0 × 0.60 share

        var core = Read("form.longsword", "core", "material.iron_ingot");
        var coreMass = core.Reads.Single(r => r.Property == "mass" && r.SharedAcrossSlots);
        Assert.True(coreMass.Weight < ReadWeight.Heavy); // 1.0 × 0.25 share
    }

    [Fact]
    public void ApertureBandsFollowTheTuningBoundaries()
    {
        Assert.Equal(TraitExpressionBand.Full, SlotReadings.ApertureBandOf(1.0));
        Assert.Equal(TraitExpressionBand.Partial, SlotReadings.ApertureBandOf(0.5));
        Assert.Equal(TraitExpressionBand.Muted, SlotReadings.ApertureBandOf(0.2));
    }
}
