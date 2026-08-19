using System.Text.Json.Serialization;

namespace Dungeons.Realms;

/// <summary>
/// Kinds of spatial location in a Realm (docs/realms.md §11).
///
/// <para>These names appear only in <c>realms/</c> content — a run is transient and never
/// persisted — so adding a kind costs a validator rule and a handler, not a save migration.</para>
/// </summary>
public enum RealmLocationType
{
    Entrance,
    Travel,
    Combat,
    Gather,
    Event,
    Descent,
    Extraction,

    /// <summary>Somewhere to stop. Restores a fraction of the party's pools, <b>once per run</b>,
    /// so the decision it creates is "spend the safety now or carry it deeper".</summary>
    Camp,

    /// <summary>A one-time boon, paid for in Realm Knowledge rather than coin: standing at one
    /// teaches you about the place.</summary>
    Shrine,

    /// <summary>The first gold sink in the game. Sells <b>inputs and knowledge</b> — never
    /// finished equipment, because the whole identity is that you craft your own (D28).</summary>
    Merchant,

    /// <summary>Costs health to cross. Knowing it is there is what turns it from an ambush into
    /// a route choice, which is what Realm Knowledge buys.</summary>
    Hazard,
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

    /// <summary>
    /// A node the party cannot see, let alone travel to, until Realm Knowledge reveals it
    /// (<see cref="RealmInsight.HiddenRoutes"/>).
    ///
    /// <para>This is the payoff that makes Knowledge worth having: the shortcut, the cache and
    /// the side route were in the graph the whole time, and the twentieth run walks a different
    /// map from the first.</para>
    /// </summary>
    public bool Hidden { get; init; }

    /// <summary>
    /// Whether a party carrying this much Realm Knowledge can see this node <b>at all</b>.
    ///
    /// <para>The single place that rule lives. Three readers need it and they must never
    /// disagree: the run gates travel on it (<see cref="RealmRun.IsReachable"/>), the map draws
    /// with it, and the pre-run briefing redacts with it. A briefing that leaked a node the run
    /// then refuses to walk to would be the worst of both.</para>
    /// </summary>
    public bool IsVisibleAt(int knowledge) =>
        !Hidden || RealmKnowledgeLevels.Reveals(knowledge, RealmInsight.HiddenRoutes);

    /// <summary>
    /// Hazard nodes: health taken on entering. Charged once — the ground is crossed, not fought.
    /// </summary>
    [JsonPropertyName("hazard_damage")]
    public int HazardDamage { get; init; }

    /// <summary>Camp nodes: the fraction of each pool restored, 0–1.</summary>
    [JsonPropertyName("restore_fraction")]
    public double RestoreFraction { get; init; }

    /// <summary>Merchant nodes: the coin price of the goods behind <see cref="LootTableId"/>.</summary>
    public int Cost { get; init; }
}
