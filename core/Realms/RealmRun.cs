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

    public RealmRun(RealmDefinition realm, int tier)
    {
        Realm = realm ?? throw new ArgumentNullException(nameof(realm));
        Tier = tier;
        CurrentDepth = 1;

        var entrance = realm.EntranceForDepth(1)
            ?? throw new InvalidOperationException($"Realm '{realm.Id}' has no depth-1 entrance.");
        CurrentLocationId = entrance.Id;
        _visited.Add(entrance.Id);
        Active = true;
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

    public RealmLocationDefinition CurrentLocation => Realm.GetLocation(CurrentLocationId);

    /// <summary>Adjacent nodes at the current depth that the party can travel to.</summary>
    public IReadOnlyList<RealmLocationDefinition> Destinations() =>
        CurrentLocation.Connections
            .Where(Realm.HasLocation)
            .Select(Realm.GetLocation)
            .Where(l => l.Depth == CurrentDepth)
            .ToList();

    public bool CanTravelTo(string locationId) =>
        Active
        && CurrentLocation.Connections.Contains(locationId)
        && Realm.HasLocation(locationId)
        && Realm.GetLocation(locationId).Depth == CurrentDepth;

    public bool TravelTo(string locationId)
    {
        if (!CanTravelTo(locationId))
            return false;
        CurrentLocationId = locationId;
        _visited.Add(locationId);
        return true;
    }

    public bool IsCleared(string locationId) => _cleared.Contains(locationId);

    public void MarkCleared(string locationId) => _cleared.Add(locationId);

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
