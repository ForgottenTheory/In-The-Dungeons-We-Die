using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>
/// A material's identity: quantize the result state, then hash it (docs/emergent-item-system.md
/// §12.1). Same state ⇒ same signature ⇒ same id ⇒ same name, for every player, forever.
///
/// <para>This is what lets emergent materials be <b>stackable runtime definitions</b> rather
/// than per-unit instances (§0 Decision 3). Forty units of the same alloy are one stack, saves
/// stay small, and two players who reach the same state get the same material — so discovery
/// is shareable and worth talking about.</para>
///
/// <para>The hash must be stable across processes and machines, so it is SHA-256 over a
/// canonical string rather than <see cref="object.GetHashCode"/>, which .NET randomizes per
/// crafting action. Numbers are formatted invariantly for the same reason.</para>
/// </summary>
public static class MaterialSignature
{
    /// <summary>The canonical archetype id for a finalized result state.</summary>
    public static string Compute(MaterialState profile, IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tags);

        return QuantizationTuning.SignaturePrefix + Hash(Canonical(profile, tags));
    }

    /// <summary>
    /// The exact string that gets hashed. Exposed because a signature is otherwise opaque:
    /// when two crafts unexpectedly do or don't collide, this is the only way to see why.
    /// </summary>
    public static string Canonical(MaterialState profile, IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tags);

        var builder = new StringBuilder();

        builder.Append("g=").Append(profile.Generation.ToString(CultureInfo.InvariantCulture));
        builder.Append("|pot=").Append(Bucketed(profile.MaterialStrength, QuantizationTuning.MaterialStrengthBucket));
        builder.Append("|form=").Append(TagValues(tags, TagFamilies.Form.Name));
        builder.Append("|state=").Append(TagValues(tags, TagFamilies.State.Name));
        builder.Append("|roots=").Append(Roots(profile.Lineage));
        builder.Append("|props=").Append(Properties(profile.Properties));

        // Reserved in P1 precisely so filling them (traits in C1a, essence in C1b) only
        // changes the signatures of materials that actually carry them — every archetype
        // already in a player's save keeps its id and its name.
        builder.Append("|traits=").Append(Traits(profile.Traits));
        builder.Append("|essence=").Append(Essence(profile.Essence));

        return builder.ToString();
    }

    /// <summary>
    /// Properties bucketed and sorted. Buckets that round to zero are omitted rather than
    /// written as <c>0</c>, so "a trace the floor left behind" and "absent" are the same
    /// material — which is what §8.3's floor pruning is trying to achieve in the first place.
    /// </summary>
    private static string Properties(PropertySet properties)
    {
        var parts = new List<string>();

        foreach (var key in properties.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var bucket = Bucket(properties.Get(key), QuantizationTuning.PropertyBucket);
            if (bucket <= 0.0)
                continue;

            parts.Add(key.ToLowerInvariant() + ":" + Format(bucket));
        }

        return string.Join(",", parts);
    }

    /// <summary>Traits as <c>id:tier</c>, sorted — tier is magnitude bucketed to 5 levels
    /// (§12.1), so "Resilient 62" and "Resilient 64" are the same material while
    /// "Resilient 62" and "Resilient 85" are not.</summary>
    private static string Traits(IReadOnlyList<TraitInstance> traits) =>
        string.Join(",", traits
            .Select(t => new { t.Id, Tier = Math.Clamp((int)Math.Ceiling(t.Magnitude / 20.0), 1, 5) })
            .OrderBy(t => t.Id, StringComparer.Ordinal)
            .Select(t => t.Id + ":" + t.Tier.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Essence bucketed to 5 and sorted (§12.1), zeros omitted — same bargain as
    /// properties, so trace essence the floor pruned and no essence are the same material.</summary>
    private static string Essence(IReadOnlyDictionary<string, double> essence) =>
        string.Join(",", essence
            .Select(pair => new { pair.Key, Bucket = Bucket(pair.Value, QuantizationTuning.PropertyBucket) })
            .Where(pair => pair.Bucket > 0.0)
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key.ToLowerInvariant() + ":" + Format(pair.Bucket)));

    /// <summary>Lineage roots, weight-bucketed to 10% and sorted, per §12.1.</summary>
    private static string Roots(Lineage lineage) =>
        string.Join(",", lineage.Roots
            .Select(r => new
            {
                r.RootId,
                Weight = Bucket(r.Weight, QuantizationTuning.LineageWeightBucket),
            })
            .Where(r => r.Weight > 0.0)
            .OrderBy(r => r.RootId, StringComparer.Ordinal)
            .Select(r => r.RootId + ":" + Format(r.Weight)));

    private static string TagValues(IReadOnlyList<string> tags, string family) =>
        string.Join(",", tags
            .Where(t => TagFamilies.TryParse(t, out var f, out _) && string.Equals(f, family, StringComparison.Ordinal))
            .Select(t => t[(t.IndexOf(':') + 1)..].ToLowerInvariant())
            .OrderBy(v => v, StringComparer.Ordinal));

    private static string Bucketed(double value, double bucket) => Format(Bucket(value, bucket));

    private static double Bucket(double value, double bucket) =>
        Math.Round(value / bucket, MidpointRounding.AwayFromZero) * bucket;

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Hash(string canonical)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes)[..QuantizationTuning.SignatureLength].ToLowerInvariant();
    }
}
