using Dungeons.Content;

namespace Dungeons.Crafting.Identity;

/// <summary>
/// Every tuned number in the identity crafting engine, named and in one place. ⚠ <b>All
/// provisional until play</b> (docs/transformation-verbs.md §7, docs/identity-foundation.md
/// §10) — the shapes are approved design, the values are first guesses.
/// </summary>
public static class IdentityCraftTuning
{
    /// <summary>Rank a transfer from a raw (non-carrier) source delivers — the identity
    /// arrives in its shallowest form. Carriers deliver their full rank instead:
    /// preparation = fidelity, the rule that interlocks the professions (D47).</summary>
    public const int RawTransferRank = 1;

    /// <summary>Highest development rank — the build-changing rung. Shares the validator's
    /// value by construction so engine and content rules cannot drift.</summary>
    public const int MaxRank = ContentValidator.MaxIdentityRank;

    /// <summary>Develop's feeding cost: points needed to raise a rank, indexed by the
    /// current rank (1→2 costs 2, 2→3 costs 4, 3→4 costs 8). A source feeds points equal to
    /// its own rank — deep sources feed more. There is no partial progress: a Develop that
    /// cannot pay is refused, which keeps development out of the fingerprint and identical
    /// states stacking (provisional).</summary>
    public static int DevelopCostToLeaveRank(int currentRank) => currentRank switch
    {
        1 => 2,
        2 => 4,
        3 => 8,
        _ => int.MaxValue,
    };

    /// <summary>Expand may push capacity one past the authored ceiling — the gambit space
    /// needs headroom, but not much (provisional).</summary>
    public const int ExpandedCapacityCeiling = ContentValidator.MaxCapacity + 1;

    /// <summary>How far past capacity a material can be pushed at all. Two over is Volatile;
    /// a third is refused — "the material cannot hold more" is a wall, not a roll.</summary>
    public const int OverfillHardLimit = 2;

    /// <summary>Fracture odds when working an overfilled material — the newest identity is
    /// lost (provisional; docs/transformation-verbs.md §7 #3).</summary>
    public const double FractureChanceUnstable = 0.15;
    public const double FractureChanceVolatile = 0.35;

    /// <summary>Destruction odds when a condition-stepping verb is attempted at Fragile —
    /// three safe identity-changing actions, then every further one gambles. Destruction
    /// still pays byproducts, always.</summary>
    public const double DestructionChanceWhenFragile = 0.35;

    /// <summary>Workmanship defaults and steps. Quality buckets into the fingerprint so
    /// near-identical workmanship still stacks.</summary>
    public const int DefaultQuality = 50;
    public const int MaxQuality = 100;
    public const int RefineQualityStep = 10;
    public const int QualityFingerprintBucket = 10;

    /// <summary>Restore climbs to Worked at best — Pristine is virgin-only (provisional,
    /// docs/transformation-verbs.md §7 #2).</summary>
    public const Condition RestoreCeiling = Condition.Worked;

    /// <summary>Provenance bounds (D45): three roots, trace-pruned, bucketed for the
    /// fingerprint. History shapes state; state is canonical.</summary>
    public const int MaxRoots = 3;
    public const double RootTraceWeight = 0.05;
    public const double RootWeightBucket = 0.10;

    /// <summary>Merged-profile bounds (§11.4): weighted union of the roots' authored
    /// profiles, trace-pruned and capped per list so profiles never grow without bound.</summary>
    public const int MaxProfileEntriesPerList = 4;
    public const double ProfileTraceWeight = 0.15;
}
