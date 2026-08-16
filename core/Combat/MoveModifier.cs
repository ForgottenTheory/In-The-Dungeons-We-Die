using System.Text.Json.Serialization;
using Dungeons.Content;
using Dungeons.Rules;

namespace Dungeons.Combat;

/// <summary>
/// Which moves a modifier touches: a specific id, and/or tag predicates. This is how
/// "<b>Heavy Strike</b> gains additional Heat damage" is authored without a code branch —
/// and how <c>if item == ThunderSword</c> never gets written.
/// </summary>
public sealed class MoveMatch
{
    /// <summary>Match one specific move. Null matches by tags alone.</summary>
    [JsonPropertyName("move_id")]
    public string? MoveId { get; init; }

    /// <summary>Every one must be present.</summary>
    [JsonPropertyName("tags_all")]
    public IReadOnlyList<string> TagsAll { get; init; } = Array.Empty<string>();

    /// <summary>At least one must be present (when non-empty).</summary>
    [JsonPropertyName("tags_any")]
    public IReadOnlyList<string> TagsAny { get; init; } = Array.Empty<string>();

    public bool Matches(MoveDefinition move)
    {
        ArgumentNullException.ThrowIfNull(move);

        if (MoveId is not null && !string.Equals(MoveId, move.Id, StringComparison.Ordinal))
            return false;

        if (TagsAll.Any(tag => !move.HasTag(tag)))
            return false;

        if (TagsAny.Count > 0 && !TagsAny.Any(move.HasTag))
            return false;

        return true;
    }
}

/// <summary>The closed op vocabulary — 11 ops cover the brief's entire list (docs/moves.md §3.2).</summary>
public static class MoveOps
{
    public const string AddPacket = "addPacket";
    public const string ScaleDamage = "scaleDamage";
    public const string Convert = "convert";
    public const string AddAsExtra = "addAsExtra";
    public const string ScaleTiming = "scaleTiming";
    public const string ScaleCost = "scaleCost";
    public const string AddTargets = "addTargets";
    public const string AddChain = "addChain";
    public const string AddEffect = "addEffect";
    public const string AddTag = "addTag";
    public const string SetFlag = "setFlag";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AddPacket, ScaleDamage, Convert, AddAsExtra, ScaleTiming, ScaleCost,
        AddTargets, AddChain, AddEffect, AddTag, SetFlag,
    };

    /// <summary>
    /// The fixed application order, regardless of source order — otherwise the same three
    /// affixes on different items would produce different results. `convert` runs <b>after</b>
    /// `scaleDamage`, matching the pipeline rule that increases apply to the lane the damage
    /// started in (docs/damage-and-defense.md §3.2).
    /// </summary>
    public static readonly IReadOnlyList<string> ApplicationOrder = new[]
    {
        AddPacket, ScaleDamage, Convert, AddAsExtra, AddTargets, AddChain,
        ScaleTiming, ScaleCost, AddEffect, AddTag, SetFlag,
    };

    /// <summary>Flags `setFlag` may set. `uninterruptible` is the live one; the other two are
    /// registered so the vocabulary is closed now rather than grown ad hoc.</summary>
    public static readonly IReadOnlySet<string> Flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "uninterruptible", "unblockable", "unavoidable",
    };

    /// <summary>Timing fields `scaleTiming` may touch.</summary>
    public static readonly IReadOnlySet<string> TimingFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "telegraph", "windup", "recovery",
    };
}

/// <summary>
/// One operation on a matched move. One uniform shape for all 11 ops — the same bargain
/// <see cref="ConditionSpec"/> struck: a couple of unused fields per entry, in exchange for one
/// schema and a simple validator.
/// </summary>
public sealed class MoveOpSpec
{
    public string Op { get; init; } = string.Empty;

    /// <summary>`addPacket`: the packet to append.</summary>
    public Packet? Packet { get; init; }

    /// <summary>`scaleDamage`: optional lane filter. `convert`/`addAsExtra`: source lane.</summary>
    public string? From { get; init; }

    /// <summary>`convert`/`addAsExtra`: destination lane (an aspect name, or `physical`).</summary>
    public string? To { get; init; }

    /// <summary>`convert`/`addAsExtra`: the fraction moved or duplicated. Always explicit —
    /// there is deliberately no bare "add aspect" op (D-01).</summary>
    public double Fraction { get; init; }

    /// <summary>`scaleTiming`: which field. `setFlag`: which flag.</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>`scaleDamage`/`scaleTiming`/`scaleCost`: the multiplier. `addTargets`/`addChain`: the count.</summary>
    public double Value { get; init; } = 1.0;

    /// <summary>`scaleCost`: which resource (empty scales all).</summary>
    public string Resource { get; init; } = string.Empty;

    /// <summary>`addEffect`: the rider to append (its own <see cref="EffectSpec.Chance"/> rides along).</summary>
    public EffectSpec? Effect { get; init; }

    /// <summary>`addTag`: the tag to append — the composition lever. An affix that adds
    /// `mech:chain` to your melee moves lets a different affix that matches `mech:chain` fire.</summary>
    public string Tag { get; init; } = string.Empty;
}

/// <summary>
/// A move modifier: a match plus a list of ops (docs/moves.md §3.1). Granted exactly like a
/// rule — by an affix, a class component, a status, or a form.
/// </summary>
public sealed class MoveModifierDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public MoveMatch Match { get; init; } = new();

    public IReadOnlyList<MoveOpSpec> Ops { get; init; } = Array.Empty<MoveOpSpec>();
}
