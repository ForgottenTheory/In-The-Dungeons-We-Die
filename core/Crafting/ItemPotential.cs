using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>
/// What a finished item is <i>capable of</i> (docs/affixes.md §2.1, "the Genome") — computed
/// once when the item is assembled, stored on the instance, and never recomputed.
///
/// <para>This is the sole input to <see cref="Dungeons.Affixes.ModifierGenerator"/>: which
/// modifiers are available, how likely each one is, and the strongest tier it may reach are all
/// pure functions of this record.</para>
/// </summary>
public sealed record ItemPotential(
    string BlueprintId,
    IReadOnlyDictionary<string, double> MaterialInfluence,
    IReadOnlyDictionary<string, double> Essence,
    IReadOnlyList<TraitInstance> Expressed,
    IReadOnlyList<TraitInstance> Dormant,
    IReadOnlyList<string> Tags,
    int MaterialStrength,
    int GenerationDepth,
    IReadOnlyList<string> Signatures)
{
    public static readonly ItemPotential Empty = new(
        string.Empty,
        new Dictionary<string, double>(),
        new Dictionary<string, double>(),
        Array.Empty<TraitInstance>(),
        Array.Empty<TraitInstance>(),
        Array.Empty<string>(),
        0, 0, Array.Empty<string>());

    public double InfluenceOf(string property) => MaterialInfluence.GetValueOrDefault(property);
    public double EssenceOf(string key) => Essence.GetValueOrDefault(key);
}

public static class ItemPotentialCalculator
{
    /// <summary>
    /// Influence below this is trace and is dropped, so an item potential lists only the
    /// properties that actually reach the item, not every rounding artefact a deep material
    /// carries.
    /// </summary>
    public const double MaterialInfluenceFloor = 0.5;

    /// <summary>
    /// §2.2 — a property's influence is its <b>stat-map-weighted</b> value: how much of it
    /// actually reaches the parts of the item that matter. Relevance is the blueprint's stat_map
    /// weight for that (slot, property), renormalised per property; slots the stat_map never
    /// mentions for a property fall back to their mass share. Same materials, different form,
    /// different item potential — which is what stops one globally-best material existing.
    /// </summary>
    public static IReadOnlyDictionary<string, double> MaterialInfluence(
        EquipmentBlueprintDefinition form,
        IReadOnlyDictionary<string, (MaterialDefinition Material, MaterialState State)> components)
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

                if (contribution.Slot == BlueprintSlots.AllSlots)
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

        // Every property any component carries gets a materialInfluence; absent = 0 and is omitted.
        var materialInfluence = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var properties = components.Values
            .SelectMany(c => c.State.Properties.AsDictionary().Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var property in properties)
        {
            double propertyInfluence;
            if (weightPerSlotByProperty.TryGetValue(property, out var weightPerSlot)
                && weightPerSlot.Values.Sum() > 0)
            {
                var totalWeight = weightPerSlot.Values.Sum();
                propertyInfluence = weightPerSlot.Sum(entry =>
                    components.TryGetValue(entry.Key, out var component)
                        ? component.State.Properties.Get(property) * (entry.Value / totalWeight)
                        : 0.0);
            }
            else
            {
                // The stat_map never reads it — mass share is the honest fallback.
                propertyInfluence = form.Slots.Sum(slot =>
                    components.TryGetValue(slot.Key, out var component)
                        ? component.State.Properties.Get(property) * slot.Value.MassShare
                        : 0.0);
            }

            if (propertyInfluence > MaterialInfluenceFloor)
                materialInfluence[property] = Math.Round(propertyInfluence, 1);
        }

        return materialInfluence;
    }
}
