using System.Text.Json.Serialization;

namespace Dungeons.Content;

/// <summary>
/// How a property behaves in the (future) reaction engine. Assigned as data, per
/// docs/emergent-item-system.md §2.3. Only <see cref="Reactive"/> properties transfer
/// along a crafting action channel; <see cref="Response"/> are derived resistances (never a
/// reaction input); <see cref="Sourcing"/> is inert in crafting (harvest only).
/// </summary>
public enum PropertyRole
{
    Structural,
    Reactive,
    Response,
    Sourcing,
}

/// <summary>One contributor to a reactive property's derived resistance (§2.2).</summary>
public sealed class ResistContributor
{
    public string Property { get; init; } = string.Empty;
    public double Weight { get; init; }
}

/// <summary>
/// A tag this property confers once it passes <see cref="Min"/> — the "state threshold" source
/// of tag derivation (docs/emergent-item-system.md §4.2), e.g. <c>toxicity ≥ 55 →
/// class:venomous</c>. Declared on the property rather than in a separate rules file so a new
/// property brings its own classification with it, and tags stay data.
/// </summary>
public sealed class TagGrant
{
    public string Tag { get; init; } = string.Empty;
    public double Min { get; init; }
}

/// <summary>
/// First-class, data-driven definition of a material property (game/data/properties/).
/// Promotes the string-keyed properties in <see cref="Dungeons.Items.ItemProperties"/> to
/// loaded data so the reaction engine can reason about roles/opposition/resistance without
/// code changes (docs/emergent-item-system.md §2.3). P0 loads and validates these; the
/// algebra that consumes them is P1+.
/// </summary>
public sealed class PropertyDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public PropertyRole Role { get; init; }

    /// <summary>Grouping for opposition/annihilation and UI (e.g. "thermal", "electrical").</summary>
    public string Family { get; init; } = string.Empty;

    /// <summary>Mutual-annihilation partner, if any (heat↔cold, growth↔decay).</summary>
    public string? Opposes { get; init; }

    /// <summary>
    /// Contributors used to derive this reactive property's resistance. If one of them is a
    /// <see cref="PropertyRole.Response"/> property authored on the material, that authored
    /// value overrides the derived sum (docs/emergent-item-system.md §2.2).
    /// </summary>
    [JsonPropertyName("resisted_by")]
    public IReadOnlyList<ResistContributor> ResistedBy { get; init; } = Array.Empty<ResistContributor>();

    /// <summary>Off-channel behaviour: reactive properties drift toward 0 rather than blend.</summary>
    public bool Dilutes { get; init; }

    /// <summary>Whether a reagent can move this property at all.</summary>
    public bool Transferable { get; init; }

    /// <summary>Below this value the property is pruned to 0 after a transformation.</summary>
    public int Floor { get; init; } = 5;

    /// <summary>Tags this property confers on a result once it passes a threshold (§4.2).</summary>
    [JsonPropertyName("grants_tags")]
    public IReadOnlyList<TagGrant> GrantsTags { get; init; } = Array.Empty<TagGrant>();

    /// <summary>Display glyph for the player crafting language (D30;
    /// docs/presentation-architecture.md §2A). Placeholder Unicode until art exists — data,
    /// never a code switch, so swapping a tofu glyph is a JSON edit.</summary>
    public string Glyph { get; init; } = string.Empty;

    /// <summary>One-line player-facing meaning ("Carries and channels energy."), the §2E
    /// context voice. Never contains numbers.</summary>
    public string Gloss { get; init; } = string.Empty;
}
