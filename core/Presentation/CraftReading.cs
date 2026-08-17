using Dungeons.Content;
using Dungeons.Crafting;

namespace Dungeons.Presentation;

/// <summary>An opposition pair the craft is fighting through (heat ⇄ cold).</summary>
public sealed record QualityConflictReading(PropertyMovement Movement, string Opposite);

/// <summary>A trait the projected state would give birth to, by name.</summary>
public sealed record TraitBirthReading(string TraitId, string Name, string Drawback);

/// <summary>
/// The pre-commit panel's view model (docs/presentation-architecture.md §3) — everything the
/// player needs to hypothesize an outcome, grouped by what it means, with no raw values.
/// Derived one-way from <see cref="CraftPreview"/>; the §6.2c guarantees ride on
/// <see cref="Risk"/> and <see cref="Workability"/> exactly as before.
/// </summary>
public sealed record CraftReading(
    bool CanCraft,
    CraftFailure Failure,
    string SubstrateName,
    string ProjectedName,
    bool FirstDiscovery,
    PropertyTier Expression,
    int ExpressionShift,
    IReadOnlyList<PropertyMovement> Strengthening,
    IReadOnlyList<PropertyMovement> Weakening,
    IReadOnlyList<PropertyMovement> WashingOut,
    IReadOnlyList<QualityConflictReading> Opposition,
    IReadOnlyList<TraitBirthReading> TraitBirths,
    IReadOnlyList<NearbyTrait> NearbyTraits,
    IReadOnlyList<EssenceReading> Essence,
    bool VesselStressed,
    RiskBand Risk,
    WorkabilityProjection Workability);

public static class CraftReadings
{
    /// <summary>A reading for a craft that cannot proceed — the failure is the whole story.</summary>
    public static CraftReading Failed(CraftFailure failure, string substrateName) => new(
        false, failure, substrateName, string.Empty, false,
        PropertyTier.None, 0,
        Array.Empty<PropertyMovement>(), Array.Empty<PropertyMovement>(),
        Array.Empty<PropertyMovement>(), Array.Empty<QualityConflictReading>(),
        Array.Empty<TraitBirthReading>(), Array.Empty<NearbyTrait>(),
        Array.Empty<EssenceReading>(), false,
        RiskBand.Safe, new WorkabilityProjection(0, 0, 0, 0));

    /// <summary>
    /// Builds the reading from a projection. <paramref name="substrateProfile"/> supplies the
    /// before-state (trait diff, material strength direction); content supplies names, opposition
    /// partners and trait conditions. Pure and deterministic — same projection, same reading.
    /// </summary>
    public static CraftReading From(
        CraftPreview projection,
        string substrateName,
        MaterialState substrateProfile,
        ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(substrateProfile);
        ArgumentNullException.ThrowIfNull(content);

        if (!projection.CanCraft)
            return Failed(projection.Failure, substrateName) with { Workability = projection.Workability };

        var movements = Trends.Aggregate(projection.StepResults);
        var glossary = new PropertyGlossary(content.Properties);

        var strengthening = movements.Where(m => m.Trend is Trend.Rising or Trend.Emerging).ToList();
        var weakening = movements.Where(m => m.Trend == Trend.Falling).ToList();
        var washingOut = movements.Where(m => m.Trend is Trend.Fading or Trend.Vanishing).ToList();
        var opposition = movements
            .Where(m => m.Trend == Trend.Conflicting)
            .Select(m => new QualityConflictReading(m, glossary.Opposes(m.Property) ?? "its opposite"))
            .ToList();

        var projected = projection.Projected;

        var births = new List<TraitBirthReading>();
        var nearby = (IReadOnlyList<NearbyTrait>)Array.Empty<NearbyTrait>();
        var essence = (IReadOnlyList<EssenceReading>)Array.Empty<EssenceReading>();
        var strained = false;

        if (projected is not null)
        {
            var before = substrateProfile.Traits.Select(t => t.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var trait in projected.Traits)
            {
                if (before.Contains(trait.Id))
                    continue;

                births.Add(content.Traits.TryGetById(trait.Id, out var def)
                    ? new TraitBirthReading(trait.Id, def.Name, def.Drawback)
                    : new TraitBirthReading(trait.Id, trait.Id, string.Empty));
            }

            var carried = projected.Traits.Select(t => t.Id).ToList();
            nearby = TraitProximity.Scan(projected.Properties, content.Traits.GetAll(), carried);

            essence = projected.Essence
                .Where(e => e.Value > 0)
                .OrderByDescending(e => e.Value)
                .ThenBy(e => e.Key, StringComparer.Ordinal)
                .Select(e => new EssenceReading(e.Key, EssenceName(content, e.Key), Tiers.Of(e.Value)))
                .ToList();

            strained = EssenceTuning.Stress(
                projected.Essence,
                projected.Properties.Get(Dungeons.Items.ItemProperties.Resonance)) > 0;
        }

        return new CraftReading(
            true,
            CraftFailure.None,
            substrateName,
            projection.ProjectedName,
            projection.WouldBeFirstDiscovery,
            Tiers.Of(projection.ProjectedMaterialStrength),
            Math.Sign(projection.ProjectedMaterialStrength - substrateProfile.MaterialStrength),
            strengthening,
            weakening,
            washingOut,
            opposition,
            births,
            nearby,
            essence,
            strained,
            Risk.Of(projection.Workability),
            projection.Workability);
    }

    private static string EssenceName(ContentBundle content, string key)
    {
        foreach (var essence in content.Essences.GetAll())
        {
            if (string.Equals(essence.Key, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(essence.Id, key, StringComparison.OrdinalIgnoreCase))
                return essence.Name;
        }

        return key.Length > 1 ? char.ToUpperInvariant(key[0]) + key[1..] : key;
    }
}
