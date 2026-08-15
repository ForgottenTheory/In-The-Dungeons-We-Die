namespace Dungeons.Crafting;

/// <summary>
/// How finely the result state is bucketed before it is hashed into a material's identity
/// (docs/emergent-item-system.md §12.1).
///
/// <para><b>§21 names <see cref="PropertyBucket"/> the single highest-risk tuning number in
/// the design.</b> Too coarse and the whole emergent space collapses into a handful of
/// materials; too fine and the registry floods with meaningless neighbours nobody can tell
/// apart. It starts at 5 per the spec and must be tuned by measurement once P1 is playable —
/// treat the current value as provisional, not settled.</para>
/// </summary>
public static class QuantizationTuning
{
    /// <summary>Property values are rounded to the nearest multiple of this before hashing.</summary>
    public const double PropertyBucket = 5.0;

    /// <summary>Potency is rounded to the nearest multiple of this before hashing.</summary>
    public const double PotencyBucket = 5.0;

    /// <summary>Lineage root weights are rounded to the nearest multiple of this (§12.1).</summary>
    public const double LineageWeightBucket = 0.10;

    /// <summary>
    /// How much of the variance magnitude becomes an actual property offset (§12.3). Second
    /// only to <see cref="PropertyBucket"/> in importance: together they decide how many
    /// buckets a bad roll scatters you across, which is the difference between "I missed by a
    /// little" and "I have no idea what I just made".
    /// </summary>
    public const double VarianceScale = 0.15;

    /// <summary>Prefix of a generated archetype id, e.g. <c>emergent.7f3a91c4</c>.</summary>
    public const string SignaturePrefix = "emergent.";

    /// <summary>Hex characters of the hash kept in the signature.</summary>
    public const int SignatureLength = 8;
}
