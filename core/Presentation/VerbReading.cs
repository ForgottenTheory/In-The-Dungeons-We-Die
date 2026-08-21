using Dungeons.Content;
using Dungeons.Crafting.Identity;

namespace Dungeons.Presentation;

/// <summary>
/// The identity bench in player language (migration Phase 6): refusals said plainly, and the
/// preview/result lines derived by diffing the states the engine produced — the same
/// read-model shape <c>CraftReading</c> proved. The engine's own <see cref="VerbStep"/> text
/// stays the Advanced/log-tail voice; nothing here recomputes a mechanic, it only reads what
/// the projection already settled (D30: translate, never recompute).
/// </summary>
public static class VerbReadings
{
    /// <summary>Why the material refuses, in words. Refusals are deterministic and
    /// previewable — the wording is part of the §4 fairness contract.</summary>
    public static string Refusal(VerbFailureReason reason) => reason switch
    {
        VerbFailureReason.MissingTargetIdentity => "Choose which identity to work.",
        VerbFailureReason.MissingOutputDefinition => "This action names nothing to become.",
        VerbFailureReason.OutputDefinitionUnknown => "The output this action names does not exist.",
        VerbFailureReason.OutputDefinitionNotMigrated => "The output has not learned the identity craft yet.",
        VerbFailureReason.NoSources => "This work needs source material.",
        VerbFailureReason.TooManySources => "Too many sources for this work.",
        VerbFailureReason.IdentityAlreadyActive => "That identity is already awake in this material.",
        VerbFailureReason.IdentityNotActive => "That identity is not active in this material.",
        VerbFailureReason.IdentityNotLatent => "Nothing of that sleeps in this material.",
        VerbFailureReason.NoFreeSlot => "No room — every identity slot is taken.",
        VerbFailureReason.OverfillLimit => "It cannot hold more, even overfilled.",
        VerbFailureReason.SourceLacksIdentity => "The source does not carry that identity.",
        VerbFailureReason.InsufficientDevelopment => "Not enough feed to deepen it.",
        VerbFailureReason.RankAtMaximum => "It is already as deep as it can go.",
        VerbFailureReason.QualityAtMaximum => "The workmanship cannot be improved further.",
        VerbFailureReason.ConditionAtCeiling => "Restoration can take it no further.",
        VerbFailureReason.CapacityAtCeiling => "Its capacity is already at the ceiling.",
        VerbFailureReason.DisplacedIdentityNotActive => "The identity to eject is not active.",
        _ => "The material refuses.",
    };

    /// <summary>The pre-commit reading: what would change, then the odds the crafter chose.</summary>
    public static IReadOnlyList<string> ProjectionLines(
        CraftVerb verb, IdentityMaterialState before, VerbProjection projection,
        string? outputName, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(content);

        if (projection.Failure is { } refusal)
            return new[] { Refusal(refusal) };

        var lines = ChangeLines(verb, before, projection.Result, projection.Produced, outputName, content);
        lines.AddRange(RiskLines(projection.Risks));
        return lines;
    }

    /// <summary>The committed result's reading — the same diff voice, plus what the dice did.</summary>
    public static IReadOnlyList<string> OutcomeLines(
        CraftVerb verb, IdentityMaterialState before, VerbOutcome outcome,
        string? outputName, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(content);

        switch (outcome.Kind)
        {
            case VerbResultKind.Refused:
                return new[] { Refusal(outcome.Failure ?? VerbFailureReason.MissingTargetIdentity) };

            case VerbResultKind.Destroyed:
                return new[] { "The Fragile gamble lands — the material is destroyed. Byproducts are still paid." };

            case VerbResultKind.Fractured:
                var broken = outcome.FracturedIdentityId is { } fracturedId
                    ? IdentityName(fracturedId, content)
                    : "the newest identity";
                return new[] { $"The overfilled material fractures — {broken} breaks away and the work is lost (condition still paid)." };

            default:
                return ChangeLines(verb, before, outcome.Result, outcome.Produced, outputName, content);
        }
    }

    // ---- The diff core -----------------------------------------------------------------------

    /// <summary>What changed between the substrate and the projected result, one sentence per
    /// fact, identities by name and ranks as rung words (D44 — never numerals).</summary>
    private static List<string> ChangeLines(
        CraftVerb verb, IdentityMaterialState before, IdentityMaterialState? after,
        IReadOnlyList<IdentityMaterialState> produced, string? outputName, ContentBundle content)
    {
        var lines = new List<string>();

        if (verb == CraftVerb.Process)
            lines.Add($"Becomes {outputName ?? "its worked form"} — its identities carry through.");
        if (verb == CraftVerb.Fuse)
            lines.Add("Fuses into one material.");

        if (verb == CraftVerb.Extract)
        {
            var carrier = produced.FirstOrDefault(state => state.IsCarrier);
            var drawn = carrier?.Identities.Count > 0
                ? IdentityPhrases.Stake(carrier.Identities[0], content)
                : "the identity";
            lines.Add($"Draws {drawn} out onto a fresh carrier — the source is spent.");
        }

        if (after is not null)
        {
            AddIdentityDiffs(lines, before, after, content);

            if (after.Quality != before.Quality)
                lines.Add(WorkmanshipLine(before.Quality, after.Quality));
            if (after.Capacity != before.Capacity)
                lines.Add($"Capacity: {before.Capacity} → {after.Capacity} identity slots.");
            if (after.Condition != before.Condition)
                lines.Add(ConditionLine(before.Condition, after.Condition));
            if (after.Stability != before.Stability && after.Stability != Stability.Stable)
                lines.Add($"Now {after.Stability} — {IdentityPhrases.OverfillMeaning(after.Stability)}");
        }

        return lines;
    }

    private static void AddIdentityDiffs(
        List<string> lines, IdentityMaterialState before, IdentityMaterialState after, ContentBundle content)
    {
        foreach (var stake in after.Identities)
        {
            var previous = before.StakeOf(stake.Id);
            if (previous is null)
            {
                var wasLatent = before.Latent.Contains(stake.Id, StringComparer.Ordinal);
                lines.Add(wasLatent
                    ? $"{IdentityName(stake.Id, content)} awakens."
                    : $"{IdentityPhrases.Stake(stake, content)} settles in.");
            }
            else if (stake.Rank > previous.Rank)
            {
                lines.Add($"{IdentityName(stake.Id, content)} deepens — now {IdentityPhrases.RungWord(stake.Rank)}.");
            }
        }

        foreach (var stake in before.Identities)
        {
            if (after.StakeOf(stake.Id) is null)
                lines.Add($"{IdentityName(stake.Id, content)} is ejected — no refund.");
        }
    }

    private static string WorkmanshipLine(int qualityBefore, int qualityAfter)
    {
        var wordBefore = IdentityPhrases.QualityWord(qualityBefore);
        var wordAfter = IdentityPhrases.QualityWord(qualityAfter);
        return wordBefore == wordAfter
            ? $"Workmanship improves a little — still {wordAfter}."
            : $"Workmanship: {wordBefore} → {wordAfter}.";
    }

    private static string ConditionLine(Condition before, Condition after)
    {
        var line = $"Condition: {before} → {after}.";
        return after == Condition.Fragile
            ? line + " Deeper work now gambles destruction."
            : line;
    }

    /// <summary>The §4 fairness lines: odds on screen before the click, zero unless the
    /// crafter chose the risk. Percentages are gameplay odds, not simulation values.</summary>
    private static IEnumerable<string> RiskLines(VerbRisks risks)
    {
        if (risks.FractureChance > 0)
            yield return $"Overfilled: {risks.FractureChance:P0} chance the newest identity fractures away.";
        if (risks.DestructionChance > 0)
            yield return $"Fragile: {risks.DestructionChance:P0} chance this work destroys the material — byproducts still paid.";
    }

    private static string IdentityName(string identityId, ContentBundle content) =>
        content.Identities.TryGetById(identityId, out var identity) ? identity.Name : identityId;
}
