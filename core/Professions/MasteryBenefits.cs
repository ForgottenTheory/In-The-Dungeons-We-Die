using Dungeons.Content;

namespace Dungeons.Professions;

/// <summary>
/// What a given mastery level is currently worth. The one place the ladder in
/// <c>game/data/mastery/</c> is turned into numbers the execution path can use.
///
/// <para>Mastery used to be four constants buried in <see cref="ProfessionTuning"/> — the reason
/// GDD §7.3 could say "a number that goes up and does nothing" while the code was in fact reading
/// it. The numbers are content now, so a balance pass is a JSON edit.</para>
/// </summary>
public sealed class MasteryBenefits
{
    /// <summary>Every kind worth zero. For tests and for a host with no mastery content wired —
    /// never a silent fallback in the shipped game, where <c>ContentValidator</c> requires the
    /// ladder to exist.</summary>
    public static readonly MasteryBenefits None = new(Array.Empty<MasteryBenefitDefinition>());

    private readonly Dictionary<(MasteryBenefitKind Kind, string? ProfessionId), MasteryBenefitDefinition> _rungs = new();

    public MasteryBenefits(IEnumerable<MasteryBenefitDefinition> ladder)
    {
        ArgumentNullException.ThrowIfNull(ladder);
        foreach (var rung in ladder)
            _rungs[(rung.Kind, rung.ProfessionId)] = rung;
    }

    public MasteryBenefits(DataStore<MasteryBenefitDefinition> ladder)
        : this((ladder ?? throw new ArgumentNullException(nameof(ladder))).GetAll())
    {
    }

    /// <summary>Every rung, for the ladder the player reads on a profession's page.</summary>
    public IReadOnlyList<MasteryBenefitDefinition> Ladder =>
        _rungs.Values.OrderBy(rung => rung.UnlockLevel).ThenBy(rung => rung.Kind).ToList();

    /// <summary>
    /// What <paramref name="kind"/> is worth to <paramref name="professionId"/> at
    /// <paramref name="masteryPoints"/>. Zero below the rung's unlock level, and zero when the
    /// ladder has no rung for this kind at all.
    /// </summary>
    public double ValueOf(MasteryBenefitKind kind, string? professionId, int masteryPoints)
    {
        if (!TryFindRung(kind, professionId, out var rung))
            return 0.0;

        var level = MasteryLeveling.LevelFor(masteryPoints);
        if (level < rung.UnlockLevel)
            return 0.0;

        return Math.Min(rung.Max, level * rung.PerLevel);
    }

    /// <summary>The mastery level at which <paramref name="kind"/> first does anything, or null
    /// when the ladder has no such rung. What the "next unlock" line reads.</summary>
    public int? UnlockLevelOf(MasteryBenefitKind kind, string? professionId) =>
        TryFindRung(kind, professionId, out var rung) ? rung.UnlockLevel : null;

    /// <summary>A profession's own rung wins over the general one — the "later layer wins per
    /// key" rule, stated once.</summary>
    private bool TryFindRung(MasteryBenefitKind kind, string? professionId, out MasteryBenefitDefinition rung)
    {
        if (professionId is not null && _rungs.TryGetValue((kind, professionId), out rung!))
            return true;

        return _rungs.TryGetValue((kind, null), out rung!);
    }
}
