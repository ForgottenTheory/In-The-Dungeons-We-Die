using Dungeons.Characters.Composition;
using Dungeons.Content;
using Xunit;

namespace Dungeons.Tests.Characters;

/// <summary>
/// The dynamic class-name formatter (docs/classes.md).
///
/// <para>The design requirement is that <b>not every character reads as "Prefix Archetype of
/// the Suffix"</b>. The Suffix carries grammar metadata so the roster reads like accidents,
/// citations and medical warnings. The other requirement is a separation: formatting must
/// never be able to influence mechanics.</para>
/// </summary>
public class ClassNameFormatterTests
{
    private static DataStore<NameFormatDefinition> Formats() =>
        TestPaths.LoadStore<NameFormatDefinition>("name_formats");

    private static string Name(string format, string? suffix = "Questionable Ethics", string? custom = null) =>
        ClassNameFormatter.Format(
            new ClassNameParts("Bastion", "Galvanic", suffix, format, custom), Formats());

    // ---- The nine shapes ---------------------------------------------------------------------

    [Theory]
    [InlineData("standard", "The Galvanic Bastion of Questionable Ethics")]
    [InlineData("citation", "The Galvanic Bastion [Cited for Questionable Ethics]")]
    [InlineData("investigation", "The Galvanic Bastion, Currently Under Investigation for Questionable Ethics")]
    [InlineData("warning", "The Galvanic Bastion (Warning: Questionable Ethics)")]
    [InlineData("medical", "The Galvanic Bastion, Diagnosed With Questionable Ethics")]
    [InlineData("liability", "The Galvanic Bastion (Questionable Ethics Accepted)")]
    [InlineData("bureaucratic", "The Galvanic Bastion [Subject to Questionable Ethics]")]
    [InlineData("consequence", "The Galvanic Bastion, Due to Questionable Ethics")]
    [InlineData("notice", "The Galvanic Bastion — Questionable Ethics")]
    public void EachFormatProducesItsSentenceShape(string format, string expected)
    {
        Assert.Equal(expected, Name(format));
    }

    /// <summary>A custom phrase replaces the whole clause, not the suffix slot — otherwise
    /// the medical template would render "Diagnosed With Against Medical Advice".</summary>
    [Fact]
    public void ACustomPhraseReplacesTheEntireClause()
    {
        Assert.Equal(
            "The Galvanic Bastion, Against Medical Advice",
            Name("medical", "Unlicensed Surgery", custom: "Against Medical Advice"));
    }

    // ---- Degrading gracefully ------------------------------------------------------------------

    [Fact]
    public void MissingPartsCollapseCleanlyWithoutStrayPunctuation()
    {
        var formats = Formats();

        Assert.Equal("The Bastion of Questionable Ethics",
            ClassNameFormatter.Format(new ClassNameParts("Bastion", null, "Questionable Ethics"), formats));

        Assert.Equal("The Galvanic Bastion",
            ClassNameFormatter.Format(new ClassNameParts("Bastion", "Galvanic"), formats));

        Assert.Equal("The Bastion",
            ClassNameFormatter.Format(new ClassNameParts("Bastion"), formats));
    }

    /// <summary>A name is cosmetic. Refusing to render a character over a typo in a format id
    /// would be absurd, so it falls back to the plainest reading.</summary>
    [Fact]
    public void AnUnknownFormatFallsBackToStandardRatherThanThrowing()
    {
        Assert.Equal("The Galvanic Bastion of Questionable Ethics", Name("interpretive_dance"));
    }

    [Fact]
    public void NoGeneratedNameHasDoubledOrTrailingWhitespace()
    {
        foreach (var format in Formats().GetAll())
        {
            var name = Name(format.Id);

            Assert.DoesNotContain("  ", name);
            Assert.Equal(name.Trim(), name);
        }
    }

    /// <summary>Prefix names are authored as "The Galvanic" so they read alone; the assembled
    /// name already supplies the article.</summary>
    [Fact]
    public void ThePrefixArticleIsStrippedWhenAssembled()
    {
        Assert.Equal("Galvanic", ClassNameFormatter.PrefixWord("The Galvanic"));
        Assert.Equal("Galvanic", ClassNameFormatter.PrefixWord("Galvanic"));
    }

    // ---- Against the real roster -------------------------------------------------------------------

    /// <summary>Every shipped suffix must render, for every base and prefix, without producing
    /// something broken. 15 × 25 × 50 is too many to eyeball, so it is asserted instead.</summary>
    [Fact]
    public void EveryRosterCombinationRendersSensibly()
    {
        var formats = Formats();
        var bases = TestPaths.LoadStore<BaseClassDefinition>("classes").GetAll().ToList();
        var prefixes = TestPaths.LoadStore<PrefixDefinition>("prefixes").GetAll().ToList();
        var suffixes = TestPaths.LoadStore<SuffixDefinition>("suffixes").GetAll().ToList();

        var rendered = 0;

        foreach (var @base in bases)
        foreach (var prefix in prefixes)
        foreach (var suffix in suffixes)
        {
            var name = ClassNameFormatter.Format(
                new ClassNameParts(
                    @base.Name, ClassNameFormatter.PrefixWord(prefix.Name),
                    suffix.Name, suffix.Format, suffix.CustomPhrase),
                formats);

            Assert.StartsWith("The ", name);
            Assert.Contains(@base.Name, name);
            Assert.DoesNotContain("{", name);          // no unsubstituted slot
            Assert.DoesNotContain("  ", name);
            Assert.DoesNotContain("The The", name);    // article not doubled
            rendered++;
        }

        Assert.Equal(bases.Count * prefixes.Count * suffixes.Count, rendered);
        Assert.True(rendered > 15000, $"only {rendered} combinations — the roster is smaller than expected.");
    }

    /// <summary>The system exists so names vary in grammar, not just in words. If every suffix
    /// produced the same sentence shape we would have rebuilt "of the".</summary>
    [Fact]
    public void TheRosterProducesGenuinelyVariedGrammar()
    {
        var formats = Formats();
        var suffixes = TestPaths.LoadStore<SuffixDefinition>("suffixes").GetAll();

        // Reduce each rendered name to its grammar skeleton by blanking the variable parts,
        // so what remains is purely the sentence shape.
        var shapes = suffixes
            .Select(s => ClassNameFormatter
                .Format(new ClassNameParts("Bastion", "Galvanic", s.Name, s.Format, s.CustomPhrase), formats)
                .Replace("The Galvanic Bastion", "{lead}", StringComparison.Ordinal)
                .Replace(s.Name, "{suffix}", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(shapes.Count >= 8,
            $"only {shapes.Count} distinct sentence shapes across 50 suffixes: {string.Join(" / ", shapes)}");
    }

    // ---- The separation ------------------------------------------------------------------------------

    /// <summary>
    /// Formatting is presentation. Changing a suffix's format must never change what it does —
    /// this pins that the mechanical fields are untouched by the naming path.
    /// </summary>
    [Fact]
    public void FormatIsIndependentOfMechanics()
    {
        var suffix = TestPaths.LoadStore<SuffixDefinition>("suffixes").GetById("suffix.exploding_kneecaps");

        var asAuthored = ClassNameFormatter.Format(
            new ClassNameParts("Bastion", "Galvanic", suffix.Name, suffix.Format), Formats());
        var reformatted = ClassNameFormatter.Format(
            new ClassNameParts("Bastion", "Galvanic", suffix.Name, "bureaucratic"), Formats());

        Assert.NotEqual(asAuthored, reformatted);

        // ...and the mechanics are identical either way, because nothing mechanical read it.
        Assert.Equal(3, suffix.Expressions.Count);
        Assert.True(suffix.IsFullyExpressed);
    }

    [Fact]
    public void EveryFormatDeclaresATemplateAndAnExample()
    {
        foreach (var format in Formats().GetAll())
        {
            Assert.Contains("{suffix}", format.Clause);
            Assert.False(string.IsNullOrWhiteSpace(format.Example), $"{format.Id} has no example.");
        }

        Assert.Equal(ContentValidator.NameFormats.Count, Formats().Count);
    }
}
