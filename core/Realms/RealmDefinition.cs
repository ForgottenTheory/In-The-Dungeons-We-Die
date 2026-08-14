using System.Linq;
using Dungeons.Content;

namespace Dungeons.Realms;

/// <summary>
/// Data-driven Realm definition including its spatial location graph. The MVP uses a
/// fixed authored map; production may generate it (docs/vertical-slice.md §10,
/// docs/json-schema.md §17).
/// </summary>
public sealed class RealmDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<int> SupportedTiers { get; init; } = new[] { 1 };
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RealmLocationDefinition> Locations { get; init; } = Array.Empty<RealmLocationDefinition>();

    public RealmLocationDefinition GetLocation(string id) =>
        Locations.FirstOrDefault(l => l.Id == id)
        ?? throw new KeyNotFoundException($"Realm '{Id}' has no location '{id}'.");

    public bool HasLocation(string id) => Locations.Any(l => l.Id == id);

    /// <summary>The entrance node for a given depth, or null if that depth doesn't exist.</summary>
    public RealmLocationDefinition? EntranceForDepth(int depth) =>
        Locations.FirstOrDefault(l => l.Type == RealmLocationType.Entrance && l.Depth == depth);

    public int MaxDepth => Locations.Count == 0 ? 0 : Locations.Max(l => l.Depth);
}
