namespace Dungeons.Crafting.Identity;

/// <summary>
/// Every tuned number in the item-effect pipeline, named and in one place.
/// ⚠ <b>All provisional until play</b> — the shapes are approved design (D50/D51,
/// docs/identity-foundation.md §8), the values are first guesses.
/// </summary>
public static class ItemEffectTuning
{
    // ---- Magnitude positioning -----------------------------------------------------------

    /// <summary>Where in a payload's [lo, hi] range a zero-quality delivery lands. A floor
    /// rather than zero — quality is roll <i>quality</i>, never a gate (the proven
    /// material-strength lever, re-aimed at workmanship).</summary>
    public const double MinimumRangePosition = 0.35;

    /// <summary>How much of the range quality buys on top of the minimum; the two sum to 1,
    /// so quality 100 reaches the top.</summary>
    public const double QualityRangeSpan = 0.65;

    /// <summary>Deterministic floor bonus per rank the identity holds beyond the payload's
    /// rung — development deepens the guarantee, without dice (D50 category 1).</summary>
    public const double FloorPositionPerExtraRank = 0.15;

    /// <summary>±band of seeded variance on generated (never floor) magnitudes.</summary>
    public const double GeneratedRollVariance = 0.10;

    // ---- Sentence counts and odds --------------------------------------------------------

    /// <summary>Ordinary generated sentences on any item with candidates at all.</summary>
    public const int GeneratedSentenceBaseCount = 1;

    /// <summary>Quality at or above this earns one more generated sentence.</summary>
    public const int HighQualityBonusThreshold = 70;

    /// <summary>Ceiling on ordinary generated sentences, whatever earned them.</summary>
    public const int MaxGeneratedSentences = 3;

    /// <summary>The scored candidate table's size — what the preview shows IS what the
    /// draws come from, so truncation here is honest, not cosmetic.</summary>
    public const int MaxCandidateTableSize = 16;

    /// <summary>Diversity cap: one payload keeps at most this many rows of the table. Without
    /// it, four open payloads × their behavior permutations fill the whole table and an
    /// authored breach payload (§9) can never surface — the table must represent payload
    /// variety, not behavior spelling.</summary>
    public const int MaxCandidatesPerPayload = 3;

    // ---- Scoring factors -------------------------------------------------------------------

    /// <summary>How strongly one point of merged-profile lean weight multiplies a matching
    /// candidate: factor = 1 + weight × this.</summary>
    public const double ProfileLeanFactor = 1.5;

    /// <summary>Multiplier a form's generation-profile lean applies to matching candidates
    /// (§8 stage 1 — the buckler's on_block thumb on the scale).</summary>
    public const double FormLeanFactor = 2.0;

    // ---- Signatures (D50 category 3) -------------------------------------------------------

    /// <summary>Base odds a mint earns a Signature at all.</summary>
    public const double SignatureBaseChance = 0.15;

    /// <summary>Signature odds per point of theme resonance (the summed merged-profile theme
    /// weights) — themes are scoring metadata, and this is where they score (§6.1).</summary>
    public const double SignatureChancePerResonance = 0.5;

    /// <summary>Overfilled components raise the odds the mint turns special (§10.3): the
    /// approved "wilder generation" made concrete.</summary>
    public const double SignatureChanceUnstableBonus = 0.15;
    public const double SignatureChanceVolatileBonus = 0.30;

    /// <summary>Signature odds never reach certainty — earned, not owed.</summary>
    public const double SignatureChanceCeiling = 0.9;

    /// <summary>Sentences a Signature bundles (§7.1's advanced shape). Coherence is scored,
    /// not enforced: the second sentence must share the first's trigger or a family.</summary>
    public const int SignatureSentenceCount = 2;

    // ---- Drawbacks (§10.3) -----------------------------------------------------------------

    /// <summary>Odds a Volatile-component mint carries a drawback sentence.</summary>
    public const double DrawbackChanceVolatile = 0.35;

    /// <summary>The drawback's own proc chance — a curse that fires every swing would
    /// dominate the item instead of shadowing it.</summary>
    public const double DrawbackProcChance = 0.2;

    /// <summary>Drawback magnitudes sit at the bottom of the payload's range.</summary>
    public const double DrawbackRangePosition = 0.35;

    // ---- Assembler numbers -----------------------------------------------------------------

    /// <summary>Ticks an amplify grant lasts after its trigger.</summary>
    public const int AmplifyDurationTicks = 30;

    /// <summary>Ticks an imbue move-rewrite lasts after its trigger.</summary>
    public const int ImbueDurationTicks = 30;

    /// <summary>Health paid per point of payload magnitude by exchange — the pact price.</summary>
    public const double ExchangeHealthCostPerPoint = 0.5;

    /// <summary>What exchange's payload magnitude is multiplied by — paying must buy
    /// something a plain sentence doesn't get.</summary>
    public const double ExchangeMagnitudeBoost = 1.5;

    /// <summary>Store gauges: capacity, fill per triggering event, and the fill fraction the
    /// band bonus turns on at. The release-on-full shape waits for a gauge-spend effect kind
    /// (drainResource has no handler today) — the band shape is the compile that resolves.</summary>
    public const double StoreGaugeMax = 100;
    public const double StoreFeedPerTrigger = 25;
    public const double StoreBandThreshold = 0.5;
}
