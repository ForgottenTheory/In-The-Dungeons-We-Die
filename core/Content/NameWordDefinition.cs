namespace Dungeons.Content;

/// <summary>What part of a generated name a <see cref="NameWordDefinition"/> supplies.</summary>
public enum NameWordKind
{
    /// <summary>An intensity ladder for a property — four adjectives, weakest first (§13.2).</summary>
    Intensity,

    /// <summary>The noun a <c>form:</c> tag contributes, e.g. <c>powder</c> → "Dust" (§13.3).</summary>
    FormNoun,
}

/// <summary>
/// One entry in the name grammar (docs/emergent-item-system.md §13, listed as
/// <c>NameGrammar</c> in §18's data model). Vocabulary is data so names can be retuned
/// without recompiling — and names are the part of an emergent system players judge hardest.
///
/// <para>Ids are prefixed by kind: <c>intensity.heat</c> carries a ladder, <c>form.powder</c>
/// carries a single noun.</para>
///
/// <para>§13.2's "ladder trick" is what delivers intensity <i>without</i> tier words: a hot
/// material is Searing rather than "Greater Warmed", because the vocabulary escalates instead
/// of the adjectives stacking.</para>
/// </summary>
public sealed class NameWordDefinition : IDefinition
{
    public const string IntensityPrefix = "intensity.";
    public const string FormPrefix = "form.";

    public string Id { get; init; } = string.Empty;

    /// <summary>Ladder tiers weakest-first, or a single noun for a form.</summary>
    public IReadOnlyList<string> Words { get; init; } = Array.Empty<string>();

    public NameWordKind Kind => Id.StartsWith(IntensityPrefix, StringComparison.Ordinal)
        ? NameWordKind.Intensity
        : NameWordKind.FormNoun;

    /// <summary>The property id or form value this entry is keyed to.</summary>
    public string Key => Id.StartsWith(IntensityPrefix, StringComparison.Ordinal)
        ? Id[IntensityPrefix.Length..]
        : Id.StartsWith(FormPrefix, StringComparison.Ordinal)
            ? Id[FormPrefix.Length..]
            : Id;

    /// <summary>True if the id carries a recognised kind prefix.</summary>
    public bool HasKnownPrefix =>
        Id.StartsWith(IntensityPrefix, StringComparison.Ordinal)
        || Id.StartsWith(FormPrefix, StringComparison.Ordinal);
}
