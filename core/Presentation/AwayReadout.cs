using Dungeons.Professions;

namespace Dungeons.Presentation;

/// <summary>One line of the return summary: a heading and the plain sentence under it.</summary>
public sealed record AwayLine(string Heading, string Detail);

/// <summary>
/// The words the player reads when they come back. One place, so the console log and the
/// summary panel cannot describe the same absence two different ways (D30, CLAUDE.md rule 7).
///
/// <para><b>Nothing here computes anything.</b> Every number arrived on the
/// <see cref="AwayReport"/>, which got it from the same <see cref="ProfessionSystem.Execute"/>
/// live passive play uses. This decides only how to say it — including the two things a summary
/// has to be honest about: that the offline cap swallowed part of a long absence, and that the
/// work stopped early because the materials ran out.</para>
///
/// <para><b>It speaks in completions and items, never in ticks or rates.</b> Raw simulation
/// values do not belong on a normal play surface; "×412" and "9.3h away" are the player's units,
/// "8,240 ticks" is the simulation's.</para>
/// </summary>
public static class AwayReadout
{
    /// <summary>How long they were gone, in the largest unit that still reads as a duration.</summary>
    public static string Duration(long seconds) => seconds switch
    {
        < 60 => $"{seconds}s",
        < 3600 => $"{seconds / 60}m",
        < 86400 => $"{seconds / 3600.0:0.#}h",
        _ => $"{seconds / 86400.0:0.#} days",
    };

    /// <summary>The headline: what the absence was, before any detail.</summary>
    public static string Headline(AwayReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.EarnedAnything
            ? $"You were away {Duration(report.ElapsedSeconds)}."
            : $"You were away {Duration(report.ElapsedSeconds)}, and nothing was left running.";
    }

    /// <summary>
    /// Why the payout is smaller than the clock says, or empty when it is not. Deliberately
    /// distinct sentences: "you were gone too long" and "you ran out of oak" are different
    /// problems and only one of them is worth doing something about.
    /// </summary>
    public static string StopNote(AwayReport report, long offlineCapHours)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.StopReason switch
        {
            OfflineStopReason.InputsExhausted =>
                "The work stopped early — it ran out of materials. It will pick itself back up when there are more.",
            OfflineStopReason.TimeCapped =>
                $"Only the first {offlineCapHours}h of that paid out; that is as far as unattended work goes.",
            OfflineStopReason.CompletionCapped =>
                "That is as much work as one absence resolves in a single go.",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// The whole summary as headed lines, in reading order. Empty for an absence that earned
    /// nothing, so a caller can decide not to show the panel at all rather than showing an
    /// empty one.
    /// </summary>
    /// <param name="actionName">An action id → its player-facing name.</param>
    /// <param name="professionName">A profession id → its player-facing name.</param>
    /// <param name="itemName">An item id → its player-facing name.</param>
    public static IReadOnlyList<AwayLine> Lines(
        AwayReport report,
        Func<string, string> actionName,
        Func<string, string> professionName,
        Func<string, string> itemName,
        long offlineCapHours)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(actionName);
        ArgumentNullException.ThrowIfNull(professionName);
        ArgumentNullException.ThrowIfNull(itemName);

        var lines = new List<AwayLine>();
        if (!report.EarnedAnything)
            return lines;

        if (report.PassiveWork is { EarnedAnything: true } work)
            lines.Add(new AwayLine(actionName(work.ActionId), $"finished {work.CompletedActions} times"));

        if (report.Harvests.Count > 0)
        {
            var crops = string.Join(", ", report.Harvests
                .GroupBy(outcome => outcome.ActionId, StringComparer.Ordinal)
                .Select(group => group.Count() == 1
                    ? actionName(group.Key)
                    : $"{actionName(group.Key)} ×{group.Count()}"));
            lines.Add(new AwayLine("Crops came in", crops));
        }

        if (report.Produced.Count > 0)
        {
            var carried = string.Join(", ", report.Produced.Select(stack => $"{itemName(stack.ItemId)} ×{stack.Quantity}"));
            lines.Add(new AwayLine("You now have", carried));
        }

        if (report.XpGained > 0)
            lines.Add(new AwayLine("Experience", $"+{report.XpGained}"));

        if (report.MasteryGained > 0)
            lines.Add(new AwayLine("Mastery", $"+{report.MasteryGained}"));

        foreach (var gain in report.LevelsGained)
        {
            lines.Add(new AwayLine(
                professionName(gain.ProfessionId),
                gain.LevelsGained == 1
                    ? $"reached level {gain.LevelAfter}"
                    : $"reached level {gain.LevelAfter} — {gain.LevelsGained} levels"));
        }

        var note = StopNote(report, offlineCapHours);
        if (note.Length > 0)
            lines.Add(new AwayLine("Note", note));

        return lines;
    }

    /// <summary>The whole summary as one console line, for the event log.</summary>
    public static string OneLine(
        AwayReport report,
        Func<string, string> actionName,
        Func<string, string> itemName,
        long offlineCapHours)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (!report.EarnedAnything)
            return Headline(report);

        var parts = new List<string>();
        if (report.PassiveWork is { EarnedAnything: true } work)
            parts.Add($"{actionName(work.ActionId)} ×{work.CompletedActions}");
        if (report.Harvests.Count > 0)
            parts.Add($"{report.Harvests.Count} crop(s) lifted");
        if (report.Produced.Count > 0)
            parts.Add(string.Join(", ", report.Produced.Select(stack => $"{itemName(stack.ItemId)} ×{stack.Quantity}")));
        if (report.XpGained > 0)
            parts.Add($"xp +{report.XpGained}");

        var note = StopNote(report, offlineCapHours);
        return $"{Headline(report)} {string.Join(" · ", parts)}." + (note.Length > 0 ? $" {note}" : string.Empty);
    }
}
