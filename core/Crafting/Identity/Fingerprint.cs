using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dungeons.Content;

namespace Dungeons.Crafting.Identity;

/// <summary>
/// The stacking hash of the identity model (docs/identity-foundation.md §11.3) — the
/// successor to the property-based <see cref="MaterialSignature"/>, and the reason the old
/// word "signature" now means only the generated effect layer (D43).
///
/// <para>Covers: sorted identities+ranks · capacity · condition · quality bucket · the
/// carrier flag · quantized root composition · <c>form:</c>/<c>state:</c> tag values.
/// Deliberately excludes: full crafting history (state is canonical — two paths to the same
/// state stack together), stability (derived from count vs capacity, both already in), and
/// profile/base content (both derive from roots, so roots cover them).</para>
/// </summary>
public static class Fingerprint
{
    /// <summary>Same prefix scheme as the old system — the id is the identity. The two
    /// engines never share a live registry: the old one dies before this one is wired in.</summary>
    public const string IdPrefix = "emergent.";
    public const int HashHexLength = 8;

    public static string Compute(IdentityMaterialState state, IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(tags);

        var canonical = Canonical(state, tags);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return IdPrefix + Convert.ToHexString(hash)[..HashHexLength].ToLowerInvariant();
    }

    /// <summary>The canonical string the hash is computed over — exposed for tests so a
    /// change to what is fingerprinted is a visible diff, never an accident.</summary>
    public static string Canonical(IdentityMaterialState state, IReadOnlyList<string> tags)
    {
        var builder = new StringBuilder();

        builder.Append("ids=");
        foreach (var stake in state.Identities.OrderBy(s => s.Id, StringComparer.Ordinal))
            builder.Append(stake.Id).Append(':').Append(stake.Rank).Append(',');

        builder.Append("|latent=");
        foreach (var latent in state.Latent.OrderBy(id => id, StringComparer.Ordinal))
            builder.Append(latent).Append(',');

        builder.Append("|cap=").Append(state.Capacity);
        builder.Append("|cond=").Append((int)state.Condition);
        builder.Append("|q=").Append(state.Quality / IdentityCraftTuning.QualityFingerprintBucket);
        builder.Append("|carrier=").Append(state.IsCarrier ? 1 : 0);

        builder.Append("|roots=");
        foreach (var root in state.Roots.OrderBy(r => r.DefinitionId, StringComparer.Ordinal))
        {
            var bucketed = Math.Round(root.Weight / IdentityCraftTuning.RootWeightBucket)
                * IdentityCraftTuning.RootWeightBucket;
            builder.Append(root.DefinitionId).Append(':')
                .Append(bucketed.ToString("0.0#", CultureInfo.InvariantCulture)).Append(',');
        }

        builder.Append("|tags=");
        foreach (var tag in tags
            .Where(t => t.StartsWith("form:", StringComparison.Ordinal)
                     || t.StartsWith("state:", StringComparison.Ordinal))
            .OrderBy(t => t, StringComparer.Ordinal))
        {
            builder.Append(tag).Append(',');
        }

        return builder.ToString();
    }
}
