using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// Assay's reveal ladder. The invariant that matters is the one the design states outright:
/// Assay improves understanding, never power. So these tests check that the <em>reading is the
/// same at every level</em> and only its legibility changes — a high-Assay player is not
/// holding a better material, they are finally reading the one they had.
/// </summary>
public class AssayLensTests
{
    private static readonly PropertyGlossary Glossary = new(new DataStore<PropertyDefinition>());

    private static MaterialReading HotMetal() => new(
        Name: "Emberite Ore",
        Descriptor: "hot metal",
        Leading: new[] { new LeadingProperty("heat", PropertyTier.Strong) },
        Bonding: PropertyTier.Moderate,
        Receptive: new[] { new Receptiveness(TransferMedium.Thermal, PropertyTier.Strong) },
        Workability: 90,
        Expression: PropertyTier.Moderate,
        Traits: new[] { new TraitReading(new TraitInstance("trait.emberbound", 40), "Emberbound", "burns the hand") },
        Essence: new[] { new EssenceReading("essence.fire", "Fire", PropertyTier.Low) },
        Resonance: PropertyTier.Low,
        VesselStressed: false);

    [Theory]
    [InlineData(1, AssayDepth.Superficial)]
    [InlineData(9, AssayDepth.Superficial)]
    [InlineData(10, AssayDepth.Composition)]
    [InlineData(25, AssayDepth.Reactive)]
    [InlineData(45, AssayDepth.Traits)]
    [InlineData(65, AssayDepth.Essence)]
    [InlineData(85, AssayDepth.Potential)]
    [InlineData(99, AssayDepth.Potential)]
    public void DepthFollowsTheLevelLadder(int assayLevel, AssayDepth expected) =>
        Assert.Equal(expected, AssayLens.DepthFor(assayLevel));

    /// <summary>Each step is a strictly larger view — nothing legible at a lower level ever
    /// becomes redacted again.</summary>
    [Fact]
    public void RevealIsMonotonic()
    {
        foreach (var facet in Enum.GetValues<AssayFacet>())
        {
            var firstRevealing = Enum.GetValues<AssayDepth>().First(d => AssayLens.Reveals(d, facet));
            foreach (var deeper in Enum.GetValues<AssayDepth>().Where(d => d >= firstRevealing))
                Assert.True(AssayLens.Reveals(deeper, facet), $"{facet} went dark again at {deeper}");
        }
    }

    [Fact]
    public void IdentityIsAlwaysLegible()
    {
        var text = AssayLens.Material(HotMetal(), Glossary, AssayDepth.Superficial);

        Assert.Contains("Emberite Ore", text);
        Assert.Contains("hot metal", text);
    }

    /// <summary>The design's own example: "Hot Metal · Unknown Trait · ??? Modifier
    /// Influence" — a novice sees that there is something there and what would open it.</summary>
    [Fact]
    public void UnearnedFacetsReadAsRedactedWithTheLevelThatOpensThem()
    {
        var text = AssayLens.Material(HotMetal(), Glossary, AssayDepth.Superficial);

        Assert.Contains(AssayTuning.Redacted, text);
        Assert.Contains($"Assay {AssayTuning.TraitsLevel}", text);
        Assert.DoesNotContain("Emberbound", text);
    }

    [Fact]
    public void ReachingATierRevealsExactlyThatFacet()
    {
        var composition = AssayLens.Material(HotMetal(), Glossary, AssayDepth.Composition);
        Assert.DoesNotContain("Emberbound", composition);

        var traits = AssayLens.Material(HotMetal(), Glossary, AssayDepth.Traits);
        Assert.Contains("Emberbound", traits);
        Assert.Contains(AssayTuning.Redacted, traits); // essence is still dark
    }

    /// <summary>At full depth the lens gets out of the way entirely and defers to the ordinary
    /// semantic voice, so the two can never drift into different vocabularies.</summary>
    [Fact]
    public void FullDepthRendersTheOrdinarySemanticReading()
    {
        var reading = HotMetal();

        Assert.Equal(
            SemanticFormat.Material(reading, Glossary),
            AssayLens.Material(reading, Glossary, AssayDepth.Essence));
    }

    /// <summary>
    /// Assay never makes anything stronger. Redaction is a rendering concern only: the reading
    /// handed in is the reading handed back, whatever the level.
    /// </summary>
    [Fact]
    public void TheUnderlyingReadingIsIdenticalAtEveryDepth()
    {
        var reading = HotMetal();

        foreach (var depth in Enum.GetValues<AssayDepth>())
        {
            AssayLens.Material(reading, Glossary, depth);

            Assert.Equal(PropertyTier.Strong, Assert.Single(reading.Leading).Tier);
            Assert.Equal(90, reading.Workability);
            Assert.Equal(PropertyTier.Low, Assert.Single(reading.Essence).Tier);
        }
    }
}
