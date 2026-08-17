using Dungeons.Content;
using Dungeons.Crafting;

namespace Dungeons.Presentation;

/// <summary>How heavily a stat read weighs this slot's material.</summary>
public enum ReadWeight
{
    Light,
    Moderate,
    Heavy,
}

/// <summary>How much of a trait category this slot's aperture lets through.</summary>
public enum ApertureBand
{
    Muted,
    Partial,
    Full,
}

/// <summary>One stat the form reads from this slot, and how the candidate material answers it.
/// <paramref name="SharedAcrossSlots"/> marks a <c>"*"</c> read (mass-share-weighted whole).</summary>
public sealed record StatRead(
    string Stat,
    string Property,
    ReadWeight Weight,
    PropertyTier MaterialTier,
    bool SharedAcrossSlots);

/// <summary>One trait the candidate material carries, and how this slot's aperture treats it —
/// the §16.3 expression rule (magnitude × aperture), read ahead of time.</summary>
public sealed record TraitFit(
    TraitInstance Trait,
    string TraitName,
    string Category,
    ApertureBand Band);

/// <summary>
/// §2E contextual meaning for fabrication: why a material appears suitable or unsuitable in a
/// named slot, derived from the form's own data (tags, stat_map, apertures) — never a generic
/// string. Eligibility stays tag-law exactly as the engine enforces it; everything else here
/// is advisory reading.
/// </summary>
public sealed record SlotReading(
    string Slot,
    bool Eligible,
    string? EligibleVia,
    IReadOnlyList<string> RequiredTags,
    double MassShare,
    IReadOnlyList<StatRead> Reads,
    IReadOnlyList<TraitFit> Traits);

public static class SlotReadings
{
    /// <summary>Reads one candidate material against one slot of a form.</summary>
    public static SlotReading For(
        FormTemplateDefinition form,
        string slotName,
        MaterialDefinition material,
        MaterialProfile profile,
        DataStore<TraitDefinition> traits)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(traits);

        if (!form.Slots.TryGetValue(slotName, out var slot))
            throw new ArgumentException($"Form '{form.Id}' has no slot '{slotName}'.", nameof(slotName));

        // Eligibility mirrors FabricationEngine's any-of tag gate exactly.
        var via = slot.RequiresTags.FirstOrDefault(
            t => material.Tags.Contains(t, StringComparer.OrdinalIgnoreCase));
        var eligible = slot.RequiresTags.Count == 0 || via is not null;

        var reads = new List<StatRead>();
        foreach (var (stat, contributions) in form.StatMap)
        {
            foreach (var read in contributions)
            {
                var shared = read.Slot == "*";
                if (!shared && !string.Equals(read.Slot, slotName, StringComparison.Ordinal))
                    continue;

                // For a "*" read this slot's effective pull is its mass share of the weight.
                var effectiveWeight = shared ? read.W * slot.MassShare : read.W;

                reads.Add(new StatRead(
                    stat,
                    read.Property,
                    WeightBand(effectiveWeight),
                    Tiers.Of(profile.Properties.Get(read.Property)),
                    shared));
            }
        }

        var fits = new List<TraitFit>();
        foreach (var trait in profile.Traits)
        {
            var category = traits.TryGetById(trait.Id, out var def) ? def.Category : "structural";
            var gate = slot.Aperture.GetValueOrDefault(category, 1.0);

            fits.Add(new TraitFit(
                trait,
                traits.TryGetById(trait.Id, out var named) ? named.Name : trait.Id,
                category,
                ApertureBandOf(gate)));
        }

        return new SlotReading(
            slotName,
            eligible,
            via,
            slot.RequiresTags,
            slot.MassShare,
            reads.OrderByDescending(r => r.Weight).ThenBy(r => r.Stat, StringComparer.Ordinal).ToList(),
            fits);
    }

    public static ReadWeight WeightBand(double weight) => weight switch
    {
        >= PresentationTuning.HeavyReadWeight => ReadWeight.Heavy,
        >= PresentationTuning.ModerateReadWeight => ReadWeight.Moderate,
        _ => ReadWeight.Light,
    };

    public static ApertureBand ApertureBandOf(double gate) => gate switch
    {
        >= PresentationTuning.FullApertureFloor => ApertureBand.Full,
        >= PresentationTuning.PartialApertureFloor => ApertureBand.Partial,
        _ => ApertureBand.Muted,
    };
}
