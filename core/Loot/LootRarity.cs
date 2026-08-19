namespace Dungeons.Loot;

/// <summary>
/// How unusual a drop is. Deliberately the same five steps as the <c>rarity:</c> material tag
/// family (<see cref="Dungeons.Content.TagFamilies.Rarity"/>) so the game has <b>one</b> rarity
/// vocabulary: a material's tag is the authoritative source and a loot entry may not restate it.
/// Only items that carry no rarity tag at all — technique manuals, schematics, consumables —
/// declare a rarity on the entry itself.
/// </summary>
public enum LootRarity
{
    Common,
    Uncommon,
    Rare,
    VeryRare,
    Exceptional,
}

/// <summary>Parsing between the <c>rarity:</c> tag values and <see cref="LootRarity"/>.</summary>
public static class LootRarities
{
    /// <summary>The tag value (<c>very_rare</c>) for a rarity, i.e. the inverse of
    /// <see cref="TryParseTagValue"/>. Used by validation messages and player-facing text.</summary>
    public static string ToTagValue(this LootRarity rarity) => rarity switch
    {
        LootRarity.Common => "common",
        LootRarity.Uncommon => "uncommon",
        LootRarity.Rare => "rare",
        LootRarity.VeryRare => "very_rare",
        LootRarity.Exceptional => "exceptional",
        _ => "common",
    };

    /// <summary>
    /// The rarity a tag list declares, or null when it declares none.
    ///
    /// <para>The one place a <c>rarity:</c> tag is turned into a rarity. Two readers need it —
    /// <see cref="LootResolver"/> when a drop lands, and the Realm briefing when it tells the
    /// player what a place yields — and a second copy of this loop is how the two would
    /// eventually disagree about what "rare" means.</para>
    /// </summary>
    public static LootRarity? FromTags(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        foreach (var tag in tags)
        {
            if (Dungeons.Content.TagFamilies.TryParse(tag, out var family, out var value)
                && string.Equals(family, Dungeons.Content.TagFamilies.Rarity.Name, StringComparison.OrdinalIgnoreCase)
                && TryParseTagValue(value, out var tagged))
                return tagged;
        }

        return null;
    }

    /// <summary>Reads a <c>rarity:</c> tag value. False for anything outside the family.</summary>
    public static bool TryParseTagValue(string value, out LootRarity rarity)
    {
        switch (value?.ToLowerInvariant())
        {
            case "common": rarity = LootRarity.Common; return true;
            case "uncommon": rarity = LootRarity.Uncommon; return true;
            case "rare": rarity = LootRarity.Rare; return true;
            case "very_rare": rarity = LootRarity.VeryRare; return true;
            case "exceptional": rarity = LootRarity.Exceptional; return true;
            default: rarity = LootRarity.Common; return false;
        }
    }
}
