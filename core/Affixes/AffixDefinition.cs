using System.Text.Json.Serialization;
using Dungeons.Content;
using Dungeons.Rules;

namespace Dungeons.Affixes;

/// <summary>One property-material influence requirement: the item potential must carry at least this much.</summary>
public sealed class MaterialInfluenceRequirement
{
    public string Property { get; init; } = string.Empty;
    public double Min { get; init; }
}

/// <summary>§3.2 availability — the hard gate. All conditions must pass.</summary>
public sealed class ModifierAvailability
{
    /// <summary>Any-of match against the item potential's form id or its tags. Empty = any form.</summary>
    [JsonPropertyName("forms_any")]
    public IReadOnlyList<string> FormsAny { get; init; } = Array.Empty<string>();

    public IReadOnlyList<MaterialInfluenceRequirement> Requires { get; init; } = Array.Empty<MaterialInfluenceRequirement>();

    [JsonPropertyName("requires_any_essence")]
    public IReadOnlyList<string> RequiresAnyEssence { get; init; } = Array.Empty<string>();

    [JsonPropertyName("excludes_family")]
    public IReadOnlyList<string> ExcludesFamily { get; init; } = Array.Empty<string>();
}

/// <summary>One term of the weight formula: <c>base + Σ material influence/10 × per10</c>.</summary>
public sealed class ChanceWeightScale
{
    public string? Property { get; init; }
    public string? Essence { get; init; }
    [JsonPropertyName("per_ten_influence")]
    public double PerTenInfluence { get; init; }
}

public sealed class ModifierChanceWeight
{
    public double Base { get; init; } = 10;
    public IReadOnlyList<ChanceWeightScale> Scale { get; init; } = Array.Empty<ChanceWeightScale>();
}

/// <summary>One tier: its item potential requirements and its value range. T1 is best (§3.2).</summary>
public sealed class AffixTier
{
    public int Tier { get; init; }

    /// <summary>Property name → min material influence, or <c>essence.X</c> → min essence.</summary>
    public Dictionary<string, double> Requires { get; init; } = new();

    /// <summary>[lo, hi] in mechanical units (fractions for chances/mults, flats for flats).</summary>
    public IReadOnlyList<double> Range { get; init; } = Array.Empty<double>();
}

/// <summary>
/// One thing an affix grants. <c>stat</c> becomes a scoped <c>ModifierContribution</c>;
/// <c>rule</c> becomes a <see cref="TriggerRule"/> attached while the item is worn — the same
/// Grant atom statuses and class components use, nothing bespoke.
/// </summary>
public sealed class AffixGrant
{
    /// <summary>"stat" or "rule".</summary>
    public string Type { get; init; } = "stat";

    /// <summary>stat: the modifier key.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>stat: a number, or "$roll" to substitute the rolled value.</summary>
    public string Value { get; init; } = "$roll";

    /// <summary>stat: optional "dimension:value" scope (e.g. "lane:heat", "move_tag:mech:heavy").</summary>
    public string? Scope { get; init; }

    /// <summary>rule: the trigger rule to attach while worn.</summary>
    public TriggerRule? Rule { get; init; }

    /// <summary>rule: where the rolled value lands — "chance", "amount", or "" (no substitution).</summary>
    [JsonPropertyName("roll_into")]
    public string RollInto { get; init; } = string.Empty;
}

/// <summary>
/// An item modifier (docs/affixes.md §3.2). In code and data these are <c>affix.*</c>; in
/// player-facing text they are always "modifiers" — the bare word Prefix belongs to the
/// character layer (D-17).
/// </summary>
public sealed class AffixDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>"prefix" | "suffix" | "innate". Innates are computed, never rolled (U-7).</summary>
    public string Slot { get; init; } = "prefix";

    /// <summary>The anti-stacking unit: one affix per family per item (§3.5).</summary>
    public string Family { get; init; } = string.Empty;

    /// <summary>"standard" | "trigger" | "innate". Exotic/signature/anomalous parse but are
    /// excluded from R4 pools (deferred to E7/P4 by decision).</summary>
    public string Class { get; init; } = "standard";

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("availability")]
    public ModifierAvailability Availability { get; init; } = new();
    [JsonPropertyName("chance_weight")]
    public ModifierChanceWeight ChanceWeight { get; init; } = new();
    public IReadOnlyList<AffixTier> Tiers { get; init; } = Array.Empty<AffixTier>();
    public IReadOnlyList<AffixGrant> Grants { get; init; } = Array.Empty<AffixGrant>();

    public string? Drawback { get; init; }

    /// <summary>Player text with <c>$roll</c> ("$roll%" renders the value ×100).</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>A rolled (or computed, for innates) affix on a specific item — what persists.</summary>
public sealed record RolledAffix(string AffixId, int Tier, double Roll);
