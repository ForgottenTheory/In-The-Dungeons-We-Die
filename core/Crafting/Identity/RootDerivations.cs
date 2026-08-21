using Dungeons.Content;

namespace Dungeons.Crafting.Identity;

/// <summary>One entry of a merged signature profile: a vocabulary key and how strongly the
/// material's provenance leans toward it.</summary>
public sealed record WeightedLean(string Id, double Weight);

/// <summary>A signature profile as the resolver consumes it — the weighted union of the
/// roots' authored profiles (docs/identity-foundation.md §11.4).</summary>
public sealed record MergedSignatureProfile(
    IReadOnlyList<WeightedLean> Themes,
    IReadOnlyList<WeightedLean> FavoredTriggers,
    IReadOnlyList<WeightedLean> FavoredBehaviors,
    IReadOnlyList<WeightedLean> FavoredPayloads)
{
    public static readonly MergedSignatureProfile Neutral = new(
        Array.Empty<WeightedLean>(), Array.Empty<WeightedLean>(),
        Array.Empty<WeightedLean>(), Array.Empty<WeightedLean>());
}

/// <summary>
/// Everything a state derives from its provenance roots rather than storing: the merged
/// signature profile and the base stats (docs/identity-foundation.md §11.3–§11.5). Derived,
/// not stored, so the fingerprint only needs the roots and neither can ever drift from them.
/// </summary>
public static class RootDerivations
{
    /// <summary>
    /// The weighted union of the roots' authored profiles: weights renormalised over total
    /// root weight, trace entries pruned, each list capped — profiles never grow without
    /// bound. A material whose roots author nothing is neutral (§6).
    /// </summary>
    /// <param name="traceWeight">The prune bar. Materials use the default
    /// (<see cref="IdentityCraftTuning.ProfileTraceWeight"/>); item composition passes the
    /// lower <see cref="IdentityCraftTuning.ItemProfileTraceWeight"/> because assembly
    /// dilutes every share by slot mass.</param>
    public static MergedSignatureProfile ProfileOf(
        IdentityMaterialState state, DataStore<MaterialDefinition> materials,
        double traceWeight = IdentityCraftTuning.ProfileTraceWeight)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(materials);

        var totalWeight = state.Roots.Sum(root => root.Weight);
        if (totalWeight <= 0)
            return MergedSignatureProfile.Neutral;

        var themes = new Dictionary<string, double>(StringComparer.Ordinal);
        var triggers = new Dictionary<string, double>(StringComparer.Ordinal);
        var behaviors = new Dictionary<string, double>(StringComparer.Ordinal);
        var payloads = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var root in state.Roots)
        {
            if (!materials.TryGetById(root.DefinitionId, out var definition)
                || definition.SignatureProfile is not { } profile)
            {
                continue;
            }

            var share = root.Weight / totalWeight;
            Accumulate(themes, profile.Themes, share);
            Accumulate(triggers, profile.FavoredTriggers, share);
            Accumulate(behaviors, profile.FavoredBehaviors, share);
            Accumulate(payloads, profile.FavoredPayloads, share);
        }

        return new MergedSignatureProfile(
            Bounded(themes, traceWeight), Bounded(triggers, traceWeight),
            Bounded(behaviors, traceWeight), Bounded(payloads, traceWeight));
    }

    /// <summary>The contribution-weighted blend of the roots' base stats, rounded to the
    /// 0–10 integers items read (§11.5). Roots without a base block contribute zeros.</summary>
    public static MaterialBaseStats BaseOf(
        IdentityMaterialState state, DataStore<MaterialDefinition> materials)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(materials);

        var totalWeight = state.Roots.Sum(root => root.Weight);
        if (totalWeight <= 0)
            return new MaterialBaseStats();

        double heft = 0, bite = 0, toughness = 0, give = 0;
        foreach (var root in state.Roots)
        {
            if (!materials.TryGetById(root.DefinitionId, out var definition)
                || definition.Base is not { } baseStats)
            {
                continue;
            }

            var share = root.Weight / totalWeight;
            heft += baseStats.Heft * share;
            bite += baseStats.Bite * share;
            toughness += baseStats.Toughness * share;
            give += baseStats.Give * share;
        }

        return new MaterialBaseStats
        {
            Heft = (int)Math.Round(heft),
            Bite = (int)Math.Round(bite),
            Toughness = (int)Math.Round(toughness),
            Give = (int)Math.Round(give),
        };
    }

    /// <summary>
    /// Merges contributing root lists into one bounded, renormalised set — used by Fuse and
    /// by carrier creation. Weights are renormalised to sum 1, trace roots pruned, and the
    /// list capped at the strongest <see cref="IdentityCraftTuning.MaxRoots"/> (D45: bounded
    /// and lossy on purpose).
    /// </summary>
    public static IReadOnlyList<ProvenanceRoot> MergeRoots(
        IEnumerable<(IReadOnlyList<ProvenanceRoot> Roots, double Share)> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var merged = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (roots, share) in contributions)
        {
            var rootTotal = roots.Sum(root => root.Weight);
            if (rootTotal <= 0)
                continue;
            foreach (var root in roots)
                merged[root.DefinitionId] = merged.GetValueOrDefault(root.DefinitionId)
                    + share * (root.Weight / rootTotal);
        }

        var kept = merged
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(IdentityCraftTuning.MaxRoots)
            .Where(pair => pair.Value >= IdentityCraftTuning.RootTraceWeight)
            .ToList();

        var keptTotal = kept.Sum(pair => pair.Value);
        if (keptTotal <= 0)
            return Array.Empty<ProvenanceRoot>();

        return kept
            .Select(pair => new ProvenanceRoot(pair.Key, pair.Value / keptTotal))
            .ToArray();
    }

    private static void Accumulate(
        Dictionary<string, double> into, IReadOnlyList<string> entries, double share)
    {
        foreach (var entry in entries)
            into[entry] = into.GetValueOrDefault(entry) + share;
    }

    private static IReadOnlyList<WeightedLean> Bounded(
        Dictionary<string, double> accumulated, double traceWeight) =>
        accumulated
            .Where(pair => pair.Value >= traceWeight)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(IdentityCraftTuning.MaxProfileEntriesPerList)
            .Select(pair => new WeightedLean(pair.Key, pair.Value))
            .ToArray();
}
