using System.Text;
using Dungeons.Content;
using Dungeons.Crafting.Identity;

namespace Dungeons.Presentation;

/// <summary>
/// The identity forge's pre-commit reading (migration Phase 6, D53): the composed item and
/// its effect projection in player language. The scored candidate table — which IS the draw
/// distribution — speaks likelihood words derived one-way from the scores it summarizes;
/// the exact scores live in <see cref="Advanced"/>, one toggle away, so the projection still
/// cannot lie and the normal voice still never shows a raw weight (D30).
/// </summary>
public static class MintReadings
{
    /// <summary>The whole normal-voice preview panel.</summary>
    public static string Preview(
        IdentityComposition composition, ItemEffectProjection effects, bool firstOfItsKind, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(content);

        var lines = new List<string>
        {
            firstOfItsKind ? $"{composition.Name} — first of its kind" : composition.Name,
            DeliveryText(composition.BaseDelivery),
        };

        if (composition.Expressed.Count > 0)
            lines.Add("Identities: " + string.Join(" · ",
                composition.Expressed.Select(stake => IdentityPhrases.Stake(stake, content))));

        if (composition.Dormant.Count > 0)
            lines.Add("Dormant: " + string.Join(", ",
                composition.Dormant.Select(stake => IdentityPhrases.Stake(stake, content)))
                + " — waits for a different form");

        foreach (var floorSentence in effects.Floor)
            lines.Add($"Guaranteed: {SentenceReadings.From(floorSentence, content).Text}");

        if (effects.GeneratedSentenceCount > 0 && effects.Candidates.Count > 0)
        {
            lines.Add($"Will draw {effects.GeneratedSentenceCount} of these:");
            var totalScore = effects.Candidates.Sum(candidate => candidate.Score);
            foreach (var candidate in effects.Candidates)
                lines.Add("  " + CandidateLine(candidate, totalScore, effects.Candidates.Count, content));
        }

        if (effects.SignatureChance > 0)
            lines.Add($"A Signature may emerge: {Percent(effects.SignatureChance)}");
        if (effects.DrawbackChance > 0)
            lines.Add($"Drawback risk: {Percent(effects.DrawbackChance)} — the price of Volatile stock");

        return string.Join("\n", lines);
    }

    /// <summary>One row of the draw table: likelihood word, the sentence's shape in
    /// vocabulary names, and the §9 breach said in words rather than a glyph.</summary>
    public static string CandidateLine(
        ScoredSentenceCandidate candidate, double totalScore, int tableSize, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(content);

        var trigger = content.SignatureTriggers.TryGetById(candidate.TriggerId, out var t) ? t.Name : candidate.TriggerId;
        var payloadName = content.SignaturePayloads.TryGetById(candidate.PayloadId, out var payload)
            ? payload.Name
            : candidate.PayloadId;
        var shape = payload is null ? string.Empty : $" ({ShapeWord(candidate.BehaviorId, payload, content)})";
        var breach = candidate.FromProfileBreach ? " — beyond its families" : string.Empty;

        return $"{LikelihoodWord(candidate.Score, totalScore, tableSize)} — {trigger}: {payloadName}{shape}{breach}";
    }

    /// <summary>D53's translation: a candidate's share of the table's total score, measured
    /// against the uniform share so the words survive any table size.</summary>
    public static string LikelihoodWord(double score, double totalScore, int tableSize)
    {
        if (totalScore <= 0 || tableSize <= 0)
            return "A long shot";

        var uniformShare = totalScore / tableSize;
        return score >= uniformShare * PresentationTuning.LikelyUniformShareMultiple ? "Likely"
            : score >= uniformShare * PresentationTuning.PossibleUniformShareMultiple ? "Possible"
            : "A long shot";
    }

    /// <summary>What kind of thing a candidate would be, in one word — the row's shape without
    /// its not-yet-rolled magnitude.</summary>
    public static string ShapeWord(string behaviorId, SignaturePayloadDefinition payload, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(content);

        return behaviorId switch
        {
            "sustain" => "standing bonus",
            "convert" or "imbue" => "move-shaping",
            "amplify" => "surge",
            "afflict" => content.Statuses.TryGetById(payload.Binding.Key, out var status)
                && status.Category == Combat.StatusCategory.State ? "boon" : "affliction",
            "retaliate" => "retaliation",
            "drain" => "leeching",
            "echo" => "echoed move",
            "exchange" => "pact",
            "store" => "charge-up",
            "direct" => payload.Binding.Kind switch
            {
                PayloadBindingKinds.Heal => "healing",
                PayloadBindingKinds.Resource => "recovery",
                _ => "damage",
            },
            _ => "effect",
        };
    }

    /// <summary>The base delivery in combat units — gameplay numbers, not simulation ones.</summary>
    public static string DeliveryText(ItemBaseDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        var parts = new List<string>();
        if (delivery.DamageBonus != 0)
            parts.Add($"+{delivery.DamageBonus:0.#} damage");
        if (delivery.WindupTicks != 0)
            parts.Add($"+{delivery.WindupTicks} windup");
        if (delivery.Armor != 0)
            parts.Add($"+{delivery.Armor:0.#} armor");
        return parts.Count == 0 ? "No physical delivery — an identity vessel." : string.Join(" · ", parts);
    }

    /// <summary>Why a composition refused, said plainly. Deterministic and previewable, like
    /// every identity-system refusal.</summary>
    public static string CompositionRefusal(IdentityCompositionFailure failure) => failure switch
    {
        IdentityCompositionFailure.FormNotMigrated => "This form has not learned the identity craft yet.",
        IdentityCompositionFailure.MissingComponent => "Fill every slot with a material.",
        IdentityCompositionFailure.SlotTagMismatch => "A slot will not take the material chosen for it.",
        _ => "The composition refuses.",
    };

    /// <summary>The Advanced voice: exact scores and engine ids — the same table the normal
    /// voice summarizes, because the toggle reveals depth, never a different truth.</summary>
    public static string Advanced(IdentityComposition composition, ItemEffectProjection effects, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(content);

        var builder = new StringBuilder();
        builder.Append("Quality ").Append(composition.Quality)
            .Append(" · wildest component ").Append(composition.WildestComponentStability);

        foreach (var floorSentence in effects.Floor)
        {
            builder.AppendLine();
            builder.Append("floor  ").Append(floorSentence.TriggerId).Append(" → ")
                .Append(floorSentence.BehaviorId).Append(" → ").Append(floorSentence.PayloadId)
                .Append(' ').Append(floorSentence.Magnitude.ToString("0.####"));
            if (floorSentence.Chance < 1.0)
                builder.Append(" @ ").Append(floorSentence.Chance.ToString("0.##"));
        }

        foreach (var candidate in effects.Candidates)
        {
            builder.AppendLine();
            builder.Append(candidate.Score.ToString("0.###").PadLeft(8)).Append("  ")
                .Append(candidate.TriggerId).Append(" → ")
                .Append(candidate.BehaviorId).Append(" → ")
                .Append(candidate.PayloadId);
            if (candidate.FromProfileBreach)
                builder.Append(" ◇");
        }

        builder.AppendLine();
        builder.Append("signature ").Append(effects.SignatureChance.ToString("0.###"))
            .Append(" · drawback ").Append(effects.DrawbackChance.ToString("0.###"));

        return builder.ToString();
    }

    private static string Percent(double fraction) => $"{Math.Round(fraction * 100, 1):0.#}%";
}
