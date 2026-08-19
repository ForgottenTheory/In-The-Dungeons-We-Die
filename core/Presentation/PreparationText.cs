using Dungeons.Combat;
using Dungeons.Realms;

namespace Dungeons.Presentation;

/// <summary>
/// The words the preparation screen and the Realm log both use. One place, because the same
/// insight is announced when it unlocks and listed when the player reads the ladder, and two
/// spellings of "you can now see the dangerous ground" is how a game starts sounding like two
/// games (D30, CLAUDE.md rule 7).
/// </summary>
public static class PreparationText
{
    /// <summary>
    /// What an insight lets the party <em>do</em>, phrased to complete "you have learned this
    /// place well enough to …". Never what it grants — Knowledge grants nothing (GDD §11.4).
    /// </summary>
    public static string DescribeInsight(RealmInsight insight) => insight switch
    {
        RealmInsight.EnemyWeaknesses => "read what lives here",
        RealmInsight.Hazards => "see the dangerous ground before you stand on it",
        RealmInsight.RichNodes => "tell the rich workings from the poor ones",
        RealmInsight.HiddenRoutes => "find the ways nobody marked",
        RealmInsight.CommonResources => "know what this place is made of",
        RealmInsight.ExtractionRoutes => "always know where the way out is",
        RealmInsight.DeepEntry => "begin an expedition at a deeper door",
        _ => insight.ToString(),
    };

    /// <summary>The heading a locked section of the briefing shows instead of its contents. The
    /// player should be able to tell "there is nothing here" from "you do not know yet".</summary>
    public static string NotYetKnown(RealmInsight insight) => insight switch
    {
        RealmInsight.EnemyWeaknesses => "You do not know what lives here.",
        RealmInsight.Hazards => "You do not know where the ground turns against you.",
        RealmInsight.RichNodes => "You cannot yet tell the rich workings from the poor ones.",
        RealmInsight.HiddenRoutes => "If there are ways nobody marked, you have not found them.",
        RealmInsight.CommonResources => "You do not know what this place is made of.",
        RealmInsight.ExtractionRoutes => "You do not know where the ways out are.",
        RealmInsight.DeepEntry => "You do not know the ways in well enough to start anywhere but the edge.",
        _ => "Not yet known.",
    };

    /// <summary>
    /// What is wrong with a loadout, said plainly. Every one of these is advice — the door is
    /// never locked (see <see cref="LoadoutIssue"/>).
    /// </summary>
    public static string Describe(LoadoutIssue issue) => issue switch
    {
        LoadoutIssue.NoRealmSelected => "Choose where you are going.",
        LoadoutIssue.NoWeaponEquipped => "Nothing in hand — you will be fighting with your fists.",
        LoadoutIssue.NothingWorn => "Nothing worn. Everything that hits you will hit you in full.",
        LoadoutIssue.EquippedItemUnresolved => "Something you are wearing cannot be read, and is doing nothing.",
        LoadoutIssue.PackedConsumableNotHeld => "Your pack asks for supplies the Stash no longer has; you will take what is there.",
        _ => issue.ToString(),
    };

    /// <summary>A rank worth calling out before the player walks into it. Normal enemies get no
    /// label — every fight would carry one, and a label everything has says nothing.</summary>
    public static string? RankLabel(EnemyRank rank) => rank switch
    {
        EnemyRank.Elite => "ELITE",
        EnemyRank.Boss => "BOSS",
        _ => null,
    };

    /// <summary>
    /// A lane or damage-type key as a player reads it. These keys are lowercase content ids
    /// (<c>toxin</c>) or already-capitalised damage types (<c>Slashing</c>), and the screen wants
    /// one consistent shape from both.
    /// </summary>
    public static string LaneLabel(string key) =>
        string.IsNullOrEmpty(key) ? key : char.ToUpperInvariant(key[0]) + key[1..].Replace('_', ' ');
}
