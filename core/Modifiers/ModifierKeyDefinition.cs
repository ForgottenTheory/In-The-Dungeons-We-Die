using System.Text.Json.Serialization;
using Dungeons.Content;

namespace Dungeons.Modifiers;

/// <summary>How a modifier key combines the contributions aimed at it.</summary>
public enum ModifierKind
{
    /// <summary>Contributions sum. Base + Σ adds. The default.</summary>
    Additive,

    /// <summary>Contributions multiply. Used for rates, intervals and scalars.</summary>
    Multiplicative,

    /// <summary>Any nonzero contribution makes it true. Used for on/off rules.</summary>
    Flag,
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

    /// <summary>The value this key resolves to when nothing has contributed.</summary>
    public double Baseline => Kind == ModifierKind.Multiplicative && Default == 0.0 ? 1.0 : Default;
}
