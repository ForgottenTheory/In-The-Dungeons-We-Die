namespace Dungeons.Realms;

/// <summary>
/// What Realm Knowledge buys. Each entry is a thing the party comes to <b>know</b> about a
/// Realm, and knowing it changes what they can decide.
///
/// <para><b>Knowledge unlocks options, never damage</b> (GDD §11.4). A percentage would make
/// Knowledge a second power curve, and the realm would get easier for reasons the player cannot
/// see. Instead the realm stays exactly as lethal and the player stops walking into it blind —
/// which is the difference between a grind and a map.</para>
/// </summary>
public enum RealmInsight
{
    /// <summary>Read an enemy's resistances and damage-type vulnerabilities before committing.
    /// The counter was always there; this is what makes it discoverable.</summary>
    EnemyWeaknesses,

    /// <summary>See a hazard on the map instead of discovering it by standing in it — the
    /// difference between an ambush and a route choice.</summary>
    Hazards,

    /// <summary>Know which gathering nodes are the rich ones before spending run-time on them.</summary>
    RichNodes,

    /// <summary>See the hidden nodes: caches, shortcuts and side routes that were in the graph
    /// all along.</summary>
    HiddenRoutes,

    /// <summary>Know where the way out is from wherever you are standing — the information the
    /// extraction decision is actually made on.</summary>
    ExtractionRoutes,
}

/// <summary>
/// The thresholds at which each <see cref="RealmInsight"/> unlocks, and the one place that
/// question is answered.
///
/// <para>The order is the intended arc of learning a place: first you learn what lives here,
/// then where it is dangerous, then where it is worth working, then the ways through, and last
/// the ways out — because knowing every exit is what finally lets you push deep on purpose.</para>
/// </summary>
public static class RealmKnowledgeLevels
{
    /// <summary>
    /// Knowledge required for each insight. Set against the measured yield of one thorough Dark
    /// Forest run (~71), so the ladder spans roughly <b>eight thorough runs</b> end to end:
    /// 0.4 / 1.1 / 2.3 / 4.5 / 7.9.
    ///
    /// <para>The first cut shipped at 6/12/20/30/42 and a single first run cleared the whole
    /// ladder — including the hidden routes that are supposed to be the reward for learning the
    /// place. <c>DarkForestBalanceTests</c> now pins the ratio rather than the numbers, so
    /// retuning the per-node grants cannot silently trivialise this again.</para>
    ///
    /// <para>Still a first pass on FEEL — nobody has played it. The ORDER is the design.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<RealmInsight, int> Required =
        new Dictionary<RealmInsight, int>
        {
            [RealmInsight.EnemyWeaknesses] = 30,
            [RealmInsight.Hazards] = 75,
            [RealmInsight.RichNodes] = 160,
            [RealmInsight.HiddenRoutes] = 320,
            [RealmInsight.ExtractionRoutes] = 560,
        };

    public static bool Reveals(int knowledge, RealmInsight insight) =>
        knowledge >= Required[insight];

    /// <summary>Everything this much knowledge has unlocked, in unlock order.</summary>
    public static IReadOnlyList<RealmInsight> Unlocked(int knowledge) =>
        Required.Where(pair => knowledge >= pair.Value)
            .OrderBy(pair => pair.Value)
            .Select(pair => pair.Key)
            .ToList();

    /// <summary>The next thing to learn, and what it costs — so the player can see the ladder
    /// rather than guess at it. Null once everything is known.</summary>
    public static (RealmInsight Insight, int Required)? Next(int knowledge)
    {
        var pending = Required.Where(pair => knowledge < pair.Value)
            .OrderBy(pair => pair.Value)
            .ToList();

        return pending.Count == 0 ? null : (pending[0].Key, pending[0].Value);
    }
}
