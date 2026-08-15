namespace Dungeons.Content;

/// <summary>How many tags of a family a definition may carry.</summary>
public enum TagCardinality
{
    ExactlyOne,
    OneOrTwo,
    AtLeastOne,
    Any,
}

/// <summary>
/// A tag family in the <c>family:value</c> namespace (docs/emergent-item-system.md §4.1).
/// Closed families validate their value against <see cref="ClosedValues"/>; open families
/// (form / class / part) accept any value — the 470-material library is too broad to fix
/// their vocabularies now. Cardinality is enforced by <see cref="ContentValidator"/>.
/// </summary>
public sealed class TagFamily
{
    public string Name { get; }
    public TagCardinality Cardinality { get; }
    public IReadOnlySet<string>? ClosedValues { get; }

    public TagFamily(string name, TagCardinality cardinality, IReadOnlySet<string>? closedValues = null)
    {
        Name = name;
        Cardinality = cardinality;
        ClosedValues = closedValues;
    }

    public int Min => Cardinality switch
    {
        TagCardinality.ExactlyOne => 1,
        TagCardinality.OneOrTwo => 1,
        TagCardinality.AtLeastOne => 1,
        _ => 0,
    };

    public int Max => Cardinality switch
    {
        TagCardinality.ExactlyOne => 1,
        TagCardinality.OneOrTwo => 2,
        _ => int.MaxValue,
    };
}

/// <summary>The material tag families and the <c>family:value</c> parsing helper.</summary>
public static class TagFamilies
{
    public static readonly TagFamily Origin = new("origin", TagCardinality.OneOrTwo,
        new HashSet<string>(StringComparer.Ordinal) { "flora", "fauna", "fungal", "mineral", "elemental", "arcane", "synthetic" });

    public static readonly TagFamily Comp = new("comp", TagCardinality.ExactlyOne,
        new HashSet<string>(StringComparer.Ordinal) { "organic", "inorganic" });

    public static readonly TagFamily Form = new("form", TagCardinality.AtLeastOne);

    public static readonly TagFamily State = new("state", TagCardinality.ExactlyOne,
        new HashSet<string>(StringComparer.Ordinal) { "raw", "refined", "processed", "alloy", "extract", "distillate", "composite", "spent" });

    public static readonly TagFamily Rarity = new("rarity", TagCardinality.ExactlyOne,
        new HashSet<string>(StringComparer.Ordinal) { "common", "uncommon", "rare", "very_rare", "exceptional" });

    public static readonly TagFamily Class = new("class", TagCardinality.Any);

    public static readonly TagFamily Part = new("part", TagCardinality.Any);

    public static readonly IReadOnlyList<TagFamily> All = new[]
    {
        Origin, Comp, Form, State, Rarity, Class, Part,
    };

    private static readonly Dictionary<string, TagFamily> ByName =
        All.ToDictionary(f => f.Name, StringComparer.Ordinal);

    public static bool TryGet(string family, out TagFamily tagFamily) => ByName.TryGetValue(family, out tagFamily!);

    /// <summary>Splits a <c>family:value</c> tag. Returns false if it isn't namespaced.</summary>
    public static bool TryParse(string tag, out string family, out string value)
    {
        var colon = tag.IndexOf(':');
        if (colon <= 0 || colon == tag.Length - 1)
        {
            family = string.Empty;
            value = string.Empty;
            return false;
        }

        family = tag[..colon];
        value = tag[(colon + 1)..];
        return true;
    }
}
