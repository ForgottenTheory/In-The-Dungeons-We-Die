using Dungeons.Content;
using Dungeons.Crafting.Identity;

namespace Dungeons.Presentation;

/// <summary>
/// Identity stakes in player words. Ranks render as the §4 access-ladder's own qualitative
/// words — basic is the unmarked norm, so "Vital" IS rank 1 — never as numerals (D44; the
/// named-evolution ladder of §14 #4 stays open, and these words are the neutral reading in
/// the meantime, not a commitment to it).
/// </summary>
public static class IdentityPhrases
{
    /// <summary>"Vital", "Vital (improved)", "Vital (build-changing)".</summary>
    public static string Stake(IdentityStake stake, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(stake);
        ArgumentNullException.ThrowIfNull(content);

        var name = content.Identities.TryGetById(stake.Id, out var identity) ? identity.Name : stake.Id;
        var rung = RungWord(stake.Rank);
        return rung.Length == 0 ? name : $"{name} ({rung})";
    }

    /// <summary>The §4 rung ladder: basic → improved → advanced → build-changing. Basic is
    /// unmarked — an identity's plain name already means "carried at all".</summary>
    public static string RungWord(int rank) => rank switch
    {
        <= 1 => string.Empty,
        2 => "improved",
        3 => "advanced",
        _ => "build-changing",
    };

    /// <summary>Compact overfill marker for picker rows — empty when Stable, the §10.3 ladder
    /// word otherwise. The enum's own words are the player words by design.</summary>
    public static string StabilityMarker(IdentityMaterialState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Stability == Stability.Stable ? string.Empty : $" · {state.Stability}";
    }

    /// <summary>Compact wear marker for picker rows — empty until the work budget is half
    /// spent; Strained and Fragile are the states a crafter must see before choosing.</summary>
    public static string ConditionMarker(IdentityMaterialState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Condition is Condition.Strained or Condition.Fragile
            ? $" · {state.Condition}"
            : string.Empty;
    }

    /// <summary>Workmanship 0–100 as a word — the raw number is Advanced-only (D30).</summary>
    public static string QualityWord(int quality) => quality switch
    {
        < PresentationTuning.DecentQualityFloor => "rough",
        < PresentationTuning.FineQualityFloor => "decent",
        < PresentationTuning.ExcellentQualityFloor => "fine",
        < PresentationTuning.MasterworkQualityFloor => "excellent",
        _ => "masterwork",
    };

    /// <summary>What a §10.3 overfill step means for the hands holding it.</summary>
    public static string OverfillMeaning(Stability stability) => stability switch
    {
        Stability.Volatile => "fracture is likely, and a mint from it may curse its wearer.",
        Stability.Unstable => "working it further risks fracture.",
        _ => string.Empty,
    };

    /// <summary>What a §10.4 condition state means for further work.</summary>
    public static string ConditionMeaning(Condition condition) => condition switch
    {
        Condition.Pristine => "never deeply worked",
        Condition.Worked => "has taken deep work",
        Condition.Strained => "most of its work budget is spent",
        _ => "further deep work gambles destruction",
    };
}
