namespace Dungeons.Combat;

/// <summary>
/// The moves a character has learned from technique items (M2′ acquisition v1). Persisted as
/// ids; learn order is preserved so moveset composition — and therefore conflict reporting —
/// stays deterministic across save round-trips.
/// </summary>
public sealed class LearnedMoves
{
    private readonly List<string> _moves = new();

    public IReadOnlyList<string> All => _moves;

    public bool Knows(string moveId) => _moves.Contains(moveId, StringComparer.Ordinal);

    /// <summary>False if the move is already known — the caller keeps the unconsumed item.</summary>
    public bool Learn(string moveId)
    {
        if (string.IsNullOrWhiteSpace(moveId) || Knows(moveId))
            return false;
        _moves.Add(moveId);
        return true;
    }

    public void Restore(IEnumerable<string> moveIds)
    {
        _moves.Clear();
        foreach (var id in moveIds)
            Learn(id);
    }
}
