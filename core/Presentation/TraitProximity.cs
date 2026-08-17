using Dungeons.Crafting;
using Dungeons.Items;

namespace Dungeons.Presentation;

/// <summary>What a nearby trait still needs: more of a property, or less of it.</summary>
public sealed record TraitNeed(string Property, bool NeedsMore, double Deficit);

/// <summary>A trait within reach of the current state — §15.4's proximity hint, computed from
/// the same authored conditions the resolver births from, so the hint can never lie.</summary>
public sealed record NearbyTrait(TraitDefinition Trait, IReadOnlyList<TraitNeed> Needs)
{
    public double TotalDeficit => Needs.Sum(n => n.Deficit);
}

public static class TraitProximity
{
    /// <summary>
    /// Scans state-born trait conditions against <paramref name="state"/> and reports the ones
    /// that are close: at least one condition unmet (met means it is being born, not near),
    /// at most <see cref="PresentationTuning.ProximityMaxUnmet"/> unmet, and no single deficit
    /// wider than <see cref="PresentationTuning.ProximityWindow"/>. Traits already carried are
    /// skipped; merge-born traits have no state condition to be near. Ordered nearest-first.
    /// </summary>
    public static IReadOnlyList<NearbyTrait> Scan(
        PropertySet state,
        IEnumerable<TraitDefinition> traits,
        IReadOnlyCollection<string> alreadyCarried)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(traits);
        ArgumentNullException.ThrowIfNull(alreadyCarried);

        var nearby = new List<NearbyTrait>();

        foreach (var trait in traits)
        {
            if (!trait.IsStateBorn || alreadyCarried.Contains(trait.Id))
                continue;

            var needs = new List<TraitNeed>();
            var withinWindow = true;

            foreach (var (property, range) in trait.Condition)
            {
                var value = state.Get(property);
                if (range.Contains(value))
                    continue;

                var needsMore = range.Min is { } min && value < min;
                var deficit = needsMore
                    ? range.Min!.Value - value
                    : value - range.Max!.Value;

                if (deficit > PresentationTuning.ProximityWindow)
                {
                    withinWindow = false;
                    break;
                }

                needs.Add(new TraitNeed(property, needsMore, deficit));
            }

            if (withinWindow && needs.Count > 0 && needs.Count <= PresentationTuning.ProximityMaxUnmet)
                nearby.Add(new NearbyTrait(trait, needs));
        }

        return nearby
            .OrderBy(n => n.TotalDeficit)
            .ThenBy(n => n.Trait.Id, StringComparer.Ordinal)
            .ToList();
    }
}
