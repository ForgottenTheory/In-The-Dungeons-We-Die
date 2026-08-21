using Dungeons.Items;

namespace Dungeons.Crafting.Identity;

/// <summary>
/// The ten transformation verbs (docs/transformation-verbs.md §2, D47) — the closed code
/// vocabulary the crafting actions of the identity system compose over. Everything the
/// player clicks is a content entry of <c>verb + parameters + gates + costs + fiction</c>;
/// the verbs themselves are code with one executor each.
/// ⚠ Member names become the <c>verb</c> field values of verb-action content — data, not
/// code symbols. Rename only with the content.
/// </summary>
public enum CraftVerb
{
    /// <summary>Mundane conversion (ore→ingot): tags and base change, identities carry.</summary>
    Process,

    /// <summary>Merge substances — the one verb that moves base stats.</summary>
    Fuse,

    /// <summary>Latent → active, at rank 1. Requires a free slot.</summary>
    Reveal,

    /// <summary>Move an identity from a consumed source into the substrate.</summary>
    Transfer,

    /// <summary>Raise an identity's rank by feeding same-identity sources.</summary>
    Develop,

    /// <summary>Pull an identity out onto a fresh capacity-1 carrier; the source is consumed.</summary>
    Extract,

    /// <summary>Swap an identity in, deliberately ejecting a chosen one (no refund).</summary>
    Displace,

    /// <summary>Improve quality. Gentle — no condition cost.</summary>
    Refine,

    /// <summary>Climb the condition ladder one step (to Worked at best). Gentle.</summary>
    Restore,

    /// <summary>+1 stable capacity, up to the expanded ceiling. Rare and expensive.</summary>
    Expand,
}

/// <summary>Why a verb was refused. Refusals are deterministic and previewable — risk is the
/// only thing that rolls, and only past the refusal gate.</summary>
public enum VerbFailureReason
{
    MissingTargetIdentity,
    MissingOutputDefinition,
    OutputDefinitionUnknown,
    OutputDefinitionNotMigrated,
    NoSources,
    TooManySources,
    IdentityAlreadyActive,
    IdentityNotActive,
    IdentityNotLatent,
    NoFreeSlot,
    OverfillLimit,
    SourceLacksIdentity,
    InsufficientDevelopment,
    RankAtMaximum,
    QualityAtMaximum,
    ConditionAtCeiling,
    CapacityAtCeiling,
    DisplacedIdentityNotActive,
}

/// <summary>What kind of line a verb step is — the typed hook the reaction-log presentation
/// colours by, exactly as the old engine's <c>PropertyChangeKind</c> did.</summary>
public enum VerbStepKind
{
    SubstanceChanged,
    IdentityGained,
    IdentityRemoved,
    LatentRevealed,
    RankRaised,
    ConditionStepped,
    ConditionRestored,
    QualityRaised,
    CapacityExpanded,
    Overfilled,
    CarrierCreated,
    RiskNoted,
}

/// <summary>One line of the verb's explanation — every line states what moved and why.</summary>
public sealed record VerbStep(VerbStepKind Kind, string Detail);

/// <summary>The previewed odds — zero unless the crafter chose the risk (docs/
/// transformation-verbs.md §4): fracture comes from working an overfilled material,
/// destruction from condition-stepping work at Fragile.</summary>
public sealed record VerbRisks(double FractureChance, double DestructionChance)
{
    public static readonly VerbRisks None = new(0, 0);
    public bool Any => FractureChance > 0 || DestructionChance > 0;
}

/// <summary>
/// One verb invocation. States travel through requests directly so crafts chain — the
/// output state of one verb is the substrate of the next. Inventory consumption is the
/// application layer's job (everything in <see cref="Sources"/>, plus the substrate when the
/// verb consumes it, is spent on commit).
/// </summary>
public sealed record VerbRequest
{
    public CraftVerb Verb { get; init; }

    /// <summary>The material being worked. For Fuse, the first input.</summary>
    public required IdentityMaterialState Substrate { get; init; }

    /// <summary>Consumed inputs: the transfer/develop feed, Fuse's other substances.</summary>
    public IReadOnlyList<IdentityMaterialState> Sources { get; init; } = Array.Empty<IdentityMaterialState>();

    /// <summary>Which identity the verb is about (Reveal/Transfer/Develop/Extract/Displace).
    /// May be omitted for Transfer when the source carries exactly one identity.</summary>
    public string? TargetIdentityId { get; init; }

    /// <summary>Displace only: the existing identity deliberately ejected.</summary>
    public string? DisplacedIdentityId { get; init; }

    /// <summary>Process only: the authored definition the substrate converts into.</summary>
    public string? OutputDefinitionId { get; init; }

    /// <summary>
    /// Steadiness from the crafter's practiced hand (migration Phase 5): the acting bench
    /// action's mastery, translated by the application layer into a fraction shaved off both
    /// risk chances. Skill narrows variance, never deletes it — the engine clamps to
    /// <see cref="IdentityCraftTuning.RiskReductionCeiling"/>. 0 for an unpracticed hand.
    /// </summary>
    public double RiskReduction { get; init; }
}

/// <summary>The preview — the same computation as commit with the dice removed, so it
/// cannot lie (the Project/Resolve lesson, kept).</summary>
public sealed record VerbProjection(
    VerbFailureReason? Failure,
    IdentityMaterialState? Result,
    IReadOnlyList<IdentityMaterialState> Produced,
    VerbRisks Risks,
    IReadOnlyList<VerbStep> Steps);

/// <summary>How a committed verb ended.</summary>
public enum VerbResultKind
{
    Refused,
    Succeeded,

    /// <summary>An overfill risk landed: the newest identity broke away and the verb's work
    /// was lost, but the material survives (condition still paid).</summary>
    Fractured,

    /// <summary>A Fragile gamble landed: the material is gone — and still pays byproducts.</summary>
    Destroyed,
}

/// <summary>The committed result. <see cref="Result"/> is null when the substrate was
/// consumed (Extract), destroyed, or the verb refused.</summary>
public sealed record VerbOutcome(
    VerbResultKind Kind,
    VerbFailureReason? Failure,
    IdentityMaterialState? Result,
    IReadOnlyList<IdentityMaterialState> Produced,
    ItemStack? Byproduct,
    string? FracturedIdentityId,
    VerbRisks Risks,
    IReadOnlyList<VerbStep> Steps);
