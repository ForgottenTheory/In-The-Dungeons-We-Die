using Dungeons.Content;

namespace Dungeons.Crafting.Identity;

/// <summary>
/// The base-read calibration (§11.5, D46): one scale from base units to combat units, pinned
/// to the authored Iron Sword the same way the old fabrication scale was — a plain iron
/// longsword's delivery must match <c>equip.iron_sword</c> (mass 3 → +3 damage/+6 windup,
/// hardness 4). ⚠ Provisional until play, like every identity-system number.
/// </summary>
public static class IdentityFabricationTuning
{
    /// <summary>Combat units one base-unit point of a weight-1.0 read delivers — damage and
    /// armor both. 0.5 lands an iron edge (Bite 6) at the authored sword's +3 damage.</summary>
    public const double CombatUnitsPerBasePoint = 0.5;

    /// <summary>Windup ticks one base-unit point of the speed read adds. 1.4 lands the plain
    /// iron longsword (whole-item Heft ≈ 4.3) at the authored sword's +6 ticks.</summary>
    public const double WindupTicksPerSpeedPoint = 1.4;
}

/// <summary>
/// The mundane physical floor an identity-minted item delivers (D46) — computed from the
/// form's base reads at composition, stored on the instance, consumed by the equipment
/// resolver. Combat units throughout; <see cref="WindupTicks"/> is the swing-weight penalty
/// resolved moves absorb.
/// </summary>
public sealed record ItemBaseDelivery(double DamageBonus, int WindupTicks, double Armor)
{
    public static readonly ItemBaseDelivery None = new(0, 0, 0);
}

/// <summary>Why a composition was refused. Deterministic and previewable, like verb refusals.</summary>
public enum IdentityCompositionFailure
{
    None,

    /// <summary>The form authors no <c>identity_cap</c> — it has not been migrated to the
    /// identity model, and the composer refuses rather than guessing a cap (the same
    /// coexistence seam as unmigrated materials).</summary>
    FormNotMigrated,

    /// <summary>A form slot was left unfilled.</summary>
    MissingComponent,

    /// <summary>A component fails its slot's any-of tag gate.</summary>
    SlotTagMismatch,
}

/// <summary>
/// Everything the item side of fabrication settles before effect generation runs
/// (docs/identity-foundation.md §8.1, §11.5 — D46/D51): the base delivery, the
/// expressed/dormant identity split, the merged personality, and the name.
/// </summary>
public sealed record IdentityComposition(
    IdentityCompositionFailure Failure,
    EquipmentBlueprintDefinition? Form,
    ItemBaseDelivery BaseDelivery,
    IReadOnlyList<IdentityStake> Expressed,
    IReadOnlyList<IdentityStake> Dormant,
    IReadOnlyList<ProvenanceRoot> Roots,
    MergedSignatureProfile Profile,
    int Quality,
    string Name,
    IReadOnlyList<(string Slot, string MaterialName)> Components,
    Stability WildestComponentStability)
{
    public static IdentityComposition Failed(IdentityCompositionFailure failure) => new(
        failure, null, ItemBaseDelivery.None,
        Array.Empty<IdentityStake>(), Array.Empty<IdentityStake>(),
        Array.Empty<ProvenanceRoot>(), MergedSignatureProfile.Neutral,
        IdentityCraftTuning.DefaultQuality, string.Empty,
        Array.Empty<(string, string)>(), Stability.Stable);
}

/// <summary>
/// Composes identity-model components in a form into the item-side facts (D51's rules,
/// stated once here):
///
/// <para><b>Union:</b> the item inherits every active identity its components bring; the
/// same identity from several slots merges at its highest rank. <b>Cap:</b> up to the form's
/// <c>identity_cap</c> express. <b>Selection is readable:</b> slot priority first (the edge
/// speaks before the binding), then rank, then mass contribution, then id — no percentage
/// arithmetic anywhere. <b>Dormancy:</b> the rest are recorded, inert, never deleted.</para>
///
/// <para>Pure composition — no inventory, no randomness, no registration. The mint engine
/// wraps it; previews call it directly (one computation, two callers).</para>
/// </summary>
public static class IdentityEquipmentComposer
{
    /// <param name="formNoun">The noun the item's name builds on — the form's own name, or
    /// one of its variants when the mint engine picked one (deterministically, from the
    /// derived definition id). Null means the plain form name.</param>
    public static IdentityComposition Compose(
        EquipmentBlueprintDefinition form,
        IReadOnlyDictionary<string, (MaterialDefinition Definition, IdentityMaterialState State)> componentsBySlot,
        ContentBundle content,
        string? formNoun = null)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(componentsBySlot);
        ArgumentNullException.ThrowIfNull(content);

        if (form.IdentityCap is not int identityCap)
            return IdentityComposition.Failed(IdentityCompositionFailure.FormNotMigrated);

        foreach (var (slotName, slot) in form.Slots)
        {
            if (!componentsBySlot.TryGetValue(slotName, out var component))
                return IdentityComposition.Failed(IdentityCompositionFailure.MissingComponent);

            if (slot.RequiresTags.Count > 0
                && !slot.RequiresTags.Any(required =>
                    component.Definition.Tags.Contains(required, StringComparer.OrdinalIgnoreCase)))
            {
                return IdentityComposition.Failed(IdentityCompositionFailure.SlotTagMismatch);
            }
        }

        var (expressed, dormant) = SplitByExpression(form, identityCap, componentsBySlot);

        var mergedRoots = RootDerivations.MergeRoots(form.Slots
            .Select(slot => (componentsBySlot[slot.Key].State.Roots, slot.Value.MassShare)));

        // The item's personality and physical floor both derive from the merged roots, the
        // same way an emergent material's do — one derivation rule, two consumers. The lower
        // trace bar keeps a name-worthy provenance generation-worthy too.
        var itemStateProxy = new IdentityMaterialState { Roots = mergedRoots };
        var profile = RootDerivations.ProfileOf(
            itemStateProxy, content.Materials, IdentityCraftTuning.ItemProfileTraceWeight);

        var quality = (int)Math.Round(form.Slots.Sum(slot =>
            componentsBySlot[slot.Key].State.Quality * slot.Value.MassShare));

        var componentNames = form.Slots
            .OrderByDescending(slot => slot.Value.MassShare)
            .ThenBy(slot => slot.Key, StringComparer.Ordinal)
            .Select(slot => (slot.Key, componentsBySlot[slot.Key].Definition.Name))
            .ToList();

        var assembledComponentIds = componentsBySlot.Values
            .Select(component => component.Definition.Id)
            .ToHashSet(StringComparer.Ordinal);

        // Overfilled components make a wilder mint (§10.3): the deepest ladder step among
        // the components is what generation widens by — one gambled edge is enough.
        var wildestStability = componentsBySlot.Values
            .Select(component => component.State.Stability)
            .DefaultIfEmpty(Stability.Stable)
            .Max();

        return new IdentityComposition(
            IdentityCompositionFailure.None,
            form,
            ResolveBaseDelivery(form, componentsBySlot, content),
            expressed,
            dormant,
            mergedRoots,
            profile,
            Math.Clamp(quality, 0, IdentityCraftTuning.MaxQuality),
            IdentityNameGenerator.NameForItem(expressed, mergedRoots, assembledComponentIds, formNoun ?? form.Name, content),
            componentNames,
            wildestStability);
    }

    /// <summary>D51's selection rule. Priority, rank and contribution are all facts the
    /// player can read off the bench — the order is an explanation, not a formula.</summary>
    private static (IReadOnlyList<IdentityStake> Expressed, IReadOnlyList<IdentityStake> Dormant) SplitByExpression(
        EquipmentBlueprintDefinition form,
        int identityCap,
        IReadOnlyDictionary<string, (MaterialDefinition Definition, IdentityMaterialState State)> componentsBySlot)
    {
        var candidates = new Dictionary<string, (int Rank, int SlotPriority, double Contribution)>(StringComparer.Ordinal);

        foreach (var (slotName, slot) in form.Slots)
        {
            foreach (var stake in componentsBySlot[slotName].State.Identities)
            {
                var merged = candidates.TryGetValue(stake.Id, out var existing)
                    ? (Math.Max(existing.Rank, stake.Rank),
                        Math.Max(existing.SlotPriority, slot.IdentityPriority),
                        existing.Contribution + slot.MassShare)
                    : (stake.Rank, slot.IdentityPriority, slot.MassShare);
                candidates[stake.Id] = merged;
            }
        }

        var ordered = candidates
            .OrderByDescending(pair => pair.Value.SlotPriority)
            .ThenByDescending(pair => pair.Value.Rank)
            .ThenByDescending(pair => pair.Value.Contribution)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new IdentityStake(pair.Key, pair.Value.Rank))
            .ToList();

        return (ordered.Take(identityCap).ToList(), ordered.Skip(identityCap).ToList());
    }

    /// <summary>Runs the form's base reads over the components' derived base stats (§11.5):
    /// a named slot reads one component, <c>"*"</c> reads the mass-share-weighted whole.</summary>
    private static ItemBaseDelivery ResolveBaseDelivery(
        EquipmentBlueprintDefinition form,
        IReadOnlyDictionary<string, (MaterialDefinition Definition, IdentityMaterialState State)> componentsBySlot,
        ContentBundle content)
    {
        var baseStatsBySlot = form.Slots.Keys.ToDictionary(
            slotName => slotName,
            slotName => RootDerivations.BaseOf(componentsBySlot[slotName].State, content.Materials),
            StringComparer.Ordinal);

        double ReadResult(IReadOnlyList<BaseReadContribution> reads)
        {
            double total = 0;
            foreach (var read in reads)
            {
                if (read.Slot == BlueprintSlots.AllSlots)
                {
                    total += read.Weight * form.Slots.Sum(slot =>
                        slot.Value.MassShare * BaseStatValue(baseStatsBySlot[slot.Key], read.Stat));
                }
                else if (baseStatsBySlot.TryGetValue(read.Slot, out var slotBase))
                {
                    total += read.Weight * BaseStatValue(slotBase, read.Stat);
                }
            }

            return total;
        }

        double ResultFor(string itemStat) =>
            form.BaseReads.TryGetValue(itemStat, out var reads) ? ReadResult(reads) : 0;

        return new ItemBaseDelivery(
            DamageBonus: Math.Round(ResultFor(IdentityFormVocabulary.Damage) * IdentityFabricationTuning.CombatUnitsPerBasePoint, 2),
            WindupTicks: (int)Math.Round(ResultFor(IdentityFormVocabulary.Speed) * IdentityFabricationTuning.WindupTicksPerSpeedPoint),
            Armor: Math.Round(ResultFor(IdentityFormVocabulary.Armor) * IdentityFabricationTuning.CombatUnitsPerBasePoint, 2));
    }

    private static double BaseStatValue(MaterialBaseStats baseStats, string statName) => statName.ToLowerInvariant() switch
    {
        "heft" => baseStats.Heft,
        "bite" => baseStats.Bite,
        "toughness" => baseStats.Toughness,
        "give" => baseStats.Give,
        _ => 0, // the validator refuses unknown stats; a runtime miss reads as zero, loudly-validated
    };
}
