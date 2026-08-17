using Dungeons.Content;
using Dungeons.Crafting;

namespace Dungeons.Presentation;

/// <summary>A property worth leading with — tier Low or better, largest first.</summary>
public sealed record LeadingProperty(string Property, PropertyTier Tier);

/// <summary>How readily this material releases under one transfer medium (§7.3), read from the
/// same medium property the algebra reads (<see cref="ReactionCoefficients.MediumProperty"/>),
/// so the hint and the engine can never disagree.</summary>
public sealed record Receptiveness(TransferMedium Medium, PropertyTier Tier);

public sealed record EssenceReading(string EssenceId, string Name, PropertyTier Tier);

public sealed record TraitReading(TraitInstance Trait, string Name, string Drawback);

/// <summary>
/// "What is this thing good at?" — §3's material answer, derived entirely from existing data.
/// This is the reading every picker and inspector renders instead of a number wall.
/// </summary>
public sealed record MaterialReading(
    string Name,
    string Descriptor,
    IReadOnlyList<LeadingProperty> Leading,
    PropertyTier Bonding,
    IReadOnlyList<Receptiveness> Receptive,
    int Integrity,
    PropertyTier Expression,
    IReadOnlyList<TraitReading> Traits,
    IReadOnlyList<EssenceReading> Essence,
    PropertyTier Resonance,
    bool VesselStrained);

public static class MaterialReadings
{
    private static readonly TransferMedium[] Media =
    {
        TransferMedium.Solvent, TransferMedium.Thermal, TransferMedium.Mechanical, TransferMedium.Arcane,
    };

    public static MaterialReading From(
        MaterialDefinition material,
        MaterialProfile profile,
        DataStore<PropertyDefinition> propertyRegistry,
        DataStore<TraitDefinition> traits,
        DataStore<EssenceDefinition> essences)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(propertyRegistry);
        ArgumentNullException.ThrowIfNull(traits);
        ArgumentNullException.ThrowIfNull(essences);

        var properties = profile.Properties;

        // "What is this good at" leads with the crafting-relevant roles. Response properties
        // are derived defence (they surface on armour readings), sourcing is gathering-only —
        // neither belongs on the identity line.
        var leading = properties.AsDictionary()
            .Where(p => Tiers.Of(p.Value) >= PropertyTier.Low && LeadsWith(propertyRegistry, p.Key))
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Key, StringComparer.Ordinal)
            .Take(PresentationTuning.LeadingPropertyCount)
            .Select(p => new LeadingProperty(p.Key, Tiers.Of(p.Value)))
            .ToList();

        var receptive = Media
            .Select(m => new Receptiveness(m, Tiers.Of(ReactionCoefficients.MediumProperty(m, properties))))
            .ToList();

        var traitReadings = profile.Traits
            .Select(t => traits.TryGetById(t.Id, out var def)
                ? new TraitReading(t, def.Name, def.Drawback)
                : new TraitReading(t, t.Id, string.Empty))
            .ToList();

        var essenceReadings = profile.Essence
            .Where(e => e.Value > 0)
            .OrderByDescending(e => e.Value)
            .ThenBy(e => e.Key, StringComparer.Ordinal)
            .Select(e => new EssenceReading(
                e.Key,
                EssenceName(essences, e.Key),
                Tiers.Of(e.Value)))
            .ToList();

        var resonance = properties.Get(Dungeons.Items.ItemProperties.Resonance);

        return new MaterialReading(
            material.Name,
            TagWords.Descriptor(material.Tags),
            leading,
            Tiers.Of(properties.Get(Dungeons.Items.ItemProperties.Affinity)),
            receptive,
            profile.Integrity,
            Tiers.Of(profile.Potency),
            traitReadings,
            essenceReadings,
            Tiers.Of(resonance),
            EssenceTuning.Strain(profile.Essence, resonance) > 0);
    }

    private static bool LeadsWith(DataStore<PropertyDefinition> registry, string propertyId) =>
        !registry.TryGetById(propertyId, out var def)
        || def.Role is PropertyRole.Structural or PropertyRole.Reactive;

    private static string EssenceName(DataStore<EssenceDefinition> essences, string key)
    {
        foreach (var essence in essences.GetAll())
        {
            if (string.Equals(essence.Key, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(essence.Id, key, StringComparison.OrdinalIgnoreCase))
                return essence.Name;
        }

        return key.Length > 1 ? char.ToUpperInvariant(key[0]) + key[1..] : key;
    }
}

/// <summary>Turns namespaced tags into the two-word descriptor a reading opens with
/// ("refined metal", "raw ore") — §2E context from data the material already carries.</summary>
public static class TagWords
{
    public static string Descriptor(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var state = ValueOf(tags, "state:");
        var form = ValueOf(tags, "form:");
        var origin = ValueOf(tags, "origin:");

        var noun = form ?? origin ?? "material";
        return state is null ? noun : $"{state} {noun}";
    }

    private static string? ValueOf(IReadOnlyList<string> tags, string family)
    {
        foreach (var tag in tags)
        {
            if (tag.StartsWith(family, StringComparison.OrdinalIgnoreCase) && tag.Length > family.Length)
                return tag[family.Length..].ToLowerInvariant();
        }

        return null;
    }
}
