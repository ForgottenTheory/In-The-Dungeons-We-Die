namespace Dungeons.Items;

/// <summary>
/// Hands out unique, monotonically increasing item-instance ids. Deterministic (a
/// simple counter, not RNG) so runs and tests reproduce, and seedable so ids do not
/// collide after loading a save.
/// </summary>
public sealed class InstanceIdSource
{
    private long _next;

    public InstanceIdSource(long start = 1) => _next = start;

    public long Next() => _next++;

    /// <summary>The next id that will be issued — persist this in the save.</summary>
    public long Peek() => _next;

    /// <summary>Ensures future ids are at least <paramref name="value"/> (used on load).</summary>
    public void EnsureAtLeast(long value)
    {
        if (value > _next)
            _next = value;
    }
}
