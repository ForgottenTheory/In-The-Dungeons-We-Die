namespace Dungeons.Combat;

/// <summary>
/// How far above the ordinary a creature stands. Composed identity means rank is not a field on
/// anything — it is two tags an actor may carry (D26), which is what lets an elite be a normal
/// actor with one extra line rather than a parallel definition type.
/// </summary>
public enum EnemyRank
{
    Normal,
    Elite,
    Boss,
}

/// <summary>
/// The rank tag vocabulary, and the one place a tag list is turned into a rank.
///
/// <para>These two strings were already load-bearing before this type existed:
/// <c>loot.shared.rank_spoils</c> gates on them, and <c>GameRoot</c> forwards an enemy's
/// identity tags into the loot context so elite and boss spoils pay out without combat knowing
/// what a rank is. Naming them here stops the third and fourth reader from spelling them by
/// hand — <b>these are content ids and may not be renamed</b> without editing every actor that
/// carries one (docs/code-map.md §12).</para>
/// </summary>
public static class EnemyRanks
{
    public const string EliteTag = "elite";
    public const string BossTag = "boss";

    /// <summary>Boss wins over Elite: a creature tagged both is the thing at the bottom.</summary>
    public static EnemyRank Of(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var carried = tags as IReadOnlyCollection<string> ?? tags.ToList();
        if (carried.Contains(BossTag, StringComparer.OrdinalIgnoreCase))
            return EnemyRank.Boss;

        return carried.Contains(EliteTag, StringComparer.OrdinalIgnoreCase)
            ? EnemyRank.Elite
            : EnemyRank.Normal;
    }
}
