namespace Dungeons.Professions;

/// <summary>
/// The one question the execution path asks: <b>what is this benefit worth, right now, for this
/// action?</b> — with every source that can answer it folded into one number.
///
/// <para>Two sources today, and the shape is what matters:</para>
/// <list type="bullet">
///   <item><see cref="Mastery"/> — what repeating <em>this action</em> bought (per-action).</item>
///   <item><see cref="Synergies"/> — what the player's other progress buys (cross-profession,
///   and the global total-level bonus).</item>
/// </list>
///
/// <para><b>Why this exists rather than a second call at each site.</b>
/// <see cref="ActionResolver"/> and <see cref="ProfessionSystem"/> ask for a benefit in six
/// places. Every place that had to remember to add a second source would eventually be a place
/// that forgot — and the forgotten one would be a bonus that silently does nothing, which is the
/// exact failure Phase 8 was written to fix. Adding E6's worn tools later is a third field here
/// and no change at all downstream.</para>
///
/// <para><b>Sources add.</b> Preservation from mastery plus preservation from a synergy is more
/// preservation; every individual contribution is capped where it is authored, and the six
/// consumers clamp their own totals where a fraction has to stay inside [0, 1].</para>
/// </summary>
public sealed class ProfessionBenefits
{
    /// <summary>Every benefit worth zero — a host with no profession content wired, and the
    /// default so that a test that does not care about benefits need not supply any.</summary>
    public static readonly ProfessionBenefits None = new(MasteryBenefits.None, ProfessionSynergies.None);

    private readonly Func<string, int> _levelOf;
    private readonly Func<int> _totalLevel;

    /// <param name="levelOf">A profession's current level; a synergy's source reads this.</param>
    /// <param name="totalLevel">The sum of every profession level — the global synergy's source.
    /// Passed in rather than derived, because Core's profession progress is held by
    /// <see cref="ProfessionSystem"/> and this must not depend on it in order to be constructed
    /// before it.</param>
    public ProfessionBenefits(
        MasteryBenefits mastery,
        ProfessionSynergies synergies,
        Func<string, int>? levelOf = null,
        Func<int>? totalLevel = null)
    {
        Mastery = mastery ?? throw new ArgumentNullException(nameof(mastery));
        Synergies = synergies ?? throw new ArgumentNullException(nameof(synergies));
        _levelOf = levelOf ?? (_ => 0);
        _totalLevel = totalLevel ?? (() => 0);
    }

    /// <summary>The mastery ladder alone — what every call site read before Phase 10, and what a
    /// test that is about mastery should still be able to ask for in one expression.</summary>
    public static ProfessionBenefits FromMasteryLadder(MasteryBenefits mastery) =>
        new(mastery, ProfessionSynergies.None);

    public MasteryBenefits Mastery { get; }
    public ProfessionSynergies Synergies { get; }

    /// <summary>
    /// What <paramref name="kind"/> is worth to <paramref name="professionId"/> given
    /// <paramref name="actionMasteryPoints"/> banked in the action being performed.
    /// </summary>
    public double ValueOf(ProfessionBenefitKind kind, string? professionId, int actionMasteryPoints) =>
        Mastery.ValueOf(kind, professionId, actionMasteryPoints)
        + Synergies.ValueOf(kind, professionId, _levelOf, _totalLevel);

    /// <summary>The synergy half alone, for the readout that tells the player <em>why</em> a
    /// number is higher than their mastery explains.</summary>
    public double SynergyValueOf(ProfessionBenefitKind kind, string? professionId) =>
        Synergies.ValueOf(kind, professionId, _levelOf, _totalLevel);

    /// <summary>The level a synergy's source currently sits at — one profession's, or the
    /// player's total. What the synergy readout shows next to each line.</summary>
    public int SourceLevelOf(ProfessionSynergyDefinition synergy)
    {
        ArgumentNullException.ThrowIfNull(synergy);
        return synergy.IsGlobalSource ? _totalLevel() : _levelOf(synergy.SourceProfession!);
    }
}
