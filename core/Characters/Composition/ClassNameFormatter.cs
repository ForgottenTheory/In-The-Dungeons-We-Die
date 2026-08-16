using System.Text.RegularExpressions;
using Dungeons.Content;

namespace Dungeons.Characters.Composition;

/// <summary>
/// A reusable sentence shape for a generated class name (docs/classes.md).
///
/// <para>Only the <b>trailing clause</b> is templated. The lead — "The {prefix} {archetype}" —
/// is universal, which is what lets a name degrade gracefully when there is no prefix, no
/// suffix, or neither, without nine templates each needing four variants.</para>
/// </summary>
public sealed class NameFormatDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>The trailing clause, containing <c>{suffix}</c>. A clause beginning with a
    /// comma or dash attaches directly; anything else attaches after a space.</summary>
    public string Clause { get; init; } = string.Empty;

    /// <summary>Shown in the Character Lab so the styles are browsable.</summary>
    public string Example { get; init; } = string.Empty;
}

/// <summary>The pieces a class name is assembled from. Presentation only.</summary>
public sealed record ClassNameParts(
    string Archetype,
    string? Prefix = null,
    string? Suffix = null,
    string Format = "standard",
    string? CustomPhrase = null);

/// <summary>
/// Builds the generated class name.
///
/// <para>The point of the system is that <b>not every character is "Prefix Archetype of the
/// Suffix"</b>. The Suffix carries format metadata that changes the grammar of the sentence, so
/// the roster reads like accidents, citations and medical warnings rather than a list of
/// fantasy titles.</para>
///
/// <para><b>Formatting never touches mechanics.</b> A Suffix's <c>format</c> field is read here
/// and nowhere else — changing how a name reads can never change how a character plays.</para>
/// </summary>
public static class ClassNameFormatter
{
    private static readonly Regex Whitespace = new(@"\s{2,}", RegexOptions.Compiled);

    /// <summary>Clause leaders that attach without a preceding space. A comma hugs the word
    /// before it; a dash does not — "Bastion— Ethics" is wrong, "Bastion — Ethics" is right.</summary>
    private const string TightLeaders = ",;:";

    public static string Format(ClassNameParts parts, DataStore<NameFormatDefinition> formats)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(formats);

        var lead = Clean(string.Join(' ', new[] { "The", parts.Prefix, parts.Archetype }
            .Where(p => !string.IsNullOrWhiteSpace(p))));

        var clause = Clause(parts, formats);
        if (string.IsNullOrEmpty(clause))
            return lead;

        return TightLeaders.Contains(clause[0]) ? lead + clause : lead + " " + clause;
    }

    /// <summary>Convenience overload for a composed build.</summary>
    public static string Format(
        BaseClassDefinition @base,
        PrefixDefinition? prefix,
        SuffixDefinition? suffix,
        DataStore<NameFormatDefinition> formats)
    {
        ArgumentNullException.ThrowIfNull(@base);

        return Format(
            new ClassNameParts(
                Archetype: @base.Name,
                Prefix: prefix?.Name,
                Suffix: suffix?.Name,
                Format: suffix?.Format ?? "standard",
                CustomPhrase: suffix?.CustomPhrase),
            formats);
    }

    private static string Clause(ClassNameParts parts, DataStore<NameFormatDefinition> formats)
    {
        // A custom phrase replaces the whole clause, not the suffix slot — otherwise
        // "Against Medical Advice" would render as "Diagnosed With Against Medical Advice".
        if (!string.IsNullOrWhiteSpace(parts.CustomPhrase))
            return ", " + parts.CustomPhrase.Trim();

        if (string.IsNullOrWhiteSpace(parts.Suffix))
            return string.Empty;

        // An unknown format falls back to the plainest reading rather than throwing — a name
        // is cosmetic, and refusing to render a character over a typo would be absurd.
        var template = formats.TryGetById(parts.Format, out var format) && !string.IsNullOrEmpty(format.Clause)
            ? format.Clause
            : "of {suffix}";

        return Clean(template.Replace("{suffix}", parts.Suffix.Trim(), StringComparison.Ordinal));
    }

    private static string Clean(string text) => Whitespace.Replace(text, " ").Trim();

    /// <summary>
    /// The prefix's display word. Prefix names are authored as "The Galvanic" so they read
    /// naturally alone, but the assembled name already supplies "The" — so it is stripped here.
    /// </summary>
    public static string PrefixWord(string prefixName)
    {
        ArgumentNullException.ThrowIfNull(prefixName);

        const string article = "The ";
        return prefixName.StartsWith(article, StringComparison.OrdinalIgnoreCase)
            ? prefixName[article.Length..]
            : prefixName;
    }
}
