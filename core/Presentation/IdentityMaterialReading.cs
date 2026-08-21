using System.Text;
using Dungeons.Content;
using Dungeons.Crafting.Identity;

namespace Dungeons.Presentation;

/// <summary>
/// A material's identity-model state as the bench inspector reads it (migration Phase 6):
/// stakes and slots, what sleeps in it, how it is wearing, and what its overfill means —
/// every §11.2 facet in a sentence, no simulation numbers. The Assay panel reads the same
/// facts through its redaction (<see cref="AssayLens"/> reuse arrives with the re-aim), so
/// the two surfaces can never disagree about what a material is.
/// </summary>
public static class IdentityMaterialReadings
{
    /// <summary>The full inspector card body. Slot counts are gameplay counts (capacity is a
    /// count the design surfaces); quality and everything else speak in words.</summary>
    public static string Summary(IdentityMaterialState state, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(content);

        var builder = new StringBuilder();
        builder.Append(StakesLine(state, content));

        if (state.Latent.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Latent: ")
                .Append(string.Join(", ", state.Latent.Select(id => IdentityNameOf(id, content))))
                .Append(" — Reveal can wake it");
        }

        if (state.IsCarrier)
        {
            builder.AppendLine();
            builder.Append("A prepared carrier — delivers its full depth on Transfer");
        }

        builder.AppendLine();
        builder.Append(state.Condition).Append(" — ").Append(IdentityPhrases.ConditionMeaning(state.Condition))
            .Append(" · ").Append(IdentityPhrases.QualityWord(state.Quality)).Append(" workmanship");

        if (state.Stability != Stability.Stable)
        {
            builder.AppendLine();
            builder.Append(state.Stability).Append(" — ").Append(IdentityPhrases.OverfillMeaning(state.Stability));
        }

        return builder.ToString();
    }

    /// <summary>"Dense · Vital (improved) — 2 of 3 identity slots taken", with the overfilled
    /// count said straight when the ladder has been climbed.</summary>
    public static string StakesLine(IdentityMaterialState state, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(content);

        if (state.Identities.Count == 0)
        {
            return state.Capacity == 1
                ? "Carries nothing — one identity slot, open"
                : $"Carries nothing — {state.Capacity} identity slots, open";
        }

        var stakes = string.Join(" · ", state.Identities.Select(stake => IdentityPhrases.Stake(stake, content)));
        var slots = state.Identities.Count <= state.Capacity
            ? $"{state.Identities.Count} of {state.Capacity} identity slot{(state.Capacity == 1 ? "" : "s")} taken"
            : $"{state.Identities.Count} identities on {state.Capacity} slot{(state.Capacity == 1 ? "" : "s")}";
        return $"{stakes} — {slots}";
    }

    /// <summary>The Assay-ungated half of a reading: what the material openly carries, with
    /// its overfill word — active identities are legible by design (D42), and chosen risk
    /// must be visible wherever the material is offered.</summary>
    public static string StakeNames(IdentityMaterialState state, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(content);

        var stakes = state.Identities.Count == 0
            ? "carries nothing openly"
            : string.Join(" · ", state.Identities.Select(stake => IdentityPhrases.Stake(stake, content)));
        return stakes + IdentityPhrases.StabilityMarker(state);
    }

    /// <summary>The Vessel facet: slots, wear, workmanship — how much material there is to
    /// work with, in the §10 ladders' own words.</summary>
    public static string VesselPhrase(IdentityMaterialState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var taken = state.Identities.Count;
        var slots = taken <= state.Capacity
            ? $"{taken} of {state.Capacity} identity slot{(state.Capacity == 1 ? "" : "s")} taken"
            : $"{taken} identities on {state.Capacity} slot{(state.Capacity == 1 ? "" : "s")}";
        return $"{slots} · {state.Condition} — {IdentityPhrases.ConditionMeaning(state.Condition)}"
            + $" · {IdentityPhrases.QualityWord(state.Quality)} workmanship";
    }

    /// <summary>The D53 profile reading: the strongest few leanings in vocabulary words,
    /// never a listing — the personality the player senses, not a stat block. Exact weights
    /// stay in Advanced/labs.</summary>
    public static string LeaningsPhrase(MergedSignatureProfile profile, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(content);

        var workLeans = StrongestLeans(profile.FavoredTriggers, id => TriggerNameOf(id, content))
            .Concat(StrongestLeans(profile.FavoredBehaviors, id => BehaviorNameOf(id, content)))
            .ToList();
        var payloadLeans = StrongestLeans(profile.FavoredPayloads, id => PayloadNameOf(id, content)).ToList();

        if (workLeans.Count == 0 && payloadLeans.Count == 0)
            return "no particular leanings";

        var parts = new List<string>();
        if (workLeans.Count > 0)
            parts.Add($"leans toward {string.Join(" · ", workLeans)} work");
        if (payloadLeans.Count > 0)
            parts.Add($"favors {string.Join(", ", payloadLeans)}");
        return string.Join("; ", parts);
    }

    /// <summary>The Potential facet: the floor each active identity would guarantee on gear —
    /// quoting the same rule generation mints from (<see cref="ItemEffectResolver.FloorPayloadOf"/>),
    /// so the promise and the mint can never disagree.</summary>
    public static string PotentialPhrase(IdentityMaterialState state, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(content);

        var promises = state.Identities
            .Select(stake => ItemEffectResolver.FloorPayloadOf(stake.Id, content))
            .Where(payload => payload is not null)
            .Select(payload => payload!.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return promises.Count == 0
            ? "promises nothing on gear yet"
            : $"on gear, promises {string.Join(", ", promises)}";
    }

    /// <summary>The strongest few of one lean list, weight-desc then id — capped so the
    /// profile stays a hint (D53's "never listed exhaustively").</summary>
    private static IEnumerable<string> StrongestLeans(
        IReadOnlyList<WeightedLean> leans, Func<string, string> nameOf) =>
        leans.OrderByDescending(lean => lean.Weight)
            .ThenBy(lean => lean.Id, StringComparer.Ordinal)
            .Take(PresentationTuning.LeaningsShown)
            .Select(lean => nameOf(lean.Id));

    private static string TriggerNameOf(string triggerId, ContentBundle content) =>
        content.SignatureTriggers.TryGetById(triggerId, out var trigger) ? trigger.Name : triggerId;

    private static string BehaviorNameOf(string behaviorId, ContentBundle content) =>
        content.SignatureBehaviors.TryGetById(behaviorId, out var behavior) ? behavior.Name : behaviorId;

    private static string PayloadNameOf(string payloadId, ContentBundle content) =>
        content.SignaturePayloads.TryGetById(payloadId, out var payload) ? payload.Name : payloadId;

    private static string IdentityNameOf(string identityId, ContentBundle content) =>
        content.Identities.TryGetById(identityId, out var identity) ? identity.Name : identityId;
}
