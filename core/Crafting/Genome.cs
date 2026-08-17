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
        var statWeights = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
        foreach (var reads in form.StatMap.Values)
        {
            foreach (var read in reads)
            {
                if (!statWeights.TryGetValue(read.Property, out var perSlot))
                    statWeights[read.Property] = perSlot = new Dictionary<string, double>(StringComparer.Ordinal);

                if (read.Slot == "*")
                {
                    foreach (var (slotName, slot) in form.Slots)
                        perSlot[slotName] = perSlot.GetValueOrDefault(slotName) + read.W * slot.MassShare;
                }
                else
                {
                    perSlot[read.Slot] = perSlot.GetValueOrDefault(read.Slot) + read.W;
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
            double value;
            if (statWeights.TryGetValue(property, out var perSlot) && perSlot.Values.Sum() > 0)
            {
                var totalWeight = perSlot.Values.Sum();
                value = perSlot.Sum(kv =>
                    components.TryGetValue(kv.Key, out var c)
                        ? c.Profile.Properties.Get(property) * (kv.Value / totalWeight)
                        : 0.0);
            }
            else
            {
                // The stat_map never reads it — mass share is the honest fallback.
                value = form.Slots.Sum(s =>
                    components.TryGetValue(s.Key, out var c)
                        ? c.Profile.Properties.Get(property) * s.Value.MassShare
                        : 0.0);
            }

            if (value > 0.5)
                pressure[property] = Math.Round(value, 1);
        }

        return pressure;
    }
}
