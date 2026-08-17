using System.Text.RegularExpressions;
using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// D30 invariant 5: display metadata is data on the property registry. Every property must
/// carry a glyph and a gloss, and a gloss never contains numbers — it is the §2E context
/// voice, not a stat line.
/// </summary>
public class GlossaryContentTests
{
    private static readonly DataStore<PropertyDefinition> Properties =
        TestPaths.LoadStore<PropertyDefinition>("properties");

    [Fact]
    public void EveryPropertyCarriesAGlyphAndAGloss()
    {
        foreach (var property in Properties.GetAll())
        {
            Assert.False(string.IsNullOrWhiteSpace(property.Glyph), $"{property.Id} has no glyph.");
            Assert.False(string.IsNullOrWhiteSpace(property.Gloss), $"{property.Id} has no gloss.");
        }
    }

    [Fact]
    public void GlossesNeverContainNumbers()
    {
        foreach (var property in Properties.GetAll())
            Assert.DoesNotMatch(new Regex(@"\d"), property.Gloss);
    }

    /// <summary>Every property code knows about is authored in the registry — the glossary can
    /// never fall back to a bare id for a real property.</summary>
    [Fact]
    public void EveryCodeNamedPropertyIsAuthored()
    {
        foreach (var id in ItemProperties.All)
            Assert.True(Properties.Contains(id), $"{id} missing from the property registry.");
    }

    [Fact]
    public void TheGlossaryDegradesGracefullyForUnknownIds()
    {
        var glossary = new PropertyGlossary(Properties);

        Assert.Equal("not_a_property", glossary.Name("not_a_property"));
        Assert.Equal("·", glossary.Glyph("not_a_property"));
        Assert.Equal(string.Empty, glossary.Gloss("not_a_property"));
    }
}
