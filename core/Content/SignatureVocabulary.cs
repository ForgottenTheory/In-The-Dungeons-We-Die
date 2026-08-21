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
/// <para>The <b>payload</b> registry (<see cref="SignaturePayloadDefinition"/>, below)
/// arrived with migration Phase 3 (D50) and completes the sentence vocabulary.</para>
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

/// <summary>The closed set of things a payload may bind to — each one names machinery that
/// already resolves in play, which is the D30 fence in vocabulary form. There is deliberately
/// no lane-specifying damage binding: the rule engine's damage effect is lane-less, and a
/// binding the machinery cannot honor would be a lie waiting for a tooltip.</summary>
public static class PayloadBindingKinds
{
    /// <summary>A registered modifier key (<see cref="PayloadBinding.Key"/>), optionally
    /// scoped. Sustain's natural food; the standing floor's usual shape.</summary>
    public const string Modifier = "modifier";

    /// <summary>A status id — afflict's food, and every Burn/Barrier economy's.</summary>
    public const string Status = "status";

    /// <summary>Plain effect damage (<c>CombatEncounter.DealEffectDamage</c> — lane-less).</summary>
    public const string Damage = "damage";

    public const string Heal = "heal";

    /// <summary>A resource grant: health, mana or stamina (<see cref="PayloadBinding.Key"/>).</summary>
    public const string Resource = "resource";

    /// <summary>A move id, for echo (<c>triggerMove</c>) and move grants.</summary>
    public const string Move = "move";

    /// <summary>A move-modifier id, for imbue (<c>modifyMove</c>) and convert.</summary>
    public const string MoveModifier = "moveModifier";

    /// <summary>An item id, for <c>grantItem</c> — Charmed's yield turf.</summary>
    public const string Item = "item";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Modifier, Status, Damage, Heal, Resource, Move, MoveModifier, Item,
    };

    /// <summary>Kinds whose rolled magnitude means something — these must author a range.</summary>
    public static readonly IReadOnlySet<string> MagnitudeBearing = new HashSet<string>(StringComparer.Ordinal)
    {
        Modifier, Status, Damage, Heal, Resource,
    };

    /// <summary>Valid <see cref="PayloadBinding.Key"/> values for the resource kind.</summary>
    public static readonly IReadOnlySet<string> ResourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "health", "mana", "stamina",
    };
}

/// <summary>What a payload concretely delivers — the machinery reference the validator proves
/// resolves. <see cref="Key"/>'s meaning depends on <see cref="Kind"/>: a modifier key, a
/// status id, a resource name, a move id, a move-modifier id, an item id — or nothing for
/// plain damage/heal.</summary>
public sealed class PayloadBinding
{
    /// <summary>One of <see cref="PayloadBindingKinds"/>.</summary>
    public string Kind { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    /// <summary>Optional <c>dimension:value</c> scope for modifier bindings (e.g.
    /// <c>status:status.burn</c>). Must match the key's declared <c>scoped_by</c> dimension.</summary>
    public string? Scope { get; init; }
}

/// <summary>One identity family a payload belongs to, and how deep into the family it sits:
/// rung 1–4 (basic → improved → advanced → build-changing, §4). An identity of rank R opens
/// its families' payloads of rung ≤ R.</summary>
public sealed class PayloadFamilyStake
{
    /// <summary>An <c>identity.*</c> id.</summary>
    public string Identity { get; init; } = string.Empty;

    public int Rung { get; init; } = 1;
}

/// <summary>
/// The guaranteed floor sentence a floor payload compiles into (D50 category 1): the authored
/// trigger and behavior this payload is delivered through for every item whose active identity
/// opens it. Guaranteed means the sentence is always <i>present</i> — a floor sentence may
/// still carry a proc chance (Ember's on-hit Burn), and that chance is part of the promise.
/// </summary>
public sealed class PayloadFloorSentence
{
    /// <summary>A trigger registry id (<c>while_worn</c>, <c>on_hit</c>…).</summary>
    public string Trigger { get; init; } = string.Empty;

    /// <summary>A behavior registry id (<c>sustain</c>, <c>afflict</c>…).</summary>
    public string Behavior { get; init; } = string.Empty;

    /// <summary>Firing probability where the trigger rolls, 0–1. Certain by default.</summary>
    public double Chance { get; init; } = 1.0;
}

/// <summary>
/// A payload — <em>what</em> a signature sentence delivers (docs/identity-foundation.md §7.4,
/// D50). The registry entry binds to live machinery (the D30 fence), declares which identity
/// families own it and at which access rung, and carries the default generation weight. The
/// grammar's third and final vocabulary: with triggers and behaviors it completes the
/// trigger × behavior × payload sentence space the item-effect pipeline generates over.
/// A record so tests and tools can derive variants with <c>with</c>; content deserializes
/// identically to the class-shaped registries.
/// </summary>
public sealed record SignaturePayloadDefinition : IDefinition
{
    /// <summary>Bare key, e.g. <c>regeneration</c> — profiles read as designed
    /// (<c>"favored_payloads": ["regeneration"]</c>).</summary>
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>The identity families that own this payload. At least one; an identity's rank
    /// gates how deep into the family generation may reach.</summary>
    public IReadOnlyList<PayloadFamilyStake> Families { get; init; } = Array.Empty<PayloadFamilyStake>();

    public PayloadBinding Binding { get; init; } = new();

    /// <summary>[lo, hi] magnitude in mechanical units (combat units for damage/heal, the
    /// modifier's own units for modifier bindings). Where in the range a delivery lands is the
    /// pipeline's quality/rank lever. Required for magnitude-bearing binding kinds.</summary>
    public IReadOnlyList<double> Range { get; init; } = Array.Empty<double>();

    /// <summary>Default scoring weight in candidate generation (profile and form biases
    /// multiply it).</summary>
    public double Weight { get; init; } = 10;

    /// <summary>Present on exactly one payload per owning identity: the family's guaranteed
    /// floor expression (D50 category 1). Floor payloads sit at rung 1 — the floor is what
    /// carrying the identity at all promises.</summary>
    public PayloadFloorSentence? Floor { get; init; }

    /// <summary>What the payload does, in one reader-facing sentence.</summary>
    public string Description { get; init; } = string.Empty;
}
