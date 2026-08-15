using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Tests;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// P0 of the emergent item system: every property in <see cref="ItemProperties"/> is backed
/// by a loaded <see cref="PropertyDefinition"/> with the correct role, and derived resistance
/// (§2.2) respects authored overrides. No reaction engine here — this is plumbing.
/// </summary>
public class PropertySystemTests
{
    private static DataStore<PropertyDefinition> LoadProperties()
    {
        var store = new DataStore<PropertyDefinition>();
        store.LoadDocuments(
            Directory.GetFiles(Path.Combine(TestPaths.DataDir, "properties"), "*.json").Select(File.ReadAllText));
        return store;
    }

    [Fact]
    public void EveryKnownProperty_HasExactlyOneDefinition_AndViceVersa()
    {
        var defs = LoadProperties();

        foreach (var name in ItemProperties.All)
            Assert.True(defs.Contains(name), $"property '{name}' has no PropertyDefinition.");

        foreach (var def in defs.GetAll())
            Assert.Contains(def.Id, ItemProperties.All);

        Assert.Equal(ItemProperties.All.Count, defs.Count);
    }

    [Theory]
    [InlineData("hardness", PropertyRole.Structural)]
    [InlineData("solubility", PropertyRole.Structural)]
    [InlineData("instability", PropertyRole.Structural)]
    [InlineData("resonance", PropertyRole.Structural)]
    [InlineData("heat", PropertyRole.Reactive)]
    [InlineData("arcane", PropertyRole.Reactive)] // arcane stays a reactive property, not an essence (§5.2.1)
    [InlineData("heat_resistance", PropertyRole.Response)]
    [InlineData("toxin_resistance", PropertyRole.Response)]
    [InlineData("harvest_resistance", PropertyRole.Sourcing)] // inert in crafting
    public void RolesAreAssignedAsDesigned(string property, PropertyRole expected)
    {
        var defs = LoadProperties();
        Assert.Equal(expected, defs.GetById(property).Role);
    }

    [Fact]
    public void OpposedPairs_AreSymmetric()
    {
        var defs = LoadProperties();
        Assert.Equal("cold", defs.GetById("heat").Opposes);
        Assert.Equal("heat", defs.GetById("cold").Opposes);
        Assert.Equal("decay", defs.GetById("growth").Opposes);
        Assert.Equal("growth", defs.GetById("decay").Opposes);
    }

    [Fact]
    public void Resistance_UsesAuthoredResponseValue_AsOverride()
    {
        var defs = LoadProperties();
        var material = new PropertySet(new Dictionary<string, double>
        {
            ["heat_resistance"] = 60, ["insulation"] = 40, ["mass"] = 60,
        });

        // Authored heat_resistance wins outright — insulation/mass are ignored.
        Assert.Equal(60, ResistanceCalculator.Resistance("heat", material, defs));
    }

    [Fact]
    public void Resistance_DerivesFromContributors_WhenNoOverrideAuthored()
    {
        var defs = LoadProperties();
        var material = new PropertySet(new Dictionary<string, double>
        {
            ["insulation"] = 40, ["mass"] = 60, // no heat_resistance
        });

        // insulation×0.5 + mass×0.15 = 20 + 9 = 29
        Assert.Equal(29, ResistanceCalculator.Resistance("heat", material, defs));
    }

    [Fact]
    public void Resistance_Charge_DerivesFromInsulation_NoResponseProperty()
    {
        var defs = LoadProperties();
        var material = new PropertySet(new Dictionary<string, double> { ["insulation"] = 50 });
        Assert.Equal(50, ResistanceCalculator.Resistance("charge", material, defs));
    }

    [Fact]
    public void Resistance_IsZero_ForNonReactiveOrUnknownProperties()
    {
        var defs = LoadProperties();
        var material = new PropertySet(new Dictionary<string, double> { ["hardness"] = 80 });
        Assert.Equal(0, ResistanceCalculator.Resistance("hardness", material, defs)); // structural, not reactive
        Assert.Equal(0, ResistanceCalculator.Resistance("nonexistent", material, defs));
    }
}
