namespace Dungeons.Characters;

/// <summary>
/// A current/maximum resource pool (Health, Mana or Stamina). Current is always
/// clamped to [0, Max]. This type never regenerates on its own — Health recovery
/// in particular must come from explicit systems (docs/GDD.md §5.4).
/// </summary>
public sealed class ResourcePool
{
    private int _current;
    private int _max;

    public ResourcePool(ResourceType type, int max, int? current = null)
    {
        Type = type;
        _max = Math.Max(0, max);
        _current = current is null ? _max : Math.Clamp(current.Value, 0, _max);
    }

    public ResourceType Type { get; }

    public int Max
    {
        get => _max;
        set
        {
            _max = Math.Max(0, value);
            if (_current > _max)
                _current = _max;
        }
    }

    public int Current => _current;

    public bool IsDepleted => _current <= 0;

    /// <summary>Current value as a fraction of max in [0, 1]. Zero-max pools read as 0.</summary>
    public double Fraction => _max <= 0 ? 0.0 : (double)_current / _max;

    /// <summary>Reduces current by <paramref name="amount"/> (clamped at 0). Returns amount actually removed.</summary>
    public int Reduce(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be non-negative.");
        var removed = Math.Min(amount, _current);
        _current -= removed;
        return removed;
    }

    /// <summary>Increases current by <paramref name="amount"/> (clamped at Max). Returns amount actually added.</summary>
    public int Restore(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be non-negative.");
        var added = Math.Min(amount, _max - _current);
        _current += added;
        return added;
    }

    /// <summary>Sets current to Max.</summary>
    public void Fill() => _current = _max;
}
