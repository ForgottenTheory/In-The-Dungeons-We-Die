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
    /// <summary>
    /// The <c>form:</c> values that make a material physical gear stock (D46/D52): things a
    /// blueprint slot can gate on, or recognizable parts and stock. A migrated structural
    /// material must author base stats, and only structural materials may — the D46 dividing
    /// line ("if it only matters for special behavior, it is an identity, not a base stat")
    /// applied to the whole library. Fruits, herbs, liquids and the other consumable-shaped
    /// forms stay base-less on purpose: their worth is what they carry, not what they weigh.
    /// </summary>
    public static readonly IReadOnlySet<string> StructuralForms = new HashSet<string>(StringComparer.Ordinal)
    {
        "metal", "ore", "ingot", "wood", "stone", "crystal", "gem", "bone", "horn",
        "chitin", "hide", "leather", "fiber", "cloth", "bark", "glass", "tooth", "claw",
        "thread", "shaft", "stave", "haft", "binding", "mechanism",
    };

    /// <summary>
    /// The structural forms that take an edge or a point — the only materials whose base may
    /// carry Bite (D46: Bite is cutting/piercing damage; a fiber cannot cut whatever its
    /// hardness says). Validated, so a copy-paste Bite on a hide fails at load.
    /// </summary>
    public static readonly IReadOnlySet<string> EdgeCapableForms = new HashSet<string>(StringComparer.Ordinal)
    {
        "metal", "ore", "ingot", "crystal", "gem", "stone", "glass",
        "bone", "horn", "tooth", "claw", "chitin",
    };

    public static readonly TagFamily Origin = new("origin", TagCardinality.OneOrTwo,
        new HashSet<string>(StringComparer.Ordinal) { "flora", "fauna", "fungal", "mineral", "elemental", "arcane", "synthetic" });

    public static readonly TagFamily Comp = new("comp", TagCardinality.ExactlyOne,
        new HashSet<string>(StringComparer.Ordinal) { "organic", "inorganic" });

    public static readonly TagFamily Form = new("form", TagCardinality.AtLeastOne);

    public static readonly TagFamily State = new("state", TagCardinality.ExactlyOne,
        new HashSet<string>(StringComparer.Ordinal) { "raw", "refined", "processed", "alloy", "extract", "distillate", "composite", "spent", "attuned" });

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
