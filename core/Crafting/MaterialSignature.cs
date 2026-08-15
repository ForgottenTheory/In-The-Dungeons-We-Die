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
/// process. Numbers are formatted invariantly for the same reason.</para>
/// </summary>
public static class MaterialSignature
{
    /// <summary>The canonical archetype id for a finalized result state.</summary>
    public static string Compute(MaterialProfile profile, IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tags);

        return QuantizationTuning.SignaturePrefix + Hash(Canonical(profile, tags));
    }

    /// <summary>
    /// The exact string that gets hashed. Exposed because a signature is otherwise opaque:
    /// when two crafts unexpectedly do or don't collide, this is the only way to see why.
    /// </summary>
    public static string Canonical(MaterialProfile profile, IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tags);

        var builder = new StringBuilder();

        builder.Append("g=").Append(profile.Generation.ToString(CultureInfo.InvariantCulture));
        builder.Append("|pot=").Append(Bucketed(profile.Potency, QuantizationTuning.PotencyBucket));
        builder.Append("|form=").Append(TagValues(tags, TagFamilies.Form.Name));
        builder.Append("|state=").Append(TagValues(tags, TagFamilies.State.Name));
        builder.Append("|roots=").Append(Roots(profile.Lineage));
        builder.Append("|props=").Append(Properties(profile.Properties));

        // Empty in P1. They are written anyway so that adding traits (P2) and essence (P3)
        // only changes the signatures of materials that actually carry them — every archetype
        // already in a player's save keeps its id and its name.
        builder.Append("|traits=");
        builder.Append("|essence=");

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
