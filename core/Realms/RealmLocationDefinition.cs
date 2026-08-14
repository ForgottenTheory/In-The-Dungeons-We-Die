namespace Dungeons.Realms;

/// <summary>Kinds of spatial location in a Realm (docs/realms.md §11). MVP subset.</summary>
public enum RealmLocationType
{
    Entrance,
    Travel,
    Combat,
    Gather,
    Event,
    Descent,
    Extraction,
}

/// <summary>
/// A single spatial node in a Realm's location graph. Connections list the adjacent
/// nodes at the same depth; crossing to the next depth happens through a Descent node,
/// not a normal connection. Content refs point at the systems a node drives.
/// </summary>
public sealed class RealmLocationDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public RealmLocationType Type { get; init; } = RealmLocationType.Travel;
    public int Depth { get; init; } = 1;
    public IReadOnlyList<string> Connections { get; init; } = Array.Empty<string>();

    /// <summary>Combat nodes: the enemy actor to fight.</summary>
    public string? ActorId { get; init; }

    /// <summary>Gather nodes: the profession action to perform.</summary>
    public string? ProfessionActionId { get; init; }

    /// <summary>Event nodes: flavour text and an optional reward.</summary>
    public string? EventText { get; init; }
    public string? RewardItemId { get; init; }
    public int RewardQuantity { get; init; } = 1;
}
