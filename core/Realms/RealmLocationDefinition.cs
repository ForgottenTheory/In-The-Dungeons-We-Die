using System.Text.Json.Serialization;

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

    /// <summary>Event nodes: flavour text.</summary>
    public string? EventText { get; init; }

    /// <summary>
    /// What this node pays out, on top of whatever its primary action already produces
    /// (docs/realms.md). On a Gather node this is the Realm's own layer over the profession
    /// action — the strange finds that a safe Hideout ladder never surfaces. On an Event node
    /// it is the chest.
    ///
    /// <para>A Gather node's table is rolled only when the profession attempt actually lands,
    /// so standing on a node is never a free faucet.</para>
    /// </summary>
    [JsonPropertyName("loot_table")]
    public string? LootTableId { get; init; }
}
