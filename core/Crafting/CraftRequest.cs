using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>Why a craft could not proceed. <see cref="None"/> means it can.</summary>
public enum CraftFailure
{
    None,
    UnknownProcess,
    UnknownSubstrate,
    UnknownReagent,
    UnknownCatalyst,
    NoReagents,
    InvalidQuantity,
    ProfessionTooLow,

    /// <summary>The substrate lacks a tag the process requires (§7.2 <c>substrate_tags</c>).</summary>
    SubstrateRejected,

    MissingInputs,
}

/// <summary>
/// A craft the player is proposing (docs/emergent-item-system.md §7.1).
///
/// <para><b>Reagents are ordered, and the order is the mechanic.</b> Applying A then B differs
/// from B then A automatically, because the second step acts on a different intermediate
/// state — six outcomes from three reagents, with zero authored triples (§0 Decision 2).</para>
/// </summary>
/// <param name="ProcessId">Which process — the choice that decides what reacts at all.</param>
/// <param name="SubstrateId">The thing being transformed; its identity and lineage carry forward.</param>
/// <param name="ReagentIds">Applied in sequence and consumed.</param>
/// <param name="CatalystId">Modifies rates and is not consumed; transfers nothing of its own.</param>
/// <param name="Quantity">How many units to put through. All units share one deterministic result.</param>
/// <param name="Performance">The active-crafting timing result, 0–1 (§7.4). 0.5 is a passive craft.</param>
public sealed record CraftRequest(
    string ProcessId,
    string SubstrateId,
    IReadOnlyList<string> ReagentIds,
    string? CatalystId = null,
    int Quantity = 1,
    double Performance = 0.5);

/// <summary>
/// What a craft <i>would</i> do, computed before the player commits
/// (docs/emergent-item-system.md §6.2c).
///
/// <para>This exists because integrity 0 destroys the material, and that rule is only fair if
/// destruction is never a surprise. "A system that silently eats eight hours of refinement is
/// a system players stop experimenting with — which defeats the entire design goal."</para>
/// </summary>
public sealed record CraftProjection(
    CraftFailure Failure,
    IntegrityProjection Integrity,
    int ProjectedPotency,
    string ProjectedName,
    bool WouldBeFirstDiscovery,
    ReactionLog Preview,
    MaterialProfile? Projected = null,
    IReadOnlyList<ReactionStepResult>? Steps = null)
{
    public bool CanCraft => Failure == CraftFailure.None;

    /// <summary>Destruction is unavoidable — warn outright rather than showing a percentage.</summary>
    public bool WarnsOfDestruction => CanCraft && Integrity.IsCertainDestruction;

    /// <summary>Destruction is possible; show the percentage (§6.2c).</summary>
    public bool WarnsOfRisk => CanCraft && Integrity.IsAtRisk;

    /// <summary>The typed per-step property movements behind <see cref="Preview"/> — the
    /// semantic layer's input (D30). Read-model exposure only: the numbers are the same ones
    /// the log already narrates.</summary>
    public IReadOnlyList<ReactionStepResult> StepResults => Steps ?? Array.Empty<ReactionStepResult>();

    public static CraftProjection Failed(CraftFailure failure) => new(
        failure,
        new IntegrityProjection(0, 0, 0, 0),
        ProjectedPotency: 0,
        ProjectedName: string.Empty,
        WouldBeFirstDiscovery: false,
        Preview: ReactionLog.Empty);
}

/// <summary>
/// The result of a committed craft (docs/emergent-item-system.md §18).
/// </summary>
/// <param name="ResultItemId">The archetype produced, or null when the material was destroyed.</param>
/// <param name="IsFirstDiscovery">True when nobody had ever produced this signature before.</param>
/// <param name="WasDestroyed">Integrity reached zero (§6.2c). Byproducts are the consolation.</param>
public sealed record CraftOutcome(
    CraftFailure Failure,
    string? ResultItemId,
    string ResultName,
    int Quantity,
    bool IsFirstDiscovery,
    bool WasDestroyed,
    IReadOnlyList<ItemStack> Byproducts,
    ReactionLog Log)
{
    public bool Success => Failure == CraftFailure.None;

    public static CraftOutcome Failed(CraftFailure failure) => new(
        failure, null, string.Empty, 0, false, false, Array.Empty<ItemStack>(), ReactionLog.Empty);
}
