using System.Text.Json;
using System.Text.Json.Serialization;
using Dungeons.Content;

namespace Dungeons.Modifiers;

/// <summary>How a modifier key combines the contributions aimed at it
/// (docs/effect-foundation.md §4.3).</summary>
[JsonConverter(typeof(ModifierKindConverter))]
public enum ModifierKind
{
    /// <summary>Contributions sum. Base + Σ adds. The default.</summary>
    Additive,

    /// <summary>Contributions multiply. Used for rates, intervals and scalars.</summary>
    Multiplicative,

    /// <summary>Any nonzero contribution makes it true. Used for on/off rules.</summary>
    Flag,

    /// <summary>
    /// <c>1 − Π(1 − x)</c>. For avoidance, preservation and doubling — the families where
    /// additive stacking eventually reaches certainty and breaks the game.
    ///
    /// <para>Three sources of 10% give 27.1%, not 30%; forty give 98.5%, not 400%. It feels
    /// additive at low values while being mathematically incapable of reaching 1. The key's
    /// <see cref="ModifierKeyDefinition.Max"/> still applies — the asymptote bounds the limit,
    /// the cap bounds the reachable value, and neither substitutes for the other (D-13).</para>
    /// </summary>
    Diminishing,

    /// <summary>
    /// <c>max(x)</c>. For effects that don't stack with themselves — Barrier from three sources
    /// is the strongest Barrier, not their sum. Authored as <c>highest_only</c>.
    /// </summary>
    HighestOnly,
}

/// <summary>
/// Reads <see cref="ModifierKind"/> from JSON, tolerating the snake_case the docs and content
/// author (<c>highest_only</c>) as well as the enum's own spelling.
///
/// <para>Registered on the property rather than the enum, because a converter in
/// <c>JsonSerializerOptions.Converters</c> — which is where <see cref="DataStore{T}"/> puts the
/// default enum converter — outranks a <c>[JsonConverter]</c> attribute on a type.</para>
/// </summary>
public sealed class ModifierKindConverter : JsonConverter<ModifierKind>
{
    public override ModifierKind Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var text = reader.GetString();

        if (string.IsNullOrWhiteSpace(text))
            throw new JsonException("Modifier key 'kind' is empty.");

        if (Enum.TryParse<ModifierKind>(text.Replace("_", string.Empty).Replace("-", string.Empty), ignoreCase: true, out var kind))
            return kind;

        throw new JsonException(
            $"'{text}' is not a modifier kind. Valid: {string.Join(", ", Enum.GetNames<ModifierKind>())}.");
    }

    public override void Write(Utf8JsonWriter writer, ModifierKind value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            ModifierKind.HighestOnly => "highest_only",
            _ => value.ToString().ToLowerInvariant(),
        });
}

/// <summary>
/// A thing a modifier can target, defined as data.
///
/// <para>This replaces the closed <c>StatId</c> enum as the modifier <i>vocabulary</i>. The
/// reason is that a progression game is a modifier-stacking game: action intervals, resource
/// preservation, yield, damage by type, block effectiveness, extraction bonuses and profession
/// efficiency are all modifier targets, and none of them are one of ten stats. Making the
/// vocabulary data means a new mechanic is a JSON entry, not an enum change that ripples
/// through every switch statement.</para>
///
/// <para>Keys are namespaced by system — <c>attr.strength</c>, <c>combat.interval.mult</c>,
/// <c>profession.yield.mining</c> — and validated, so a typo still fails loudly (the same
/// bargain <see cref="PropertyDefinition"/> struck for material properties, DECISIONS D17).</para>
/// </summary>
public sealed class ModifierKeyDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    [JsonConverter(typeof(ModifierKindConverter))]
    public ModifierKind Kind { get; init; } = ModifierKind.Additive;

    /// <summary>Value when nothing contributes. 0 for additive, 1 for multiplicative.</summary>
    public double Default { get; init; }

    /// <summary>Lower clamp applied after resolution. Null leaves it unbounded.</summary>
    public double? Min { get; init; }

    /// <summary>Upper clamp applied after resolution. Null leaves it unbounded.</summary>
    public double? Max { get; init; }

    /// <summary>
    /// Grouping for UI and for "show me everything affecting my professions" queries.
    /// Conventionally the first segment of the id.
    /// </summary>
    public string Family { get; init; } = string.Empty;

    /// <summary>Human-readable note for tooltips and the Character Lab.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// True when a lower resolved value is better (action intervals, resource costs). The UI
    /// needs this to colour a change correctly; nothing mechanical reads it.
    /// </summary>
    [JsonPropertyName("lower_is_better")]
    public bool LowerIsBetter { get; init; }

    /// <summary>
    /// The one <see cref="ScopeDimensions"/> dimension a contribution to this key may be scoped
    /// by — <c>profession</c> for <c>profession.interval.mult</c>, <c>move_tag</c> for
    /// <c>combat.damage.flat</c>. Empty means the key is global and refuses any scope.
    ///
    /// <para>This is what makes D-12's requirement declarative rather than remembered:
    /// resolving a scoped key without its dimension in the context <b>throws</b>, so a context
    /// bug is loud at the call site instead of quietly producing the unscoped subtotal.</para>
    ///
    /// <para>It does not mean every contribution must carry a scope. An unscoped contribution to
    /// a scoped key is the global case — the ring that gives <c>+20% damage</c> everywhere,
    /// against the weapon that gives it only to itself.</para>
    /// </summary>
    [JsonPropertyName("scoped_by")]
    public string ScopedBy { get; init; } = string.Empty;

    /// <summary>
    /// Marks a family that reaches an unrecoverable state if it ever hits its ceiling —
    /// avoidance, preservation, doubling. The validator refuses to load one without a
    /// <see cref="Max"/> (docs/effect-foundation.md §9), so "unbounded dodge chance" cannot be
    /// authored by leaving a field out.
    /// </summary>
    public bool Danger { get; init; }

    /// <summary>The value this key resolves to when nothing has contributed.</summary>
    public double Baseline => Kind == ModifierKind.Multiplicative && Default == 0.0 ? 1.0 : Default;

    /// <summary>True when this key names a dimension its context must supply.</summary>
    [JsonIgnore]
    public bool IsScoped => !string.IsNullOrWhiteSpace(ScopedBy);
}
