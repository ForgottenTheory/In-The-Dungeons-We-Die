using Dungeons.Items;

namespace Dungeons.Professions;

/// <summary>Why an absence stopped paying out before the whole elapsed time was consumed.</summary>
public enum OfflineStopReason
{
    /// <summary>The full elapsed time was converted into completions.</summary>
    TimeConsumed,

    /// <summary>The action ran out of inputs partway through.</summary>
    InputsExhausted,

    /// <summary>The absence was longer than <see cref="ProfessionTuning.MaxOfflineTicks"/>.</summary>
    TimeCapped,

    /// <summary>More completions were owed than one payout is allowed to resolve.</summary>
    CompletionCapped,
}

/// <summary>What an absence earned, aggregated into one report the client shows on return.</summary>
public sealed class OfflineProgressReport
{
    public required string ActionId { get; init; }
    public required int CompletedActions { get; init; }
    public required long ElapsedTicks { get; init; }
    public required OfflineStopReason StopReason { get; init; }

    /// <summary>Everything produced, merged per item id — not one line per completion.</summary>
    public IReadOnlyList<ItemStack> Produced { get; init; } = Array.Empty<ItemStack>();

    public long XpGained { get; init; }
    public int MasteryGained { get; init; }

    public bool EarnedAnything => CompletedActions > 0;

    public static OfflineProgressReport Nothing(string actionId, OfflineStopReason reason) => new()
    {
        ActionId = actionId,
        CompletedActions = 0,
        ElapsedTicks = 0,
        StopReason = reason,
    };
}

/// <summary>
/// Pays out the passive action the player left running while they were away.
///
/// <para>Levelling must never require staying online, so this is a first-class path, not a
/// courtesy: it runs the <em>same</em> <see cref="ProfessionSystem.Execute"/> as live passive
/// play, at performance 0 and <c>isActive: false</c>. That single fact gives offline play its
/// whole character for free — no active XP bonus, no active bonus-output chance, and
/// structurally no opportunity rolls, because only the active path rolls them.</para>
///
/// <para>It resolves once per <em>completion</em>, never once per tick (docs/professions.md
/// §10), and caps both the elapsed window and the completion count so returning after a
/// fortnight cannot stall the client.</para>
/// </summary>
public static class OfflineProgressCalculator
{
    /// <summary>
    /// Applies <paramref name="elapsedRealSeconds"/> of absence to <paramref name="actionId"/>.
    /// Returns what was earned; the inventory, XP and mastery have already been credited.
    /// </summary>
    public static OfflineProgressReport Apply(ProfessionSystem professions, string actionId, double elapsedRealSeconds)
    {
        ArgumentNullException.ThrowIfNull(professions);

        var rawTicks = elapsedRealSeconds <= 0 ? 0 : (long)Math.Floor(elapsedRealSeconds * ProfessionTuning.TicksPerSecond);
        var availableTicks = ProfessionTuning.OfflineTicks(elapsedRealSeconds);
        var stopReason = rawTicks > availableTicks ? OfflineStopReason.TimeCapped : OfflineStopReason.TimeConsumed;

        if (availableTicks <= 0 || professions.CheckExecutable(actionId) != ActionFailure.None)
            return OfflineProgressReport.Nothing(actionId, stopReason);

        var produced = new Dictionary<string, int>(StringComparer.Ordinal);
        var completions = 0;
        var spentTicks = 0L;
        var xp = 0L;
        var mastery = 0;

        // The interval is re-read every completion: mastery earned during the absence shortens
        // the remaining ones, exactly as it would have live.
        while (true)
        {
            var interval = professions.EffectiveIntervalTicks(actionId);
            if (spentTicks + interval > availableTicks)
                break;

            if (completions >= ProfessionTuning.MaxOfflineCompletions)
            {
                stopReason = OfflineStopReason.CompletionCapped;
                break;
            }

            var outcome = professions.Execute(actionId, performance: 0.0, isActive: false);
            if (!outcome.Success)
            {
                stopReason = OfflineStopReason.InputsExhausted;
                break;
            }

            spentTicks += interval;
            completions++;
            xp += outcome.XpGained;
            mastery += outcome.MasteryGained;
            foreach (var stack in outcome.Produced)
                produced[stack.ItemId] = produced.GetValueOrDefault(stack.ItemId) + stack.Quantity;
        }

        return new OfflineProgressReport
        {
            ActionId = actionId,
            CompletedActions = completions,
            ElapsedTicks = spentTicks,
            StopReason = stopReason,
            Produced = produced
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ItemStack(pair.Key, pair.Value))
                .ToList(),
            XpGained = xp,
            MasteryGained = mastery,
        };
    }
}
