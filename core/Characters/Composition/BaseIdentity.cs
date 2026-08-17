using System.Text.Json.Serialization;
using Dungeons.Rules;

namespace Dungeons.Characters.Composition;

/// <summary>
/// Which combat event a build expresses Suffix modifiers through (docs/classes.md — the three
/// expression channels).
///
/// <para>Channels are keyed to <b>events every build produces</b> rather than to attribute
/// archetypes, so no Suffix is ever unusable by a given Base. A caster still blocks sometimes;
/// a Bastion still hits sometimes; everyone runs a resource. The Base declares a default and
/// prefixes or equipment may shift it.</para>
/// </summary>
public enum ExpressionChannel
{
    /// <summary>You landed a discrete damaging hit.</summary>
    Strike,

    /// <summary>You avoided, absorbed, mitigated, or protected.</summary>
    Guard,

    /// <summary>You spent, accumulated, or sustained a resource.</summary>
    Surge,
}

/// <summary>
/// The broad shape of a gauge, kept as a <i>design taxonomy</i> rather than a hard rule.
/// It exists so balance can reason about five archetypes instead of fifteen bespoke meters,
/// and so the Character Lab can say what kind of engine a build is running.
/// </summary>
public enum GaugeBehaviour
{
    /// <summary>Accumulate, then dump.</summary>
    BuildSpend,

    /// <summary>Commit, release on timing.</summary>
    ChargeHold,

    /// <summary>Maintain; grows while held, breaks when dropped.</summary>
    SustainRamp,

    /// <summary>Starts full, drains under material influence, refunded by skill.</summary>
    DepleteRecover,

    /// <summary>Borrow now, pay later.</summary>
    DebtCollect,
}

/// <summary>A threshold band: while the gauge sits in it, contribute a modifier.</summary>
public sealed class GaugeBand
{
    [JsonPropertyName("at_least")]
    public double AtLeast { get; init; }

    [JsonPropertyName("at_most")]
    public double? AtMost { get; init; }

    /// <summary>A registered modifier key.</summary>
    public string Modifier { get; init; } = string.Empty;

    public double Value { get; init; }

    public bool Contains(double fraction) =>
        fraction >= AtLeast && (AtMost is null || fraction <= AtMost);
}

/// <summary>
/// A Base's signature meter. <b>Optional</b> — several Bases deliberately have none, because
/// "everyone gets a bar" would flatten exactly the distinctions the roster exists to create.
/// Fighter runs on equipment, Vitalist spends Health itself, Operative's resource lives on the
/// enemy, Necromancer's is a pile of bodies.
///
/// <para>Generation is expressed as ordinary <see cref="TriggerRule"/>s, so a gauge feeds off
/// the same event bus everything else does and needs no bespoke plumbing.</para>
/// </summary>
public sealed class GaugeDefinition
{
    public string Name { get; init; } = string.Empty;
    public GaugeBehaviour Behaviour { get; init; } = GaugeBehaviour.BuildSpend;
    public double Max { get; init; } = 100.0;

    [JsonPropertyName("starts_full")]
    public bool StartsFull { get; init; }

    /// <summary>Passive bleed per tick once the grace period lapses.</summary>
    [JsonPropertyName("decay_per_tick")]
    public double DecayPerTick { get; init; }

    /// <summary>Ticks of inactivity tolerated before decay begins.</summary>
    [JsonPropertyName("decay_grace_ticks")]
    public int DecayGraceTicks { get; init; }

    /// <summary>Passive refill per tick. Mutually sensible with, not opposed to, decay.</summary>
    [JsonPropertyName("regen_per_tick")]
    public double RegenPerTick { get; init; }

    /// <summary>What fills it, as event hooks.</summary>
    public IReadOnlyList<TriggerRule> Feeds { get; init; } = Array.Empty<TriggerRule>();

    /// <summary>What its level does, as threshold bands over the 0–1 fill fraction.</summary>
    public IReadOnlyList<GaugeBand> Bands { get; init; } = Array.Empty<GaugeBand>();
}

/// <summary>
/// How attribute growth works (the "2K budget" rule).
///
/// <para><b>Every Base distributes the same total per level.</b> Only the shape differs. That
/// single constraint is what makes Base choice a real trade instead of a menu where some
/// options are strictly larger — three attributes pushed hard means four left behind, and no
/// Base can simply be "more".</para>
///
/// <para>Authored weights name only the notable attributes; whatever budget is left over is
/// spread evenly across the rest, so a Base's JSON shows its identity and nothing else.</para>
/// </summary>
public static class AttributeGrowth
{
    /// <summary>Total attribute points every Base gains per level. The whole rule, one number.</summary>
    public const double BudgetPerLevel = 4.0;

    /// <summary>
    /// Growth per level for every attribute, with the unlisted remainder spread evenly.
    /// </summary>
    public static IReadOnlyDictionary<AttributeType, double> PerLevel(
        IReadOnlyDictionary<string, double> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var result = new Dictionary<AttributeType, double>();
        var listed = 0.0;

        foreach (var attribute in Enum.GetValues<AttributeType>())
        {
            if (weights.TryGetValue(attribute.ToString(), out var weight))
            {
                result[attribute] = weight;
                listed += weight;
            }
        }

        var unlisted = Enum.GetValues<AttributeType>().Where(a => !result.ContainsKey(a)).ToList();
        var trickle = unlisted.Count == 0 ? 0.0 : Math.Max(0.0, BudgetPerLevel - listed) / unlisted.Count;

        foreach (var attribute in unlisted)
            result[attribute] = trickle;

        return result;
    }

    /// <summary>Cumulative growth at <paramref name="level"/>, rounded down per attribute so
    /// fractional weights accumulate smoothly and deterministically.</summary>
    public static IReadOnlyDictionary<AttributeType, int> AtLevel(
        IReadOnlyDictionary<string, double> weights, int level)
    {
        var perLevel = PerLevel(weights);
        var levels = Math.Max(0, level - 1);

        return perLevel.ToDictionary(pair => pair.Key, pair => (int)Math.Floor(pair.Value * levels));
    }
}
