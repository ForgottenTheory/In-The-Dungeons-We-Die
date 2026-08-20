namespace Dungeons.Content;

/// <summary>
/// The Signature grammar's vocabulary registries (docs/identity-foundation.md §7, D43).
/// A signature effect is a sentence — trigger → behavior → payload — and these definitions
/// are the words the generator may compose. They are <b>keys, not entities</b>: ids are bare
/// (<c>on_block</c>, <c>store</c>, <c>renewal</c>), the same bargain the property registry
/// strikes, so authored Signature Profiles read exactly as designed
/// (<c>"favored_triggers": ["on_block"]</c>).
///
/// <para>The D30 fence, enforced by <see cref="ContentValidator"/>: a vocabulary entry may
/// only be registered by binding to machinery that resolves in play. A trigger names a real
/// <see cref="Dungeons.Events.GameEvents"/> event (or is the one standing shape); behaviors
/// ship only when their assembler's machinery exists. Weirdness is unrestricted; lying to
/// the player is impossible.</para>
///
/// <para>The <b>payload</b> registry is deliberately absent here — its shape (family
/// ownership, rungs, machinery bindings) belongs to the Signature Resolver and arrives with
/// migration Phase 3.</para>
/// </summary>
public sealed class SignatureTriggerDefinition : IDefinition
{
    /// <summary>Bare key, e.g. <c>on_block</c>.</summary>
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The <see cref="Dungeons.Events.GameEvents"/> event this trigger compiles onto.
    /// Exactly one of <see cref="Event"/> / <see cref="Standing"/> is set — validated.
    /// </summary>
    public string? Event { get; init; }

    /// <summary>
    /// True for the one trigger that is not an event: <c>while_worn</c> compiles to standing
    /// modifier grants through the equip/unequip pipeline rather than to a rule.
    /// </summary>
    public bool Standing { get; init; }

    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// A behavior verb — <em>how</em> a payload is delivered (amplify, store, retaliate…). Each
/// shipped verb gets one registered assembler when the Signature Resolver lands (Phase 3);
/// until then the registry is the settled vocabulary of docs/identity-foundation.md §7.3.
/// The three designed verbs whose machinery does not exist yet (detonate, spread, bloom) are
/// deliberately <b>not</b> shipped — the D30 fence — and <c>IdentityContentTests</c> names
/// them so adding one is an act of intent, not drift.
/// </summary>
public sealed class SignatureBehaviorDefinition : IDefinition
{
    /// <summary>Bare key, e.g. <c>store</c>.</summary>
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>What the assembler composes, in one sentence — the reader's contract.</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// A theme — hidden scoring metadata, nothing more (docs/identity-foundation.md §6.1, D43).
/// Themes resonate between sources during signature scoring and are <b>never player-facing</b>;
/// the name exists for authoring tools only.
/// </summary>
public sealed class SignatureThemeDefinition : IDefinition
{
    /// <summary>Bare key, e.g. <c>renewal</c>.</summary>
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
