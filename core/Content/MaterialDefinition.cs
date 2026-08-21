using System.Text.Json.Serialization;
using Dungeons.Items;

namespace Dungeons.Content;

/// <summary>A named quantitative property value (e.g. toxin_resistance 0.05). Used to
/// report a crafting outcome's derived properties; material definitions store their
/// properties as a flat name→value map (see <see cref="MaterialDefinition.Properties"/>).</summary>
public sealed class MaterialProperty
{
    public string Property { get; init; } = string.Empty;
    public double Value { get; init; }
}

/// <summary>One identity carried by a material: which door is open, and how far it has been
/// developed. Rank is an internal integer (1–4) mapping to the effect-family access rungs;
/// presentation renders qualitative language, never numerals (D44).</summary>
public sealed class IdentityGrant
{
    /// <summary>An <c>identity.*</c> id from the identity registry.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Development rank, 1–4. Omitted in JSON means 1 — the rank a reveal or a raw
    /// transfer delivers (docs/transformation-verbs.md §3).</summary>
    public int Rank { get; init; } = 1;
}

/// <summary>
/// The mundane physical floor — four visible stats on 0–10 (docs/identity-foundation.md
/// §11.5, D46). Absent stats read as zero; the dividing-line rule keeps this at four forever:
/// anything that only matters for special or magical behavior is an identity, not a base stat.
/// </summary>
public sealed class MaterialBaseStats
{
    /// <summary>How heavy — impact and block up, action speed down, stagger.</summary>
    public int Heft { get; init; }

    /// <summary>How keen an edge or point it takes — cutting/piercing damage.</summary>
    public int Bite { get; init; }

    /// <summary>How much punishment it absorbs — armor, block strength.</summary>
    public int Toughness { get; init; }

    /// <summary>Flexibility and spring — hafts, bow staves, grips, cloth.</summary>
    public int Give { get; init; }
}

/// <summary>
/// A material's authored personality for signature generation — weights and tendencies,
/// never recipes (docs/identity-foundation.md §6, D43). Every list references a Signature
/// vocabulary registry and is validated against it. Absent profile = neutral material.
///
/// <para>Deliberately narrower than the full design for now: interaction biases / hidden
/// tendencies / exclusions join with their consumers — a field nothing validates is a
/// silent-typo farm.</para>
/// </summary>
public sealed class SignatureProfile
{
    /// <summary>Hidden scoring metadata only — never player-facing (§6.1).</summary>
    public IReadOnlyList<string> Themes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("favored_triggers")]
    public IReadOnlyList<string> FavoredTriggers { get; init; } = Array.Empty<string>();

    [JsonPropertyName("favored_behaviors")]
    public IReadOnlyList<string> FavoredBehaviors { get; init; } = Array.Empty<string>();

    /// <summary>Favored payload keys — and the §9 breach mechanism: a favored payload is
    /// <b>eligible</b> in generation even when no active identity opens its family, which is
    /// how authored data extends the sensible defaults with no engine special-casing.</summary>
    [JsonPropertyName("favored_payloads")]
    public IReadOnlyList<string> FavoredPayloads { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Data-driven raw-material definition — a stackable <see cref="IItemDefinition"/>.
/// Its properties are the intrinsic starting point that crafting derives from. They are
/// a flat name→value map (matching <see cref="Dungeons.Items.EquipmentDefinition"/>), on
/// a 0–100 scale, and only the properties a material actually has are listed — anything
/// absent reads as 0 (docs/itemization.md §2, docs/json-schema.md §7).
///
/// <para><b>Coexistence note (migration Phase 1, D42):</b> the identity-system fields below
/// (<see cref="Capacity"/>, <see cref="Identities"/>, <see cref="Latent"/>, <see cref="Base"/>,
/// <see cref="SignatureProfile"/>) are the replacement model landing alongside the property
/// model. Nothing consumes them yet; the library adopts them in Phase 4 and the property map
/// is deleted in Phase 7 (docs/identity-foundation.md §15).</para>
/// </summary>
public sealed class MaterialDefinition : IItemDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public Dictionary<string, double> Properties { get; init; } = new();

    /// <summary>Stable identity capacity — how many distinct identities this material holds
    /// before overfill (docs/identity-foundation.md §10.1; range validated 1–4, provisional).
    /// Unset means the material has not been migrated to the identity model yet.</summary>
    public int? Capacity { get; init; }

    /// <summary>Innate <b>active</b> identities, usually empty — most raw materials earn
    /// identities through crafting, not authorship.</summary>
    public IReadOnlyList<IdentityGrant> Identities { get; init; } = Array.Empty<IdentityGrant>();

    /// <summary>Latent identities: present but inactive until revealed (§10.2). Latents do not
    /// occupy capacity; revealing one requires a free slot.</summary>
    public IReadOnlyList<string> Latent { get; init; } = Array.Empty<string>();

    /// <summary>The four visible physical stats (§11.5). Null means all-zero — correct for
    /// herbs, extracts and anything with no structural role.</summary>
    public MaterialBaseStats? Base { get; init; }

    /// <summary>This material's signature personality (§6). Null = neutral.</summary>
    [JsonPropertyName("signature_profile")]
    public SignatureProfile? SignatureProfile { get; init; }

    /// <summary>
    /// The full identity-model state of an <b>emergent</b> material, set by the registration
    /// path and never authored in JSON — the identity-system twin of <see cref="State"/>.
    /// Null for authored materials, whose starting state
    /// <see cref="Dungeons.Crafting.Identity.IdentityStateResolver"/> derives.
    /// </summary>
    [JsonIgnore]
    public Dungeons.Crafting.Identity.IdentityMaterialState? IdentityState { get; init; }

    /// <summary>The supernatural layer (§5.2), keyed by bare essence name (<c>"fire": 60</c>).
    /// Deliberately sparse — mundane is the absence of essence, so ~430 of 480 materials
    /// author nothing here and store no zeros.</summary>
    public Dictionary<string, double> Essence { get; init; } = new();

    /// <summary>
    /// Optional authored override for derived material strength (§6.1). Left unset by the whole
    /// authored library — <see cref="MaterialStateResolver"/> derives it — and exists so a
    /// single material whose derived value reads wrong can be hand-tuned without special
    /// cases in code.
    /// </summary>
    public int? MaterialStrength { get; init; }

    /// <summary>Optional authored override for derived workability (§6.2). See <see cref="MaterialStrength"/>.</summary>
    public int? Workability { get; init; }

    /// <summary>
    /// The explicit profile of an <b>emergent</b> archetype, computed by the reaction engine
    /// and registered under its signature (§12). Null for authored materials, whose profile
    /// is derived by <see cref="MaterialStateResolver"/>. Never authored in JSON — always
    /// resolve through the resolver rather than reading this directly.
    /// </summary>
    [JsonIgnore]
    public MaterialState? State { get; init; }

    public ItemType ItemType => ItemType.Material;
    public bool Stackable => true;

    public PropertySet BaseProperties => new(Properties);

    public bool HasProperty(string property) => BaseProperties.Has(property);

    /// <summary>Returns the value of <paramref name="property"/>, or 0 if the material lacks it.</summary>
    public double GetProperty(string property) => BaseProperties.Get(property);
}
