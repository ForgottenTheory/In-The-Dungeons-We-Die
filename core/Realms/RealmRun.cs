using System.Linq;
using Dungeons.Items;

namespace Dungeons.Realms;

/// <summary>
/// Authoritative state for one expedition into a Realm (docs/architecture.md §24):
/// where the party is, how deep, and what has been seen or cleared. Travel is
/// validated against the location graph; descending advances depth through a Descent
/// node. This aggregate owns spatial/run state only — combat, gathering and loot are
/// coordinated by the application layer.
/// </summary>
public sealed class RealmRun
{
    private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
    private readonly HashSet<string> _cleared = new(StringComparer.Ordinal);

    /// <param name="startingDepth">Where the expedition begins. Deeper than 1 only once
    /// <see cref="RealmInsight.DeepEntry"/> is earned — see <see cref="DeepestReachableEntry"/>,
    /// which is the single place that rule lives. Clamped rather than rejected, so a stale
    /// choice on the preparation screen starts a shallower run instead of no run at all.</param>
    public RealmRun(RealmDefinition realm, int tier, int knowledge = 0, int startingDepth = 1)
    {
        Realm = realm ?? throw new ArgumentNullException(nameof(realm));
        Tier = tier;
        Knowledge = knowledge;
        CurrentDepth = Math.Clamp(startingDepth, 1, DeepestReachableEntry(realm, knowledge));

        var entrance = realm.EntranceForDepth(CurrentDepth)
            ?? throw new InvalidOperationException($"Realm '{realm.Id}' has no depth-{CurrentDepth} entrance.");
        CurrentLocationId = entrance.Id;
        _visited.Add(entrance.Id);
        Active = true;
    }

    /// <summary>
    /// The deepest entrance a party carrying this much Knowledge may start at.
    ///
    /// <para>1 until <see cref="RealmInsight.DeepEntry"/> is earned, and then every depth the
    /// realm actually has an entrance for. Static because the preparation screen has to ask this
    /// <em>before</em> a run exists — which is the whole point of the insight.</para>
    /// </summary>
    public static int DeepestReachableEntry(RealmDefinition realm, int knowledge)
    {
        ArgumentNullException.ThrowIfNull(realm);
        if (!RealmKnowledgeLevels.Reveals(knowledge, RealmInsight.DeepEntry))
            return 1;

        var deepest = 1;
        while (realm.EntranceForDepth(deepest + 1) is not null)
            deepest++;
        return deepest;
    }

    public RealmDefinition Realm { get; }
    public int Tier { get; }
    public int CurrentDepth { get; private set; }
    public string CurrentLocationId { get; private set; }
    public bool Active { get; private set; }

    /// <summary>Loot acquired this run — unsecured until extraction (docs/architecture.md §23).</summary>
    public Inventory RunInventory { get; } = new();

    public IReadOnlySet<string> Visited => _visited;
    public IReadOnlySet<string> Cleared => _cleared;

    /// <summary>
    /// The party's Realm Knowledge for this Realm. Held on the run rather than looked up,
    /// because it changes <em>during</em> a run — the shortcut you earn on the way down is one
    /// you can take on the way back.
    /// </summary>
    public int Knowledge { get; set; }

    public RealmLocationDefinition CurrentLocation => Realm.GetLocation(CurrentLocationId);

    /// <summary>True once Realm Knowledge has reached this insight's threshold.</summary>
    public bool Knows(RealmInsight insight) => RealmKnowledgeLevels.Reveals(Knowledge, insight);

    /// <summary>
    /// A hidden node is invisible and untravelable until Knowledge reveals the routes — the rule
    /// itself lives on <see cref="RealmLocationDefinition.IsVisibleAt"/>, so "can I see it" and
    /// "can I walk there" can never disagree.
    /// </summary>
    public bool IsReachable(RealmLocationDefinition location) => location.IsVisibleAt(Knowledge);

    /// <summary>Adjacent nodes at the current depth that the party can travel to.</summary>
    public IReadOnlyList<RealmLocationDefinition> Destinations() =>
        CurrentLocation.Connections
            .Where(Realm.HasLocation)
            .Select(Realm.GetLocation)
            .Where(l => l.Depth == CurrentDepth && IsReachable(l))
            .ToList();

    public bool CanTravelTo(string locationId) =>
        Active
        && CurrentLocation.Connections.Contains(locationId)
        && Realm.HasLocation(locationId)
        && Realm.GetLocation(locationId).Depth == CurrentDepth
        && IsReachable(Realm.GetLocation(locationId));

    public bool TravelTo(string locationId)
    {
        if (!CanTravelTo(locationId))
            return false;
        CurrentLocationId = locationId;
        _visited.Add(locationId);
        return true;
    }

    /// <summary>
    /// Cleared means "this node has given what it has to give": the fight is won, the chest is
    /// open, the camp is slept in, the shrine is spent, the hazard is already paid for.
    ///
    /// <para>One set for all of them on purpose — every one of those is the same question at the
    /// application layer ("is there anything left here?"), and splitting it into five flags would
    /// mean five chances to check the wrong one.</para>
    /// </summary>
    public bool IsCleared(string locationId) => _cleared.Contains(locationId);

    public void MarkCleared(string locationId) => _cleared.Add(locationId);

    /// <summary>
    /// Every node at this depth the party has already found, with the ones they still cannot
    /// see filtered out. This is what a map screen draws.
    /// </summary>
    public IReadOnlyList<RealmLocationDefinition> KnownAtCurrentDepth() =>
        Realm.Locations
            .Where(l => l.Depth == CurrentDepth && IsReachable(l))
            .ToList();

    /// <summary>
    /// Where the nearest way out is, for a party deciding whether to push on — but only once
    /// they have learned the routes. Before that the answer is "you do not know", which is the
    /// whole reason <see cref="RealmInsight.ExtractionRoutes"/> is worth earning.
    /// </summary>
    public IReadOnlyList<RealmLocationDefinition> KnownExtractions() =>
        Knows(RealmInsight.ExtractionRoutes)
            ? Realm.Locations
                .Where(l => l.Depth == CurrentDepth && IsReachable(l))
                .Where(l => l.Type is RealmLocationType.Extraction or RealmLocationType.Descent)
                .ToList()
            : Array.Empty<RealmLocationDefinition>();

    /// <summary>True when standing on a Descent node with a deeper level to reach.</summary>
    public bool CanDescend =>
        Active
        && CurrentLocation.Type == RealmLocationType.Descent
        && Realm.EntranceForDepth(CurrentDepth + 1) is not null;

    public bool Descend()
    {
        if (!CanDescend)
            return false;
        var entrance = Realm.EntranceForDepth(CurrentDepth + 1)!;
        CurrentDepth++;
        CurrentLocationId = entrance.Id;
        _visited.Add(entrance.Id);
        return true;
    }

    /// <summary>Extraction is available on Descent or Extraction nodes.</summary>
    public bool CanExtract =>
        Active && CurrentLocation.Type is RealmLocationType.Descent or RealmLocationType.Extraction;

    public void End() => Active = false;
}
