using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>
/// The item's genetic profile (docs/affixes.md §2.1) — computed once at fabrication, stored on
/// the instance, never recomputed. Everything the affix system decides (eligibility, weight,
/// tier ceiling) it decides by reading this.
/// </summary>
public sealed record Genome(
    string FormId,
    IReadOnlyDictionary<string, double> Pressure,
    IReadOnlyDictionary<string, double> Essence,
    IReadOnlyList<TraitInstance> Expressed,
    IReadOnlyList<TraitInstance> Dormant,
    IReadOnlyList<string> Tags,
    int Potency,
    int GenerationDepth,
    IReadOnlyList<string> Signatures)
{
    public static readonly Genome Empty = new(
        string.Empty,
        new Dictionary<string, double>(),
        new Dictionary<string, double>(),
        Array.Empty<TraitInstance>(),
        Array.Empty<TraitInstance>(),
        Array.Empty<string>(),
        0, 0, Array.Empty<string>());

    public double PressureOf(string property) => Pressure.GetValueOrDefault(property);
    public double EssenceOf(string key) => Essence.GetValueOrDefault(key);
}

public static class GenomeCalculator
{
    /// <summary>
    /// Pressure below this is trace and is dropped, so the genome lists only properties that
    /// actually reach the item rather than every rounding artefact a deep material carries.
    /// </summary>
    public const double PressureFloor = 0.5;

    /// <summary>
    /// §2.2 — pressure is the <b>stat-map-weighted</b> property value: how much of the property
    /// actually reaches the parts of the item that matter. Relevance is the form's stat_map
    /// weight for that (slot, property), renormalised per property; slots the stat_map never
    /// mentions for a property fall back to their mass share. Same materials, different form,
    /// different genome — which is what stops one globally-best material existing.
    /// </summary>
    public static IReadOnlyDictionary<string, double> Pressure(
        FormTemplateDefinition form,
        IReadOnlyDictionary<string, (MaterialDefinition Material, MaterialProfile Profile)> components)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(components);

        // Which properties does the stat_map read at all, and at what per-slot weight?
        var weightPerSlotByProperty =
            new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
        foreach (var contributions in form.StatMap.Values)
        {
            foreach (var contribution in contributions)
            {
                if (!weightPerSlotByProperty.TryGetValue(contribution.Property, out var weightPerSlot))
                    weightPerSlotByProperty[contribution.Property] =
                        weightPerSlot = new Dictionary<string, double>(StringComparer.Ordinal);

                if (contribution.Slot == FormSlots.AllSlots)
                {
                    foreach (var (slotName, slot) in form.Slots)
                        weightPerSlot[slotName] =
                            weightPerSlot.GetValueOrDefault(slotName) + (contribution.Weight * slot.MassShare);
                }
                else
                {
                    weightPerSlot[contribution.Slot] =
                        weightPerSlot.GetValueOrDefault(contribution.Slot) + contribution.Weight;
                }
            }
        }

        // Every property any component carries gets a pressure; absent = 0 and is omitted.
        var pressure = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var properties = components.Values
            .SelectMany(c => c.Profile.Properties.AsDictionary().Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var property in properties)
        {
            double propertyPressure;
            if (weightPerSlotByProperty.TryGetValue(property, out var weightPerSlot)
                && weightPerSlot.Values.Sum() > 0)
            {
                var totalWeight = weightPerSlot.Values.Sum();
                propertyPressure = weightPerSlot.Sum(entry =>
                    components.TryGetValue(entry.Key, out var component)
                        ? component.Profile.Properties.Get(property) * (entry.Value / totalWeight)
                        : 0.0);
            }
            else
            {
                // The stat_map never reads it — mass share is the honest fallback.
                propertyPressure = form.Slots.Sum(slot =>
                    components.TryGetValue(slot.Key, out var component)
                        ? component.Profile.Properties.Get(property) * slot.Value.MassShare
                        : 0.0);
            }

            if (propertyPressure > PressureFloor)
                pressure[property] = Math.Round(propertyPressure, 1);
        }

        return pressure;
    }
}
