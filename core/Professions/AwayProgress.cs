using Dungeons.Items;

namespace Dungeons.Professions;

/// <summary>A profession that gained a level (or several) while the player was away.</summary>
public readonly record struct AwayLevelGain(string ProfessionId, int LevelBefore, int LevelAfter)
{
    public int LevelsGained => LevelAfter - LevelBefore;
}

/// <summary>
/// Everything an absence earned, in one object the player is shown on return.
///
/// <para><b>Why this exists rather than more log lines.</b> The payout already worked; what the
/// player got back was three sentences scrolling past in a console shared with combat traces and
/// crafting output. "Show a clear summary of what happened while away" is a real requirement of
/// idle play — the return <em>is</em> the session's first beat, and it has to be readable in one
/// glance.</para>
///
/// <para>It aggregates, it does not resolve: every number here was produced by
/// <see cref="ProfessionSystem.Execute"/> through <see cref="OfflineProgressCalculator"/> and by
/// the farming plots, on the same rules live passive play uses.</para>
/// </summary>
public sealed class AwayReport
{
    /// <summary>Wall-clock seconds between the save and the load.</summary>
    public required long ElapsedSeconds { get; init; }

    /// <summary>Ticks actually credited — less than the elapsed time when the cap bit.</summary>
    public required long CreditedTicks { get; init; }

    /// <summary>What the passive selection earned, or null if nothing was selected.</summary>
    public OfflineProgressReport? PassiveWork { get; init; }

    /// <summary>Crops that finished growing while the game was closed.</summary>
    public IReadOnlyList<ActionOutcome> Harvests { get; init; } = Array.Empty<ActionOutcome>();

    /// <summary>Everything produced by both, merged per item id and sorted — not one line per
    /// completion, because a twelve-hour absence is thousands of completions.</summary>
    public IReadOnlyList<ItemStack> Produced { get; init; } = Array.Empty<ItemStack>();

    /// <summary>Professions that levelled during the absence, in id order.</summary>
    public IReadOnlyList<AwayLevelGain> LevelsGained { get; init; } = Array.Empty<AwayLevelGain>();

    public long XpGained { get; init; }
    public int MasteryGained { get; init; }

    /// <summary>Why the passive payout stopped. <see cref="OfflineStopReason.TimeConsumed"/>
    /// when nothing was selected — no absence was cut short.</summary>
    public OfflineStopReason StopReason { get; init; } = OfflineStopReason.TimeConsumed;

    /// <summary>The absence was longer than the offline cap, and the excess paid nothing.</summary>
    public bool WasTimeCapped => StopReason == OfflineStopReason.TimeCapped;

    /// <summary>Whether there is anything worth showing the player.</summary>
    public bool EarnedAnything =>
        (PassiveWork?.EarnedAnything ?? false) || Harvests.Count > 0;

    /// <summary>An absence that earned nothing — either none was measurable, or nothing was
    /// left running.</summary>
    public static AwayReport Nothing(long elapsedSeconds) => new()
    {
        ElapsedSeconds = elapsedSeconds,
        CreditedTicks = 0,
    };
}

/// <summary>
/// Resolves one absence into an <see cref="AwayReport"/>: the standing passive selection, the
/// crops that came ready, and what both did to the player's levels.
///
/// <para><b>It owns no rules.</b> The passive payout is <see cref="OfflineProgressCalculator"/>
/// — which is itself just <see cref="ProfessionSystem.Execute"/> in a loop — and the harvest is
/// <see cref="FarmingPlots.HarvestAllReady"/>. There is deliberately no offline simulator:
/// online passive, offline passive and active play are one code path, and this only decides what
/// to <em>tell the player</em> about a stretch of it.</para>
///
/// <para><b>Rebasing the crop clock is the client's job, and must already have happened.</b> A
/// plot stores an absolute ready-tick and the simulation clock restarts at zero every session,
/// so the host moves the remaining growth onto the new clock before calling this
/// (<c>GameRoot.RebasePlantedCrops</c>). Core has no wall clock to do it with.</para>
/// </summary>
public static class AwayProgress
{
    /// <summary>
    /// Applies <paramref name="elapsedRealSeconds"/> of absence and reports it. The inventory,
    /// XP and mastery have already been credited when this returns.
    /// </summary>
    /// <param name="selectedActionId">The standing passive selection, or null.</param>
    /// <param name="plots">The farming plots, already rebased onto this session's clock.</param>
    /// <param name="currentTick">Now, on the simulation clock — what a crop's readiness is measured against.</param>
    public static AwayReport Resolve(
        ProfessionSystem professions,
        string? selectedActionId,
        double elapsedRealSeconds,
        FarmingPlots? plots = null,
        long currentTick = 0)
    {
        ArgumentNullException.ThrowIfNull(professions);

        if (elapsedRealSeconds <= 0)
            return AwayReport.Nothing(0);

        var elapsedSeconds = (long)Math.Floor(elapsedRealSeconds);
        var levelsBefore = SnapshotLevels(professions);

        // Crops first: a harvest can put back exactly the input a stalled passive action needs,
        // and paying the absence in the order it happened is also the order that is kindest.
        var harvests = plots?.HarvestAllReady(currentTick) ?? Array.Empty<ActionOutcome>();

        var passiveWork = selectedActionId is null
            ? null
            : OfflineProgressCalculator.Apply(professions, selectedActionId, elapsedRealSeconds);

        var produced = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var stack in harvests.SelectMany(outcome => outcome.Produced))
            produced[stack.ItemId] = produced.GetValueOrDefault(stack.ItemId) + stack.Quantity;
        foreach (var stack in passiveWork?.Produced ?? Array.Empty<ItemStack>())
            produced[stack.ItemId] = produced.GetValueOrDefault(stack.ItemId) + stack.Quantity;

        return new AwayReport
        {
            ElapsedSeconds = elapsedSeconds,
            CreditedTicks = passiveWork?.ElapsedTicks ?? 0,
            PassiveWork = passiveWork,
            Harvests = harvests,
            Produced = produced
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ItemStack(pair.Key, pair.Value))
                .ToList(),
            LevelsGained = LevelsSince(professions, levelsBefore),
            XpGained = (passiveWork?.XpGained ?? 0) + harvests.Sum(outcome => outcome.XpGained),
            MasteryGained = (passiveWork?.MasteryGained ?? 0) + harvests.Sum(outcome => outcome.MasteryGained),
            StopReason = passiveWork?.StopReason ?? OfflineStopReason.TimeConsumed,
        };
    }

    private static Dictionary<string, int> SnapshotLevels(ProfessionSystem professions) =>
        professions.AllProgress.ToDictionary(
            progress => progress.ProfessionId, progress => progress.Level, StringComparer.Ordinal);

    /// <summary>
    /// Levels gained since the snapshot. A profession absent from it started at
    /// <see cref="ProfessionLeveling.MinLevel"/> — an untouched profession has no progress
    /// record, and reading that as "level 0" would report a phantom level-up the first time one
    /// is used.
    /// </summary>
    private static IReadOnlyList<AwayLevelGain> LevelsSince(
        ProfessionSystem professions, Dictionary<string, int> levelsBefore)
    {
        var gains = new List<AwayLevelGain>();
        foreach (var progress in professions.AllProgress.OrderBy(p => p.ProfessionId, StringComparer.Ordinal))
        {
            var before = levelsBefore.GetValueOrDefault(progress.ProfessionId, ProfessionLeveling.MinLevel);
            if (progress.Level > before)
                gains.Add(new AwayLevelGain(progress.ProfessionId, before, progress.Level));
        }

        return gains;
    }
}
