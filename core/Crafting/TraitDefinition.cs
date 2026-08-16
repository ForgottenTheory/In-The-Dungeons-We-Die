using System.Text.Json.Serialization;
using Dungeons.Content;

namespace Dungeons.Crafting;

/// <summary>A closed property-value window. Absent bounds don't constrain.</summary>
public sealed class PropertyRange
{
    public double? Min { get; init; }
    public double? Max { get; init; }

    public bool Contains(double value) =>
        (Min is not { } min || value >= min) && (Max is not { } max || value <= max);
}

/// <summary>An authored supersession: this trait + <see cref="With"/> merge into
/// <see cref="Into"/>, freeing a slot — the reason to go deep (§10.4).</summary>
public sealed class TraitMerge
{
    public string With { get; init; } = string.Empty;
    public string Into { get; init; } = string.Empty;
}

/// <summary>
/// A state trait (docs/emergent-item-system.md §10): a named, discrete, capped qualitative
/// state a material entered. Authored as a rule, <b>never authored onto items</b> — which
/// material has which trait is always emergent. Anyone reaching the state region gets it,
/// every time; these are the traits players learn to aim at.
/// </summary>
public sealed class TraitDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>The stated cost of carrying it (§10.3: every trait carries a drawback or an
    /// opportunity cost — often the <see cref="Consumes"/> bill, sometimes worse).</summary>
    public string Drawback { get; init; } = string.Empty;

    /// <summary>§16.3 expression category — the axis form apertures gate
    /// (<see cref="FabricationTuning.TraitCategories"/>). Validated.</summary>
    public string Category { get; init; } = "structural";

    /// <summary>
    /// Property thresholds, all of which must hold for the trait to be born from state.
    /// <b>Empty means merge-born only</b> — the trait can never be reached from state-space
    /// and exists solely as a supersession target (validated).
    /// </summary>
    public Dictionary<string, PropertyRange> Condition { get; init; } = new();

    /// <summary>Magnitude at birth = min of these properties (§10.2's exemplar shape), read
    /// before <see cref="Consumes"/> is charged. Clamped 0–100.</summary>
    [JsonPropertyName("magnitude_of")]
    public IReadOnlyList<string> MagnitudeOf { get; init; } = Array.Empty<string>();

    /// <summary>Properties eaten at birth. Not refunded on displacement (§10.4) — that is
    /// what makes late-chain crafting a "which three?" decision instead of accumulation.</summary>
    public Dictionary<string, double> Consumes { get; init; } = new();

    /// <summary>Supersessions this trait participates in. Authored on either partner;
    /// the resolver checks both directions.</summary>
    public IReadOnlyList<TraitMerge> Merges { get; init; } = Array.Empty<TraitMerge>();

    /// <summary>Whether the state condition alone can birth this trait.</summary>
    public bool IsStateBorn => Condition.Count > 0;
}

/// <summary>A trait a specific material actually carries: the definition id + the magnitude
/// it was born (or merged) at.</summary>
public sealed record TraitInstance(string Id, double Magnitude);
