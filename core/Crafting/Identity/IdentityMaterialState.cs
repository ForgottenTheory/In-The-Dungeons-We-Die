namespace Dungeons.Crafting.Identity;

/// <summary>
/// How overfilled a material is (docs/identity-foundation.md §10.3). Always <b>derived</b>
/// from identity count vs capacity — never stored, so it can never drift.
/// ⚠ Member names will become save keys when persistence lands (Phase 7) — rename only with
/// a migration.
/// </summary>
public enum Stability
{
    /// <summary>Within capacity — clean, deterministic crafting.</summary>
    Stable,

    /// <summary>One over capacity: wilder generation, fracture risk on further work.</summary>
    Unstable,

    /// <summary>Two over capacity: the deep end — fracture likely, destruction possible.</summary>
    Volatile,
}

/// <summary>
/// The work budget (docs/identity-foundation.md §10.4) — the visible successor to the old
/// integrity float. Identity-changing verbs step it down; at <see cref="Fragile"/>, further
/// stepping work rolls destruction instead (which still pays byproducts).
/// ⚠ Member names will become save keys when persistence lands — rename only with a migration.
/// </summary>
public enum Condition
{
    /// <summary>Never deeply worked. Restore cannot fake this (virgin-only, provisional).</summary>
    Pristine,

    Worked,
    Strained,

    /// <summary>The last step. Condition-stepping work from here gambles on destruction.</summary>
    Fragile,
}

/// <summary>One identity carried by a material state: the open door and how far it has been
/// developed. Rank is internal (1–4, the effect-family access rungs); presentation renders
/// qualitative language, never numerals (D44).</summary>
public sealed record IdentityStake(string Id, int Rank);

/// <summary>
/// One physical root a material descends from, by contribution weight. Roots are the
/// provenance the fingerprint hashes and the source both the merged signature profile and
/// the derived base stats are computed from (<see cref="RootDerivations"/>) — deliberately
/// bounded and lossy (D45): state is canonical, history only shapes it.
/// </summary>
public sealed record ProvenanceRoot(string DefinitionId, double Weight);

/// <summary>
/// A material's full crafting state under the identity model (docs/identity-foundation.md
/// §11) — the successor to the property-based <see cref="Dungeons.Content.MaterialState"/>,
/// landing beside it during the migration (D42). Eight facets, each readable in a sentence;
/// <see cref="Stability"/> is derived, and the signature profile and base stats are derived
/// from <see cref="Roots"/> rather than stored (§11.3–§11.4).
/// Immutable — every verb produces a new state.
/// </summary>
public sealed record IdentityMaterialState
{
    /// <summary>Active identities in <b>acquisition order</b> — order is meaningful: a
    /// fracture removes the newest (provisional, docs/transformation-verbs.md §7).</summary>
    public IReadOnlyList<IdentityStake> Identities { get; init; } = Array.Empty<IdentityStake>();

    /// <summary>Present but inactive until revealed. Latents never occupy capacity.</summary>
    public IReadOnlyList<string> Latent { get; init; } = Array.Empty<string>();

    /// <summary>Stable identity slots. Authored 1–4; Expand may push it to the expanded
    /// ceiling (<see cref="IdentityCraftTuning.ExpandedCapacityCeiling"/>).</summary>
    public int Capacity { get; init; } = 1;

    public Condition Condition { get; init; } = Condition.Pristine;

    /// <summary>Workmanship, 0–100. Refine raises it; it buckets into the fingerprint.</summary>
    public int Quality { get; init; } = IdentityCraftTuning.DefaultQuality;

    /// <summary>True for prepared vessels made by Extract. Carriers deliver their full rank
    /// on Transfer — preparation = fidelity (docs/transformation-verbs.md §3).</summary>
    public bool IsCarrier { get; init; }

    /// <summary>Bounded, weight-normalised physical provenance. For an unworked authored
    /// material this is itself at weight 1.</summary>
    public IReadOnlyList<ProvenanceRoot> Roots { get; init; } = Array.Empty<ProvenanceRoot>();

    /// <summary>Derived, never stored: count vs capacity is the whole fact (§10.3).</summary>
    public Stability Stability =>
        Identities.Count <= Capacity ? Stability.Stable
        : Identities.Count == Capacity + 1 ? Stability.Unstable
        : Stability.Volatile;

    public bool Carries(string identityId) =>
        Identities.Any(stake => string.Equals(stake.Id, identityId, StringComparison.Ordinal));

    /// <summary>The stake for <paramref name="identityId"/>, or null if not active.</summary>
    public IdentityStake? StakeOf(string identityId) =>
        Identities.FirstOrDefault(stake => string.Equals(stake.Id, identityId, StringComparison.Ordinal));
}
