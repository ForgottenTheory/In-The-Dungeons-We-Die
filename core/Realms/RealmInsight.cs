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
    /// <summary>
    /// Know what this place yields before walking in — the materials its ground and its
    /// creatures actually give up.
    ///
    /// <para>The cheapest rung, and first on purpose: a party that has been here once should
    /// come away knowing <em>something</em>, and "what grows here" is the thing a real
    /// expedition learns first. It is not the same as <see cref="RichNodes"/>, which is knowing
    /// <b>where</b> the good ground is — this is only knowing what the place is made of.</para>
    /// </summary>
    CommonResources,

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

    /// <summary>
    /// Begin a run at a deeper entrance you have already learned — GDD §11.4's "portal
    /// targeting", and the only rung that hands the player an <b>option</b> rather than a fact.
    ///
    /// <para>Last on the ladder because it is the one thing that can only be earned by knowing
    /// the ways out: you cannot aim at a door you have not found. It grants no power — starting
    /// at depth 2 skips the shallow fights, which means skipping the shallow loot and the
    /// shallow knowledge too. It is a shortcut with a price, which is the only kind this game
    /// hands out.</para>
    /// </summary>
    DeepEntry,
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
    /// Forest run (~71), so the ladder spans roughly <b>thirteen thorough runs</b> end to end:
    /// 0.2 / 0.4 / 1.1 / 2.3 / 4.5 / 7.9 / 12.7.
    ///
    /// <para>The first cut shipped at 6/12/20/30/42 and a single first run cleared the whole
    /// ladder — including the hidden routes that are supposed to be the reward for learning the
    /// place. <c>DarkForestBalanceTests</c> now pins the ratio rather than the numbers, so
    /// retuning the per-node grants cannot silently trivialise this again.</para>
    ///
    /// <para><b>The five middle thresholds are exactly where D38 left them.</b> Phase 8 added a
    /// rung below (<see cref="RealmInsight.CommonResources"/>, reachable partway through a first
    /// expedition, so nobody's first run teaches them nothing) and a rung above
    /// (<see cref="RealmInsight.DeepEntry"/>) — bracketing the ladder rather than rescaling a
    /// balance pass that has already been made.</para>
    ///
    /// <para>Still a first pass on FEEL — nobody has played it. The ORDER is the design.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<RealmInsight, int> Required =
        new Dictionary<RealmInsight, int>
        {
            [RealmInsight.CommonResources] = 12,
            [RealmInsight.EnemyWeaknesses] = 30,
            [RealmInsight.Hazards] = 75,
            [RealmInsight.RichNodes] = 160,
            [RealmInsight.HiddenRoutes] = 320,
            [RealmInsight.ExtractionRoutes] = 560,
            [RealmInsight.DeepEntry] = 900,
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
