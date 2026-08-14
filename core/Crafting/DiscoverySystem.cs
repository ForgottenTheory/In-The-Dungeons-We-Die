namespace Dungeons.Crafting;

/// <summary>
/// Persistent record of what the player has discovered (crafting interactions,
/// material combinations). Discovery turns game knowledge into progression
/// (docs/crafting.md §6–7, docs/progression.md §5). Save persistence is wired in a
/// later milestone; for now it holds the session's discoveries.
/// </summary>
public sealed class DiscoverySystem
{
    private readonly HashSet<string> _discovered = new(StringComparer.Ordinal);

    public DiscoverySystem(IEnumerable<string>? known = null)
    {
        if (known is not null)
            _discovered.UnionWith(known);
    }

    /// <summary>Raised the first time a given id is discovered.</summary>
    public event Action<string>? Discovered;

    public bool IsDiscovered(string id) => _discovered.Contains(id);

    public IReadOnlyCollection<string> All => _discovered;

    public int Count => _discovered.Count;

    /// <summary>Adds discoveries without raising events (used when loading a save).</summary>
    public void Restore(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        foreach (var id in ids)
            _discovered.Add(id);
    }

    /// <summary>Records a discovery. Returns true only if it was not already known.</summary>
    public bool Record(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Discovery id is null or empty.", nameof(id));
        if (!_discovered.Add(id))
            return false;

        Discovered?.Invoke(id);
        return true;
    }
}
