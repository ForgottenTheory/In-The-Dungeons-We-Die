using Dungeons.Content;

namespace Dungeons.Presentation;

/// <summary>
/// Read-only display-metadata lookup over the property registry. Glyphs and glosses are data
/// (D30 invariant 5 — no property-name switches in code); this class only reads them and
/// supplies safe fallbacks so an unauthored glyph degrades to "·", never to a crash.
/// </summary>
public sealed class PropertyGlossary
{
    private readonly DataStore<PropertyDefinition> _properties;

    public PropertyGlossary(DataStore<PropertyDefinition> properties)
    {
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
    }

    public string Name(string propertyId) =>
        _properties.TryGetById(propertyId, out var def) && !string.IsNullOrWhiteSpace(def.Name)
            ? def.Name
            : propertyId;

    public string Glyph(string propertyId) =>
        _properties.TryGetById(propertyId, out var def) && !string.IsNullOrWhiteSpace(def.Glyph)
            ? def.Glyph
            : "·";

    public string Gloss(string propertyId) =>
        _properties.TryGetById(propertyId, out var def) ? def.Gloss : string.Empty;

    /// <summary>The annihilation partner, if the registry declares one (heat ↔ cold).</summary>
    public string? Opposes(string propertyId) =>
        _properties.TryGetById(propertyId, out var def) && !string.IsNullOrWhiteSpace(def.Opposes)
            ? def.Opposes
            : null;

    public PropertyRole? RoleOf(string propertyId) =>
        _properties.TryGetById(propertyId, out var def) ? def.Role : null;

    /// <summary>"◆ Hardness" — the standard glyph-plus-name pair every surface uses.</summary>
    public string Label(string propertyId) => $"{Glyph(propertyId)} {Name(propertyId)}";
}
