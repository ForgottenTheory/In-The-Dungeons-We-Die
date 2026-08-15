namespace Dungeons.Items;

/// <summary>
/// An immutable, case-insensitive map of item property name → value. Represents both
/// a definition's intrinsic (base) properties and an instance's derived properties.
/// Zero-valued entries are dropped so "absent" and "zero" are the same. String-keyed
/// so new properties never require code changes (docs/itemization.md §2).
/// </summary>
public sealed class PropertySet
{
    public static readonly PropertySet Empty = new(new Dictionary<string, double>());

    private readonly Dictionary<string, double> _values;

    public PropertySet(IReadOnlyDictionary<string, double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            if (pair.Value != 0.0)
                _values[pair.Key] = pair.Value;
        }
    }

    /// <summary>Value for <paramref name="key"/>, or 0 if absent.</summary>
    public double Get(string key) => _values.TryGetValue(key, out var v) ? v : 0.0;

    public bool Has(string key) => _values.ContainsKey(key);

    public int Count => _values.Count;

    public IReadOnlyCollection<string> Keys => _values.Keys;

    public IReadOnlyDictionary<string, double> AsDictionary() => _values;

    /// <summary>Returns a copy with <paramref name="key"/> set (or removed if 0).</summary>
    public PropertySet With(string key, double value)
    {
        var copy = new Dictionary<string, double>(_values, StringComparer.OrdinalIgnoreCase);
        if (value == 0.0)
            copy.Remove(key);
        else
            copy[key] = value;
        return new PropertySet(copy);
    }

    /// <summary>
    /// Combines two sets key-by-key using <paramref name="combine"/> over the union of
    /// keys (missing side reads as 0). The building block for crafting derivation.
    /// </summary>
    public PropertySet Combine(PropertySet other, Func<double, double, double> combine)
    {
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(combine);
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in _values.Keys.Union(other._values.Keys, StringComparer.OrdinalIgnoreCase))
            result[key] = combine(Get(key), other.Get(key));
        return new PropertySet(result);
    }

    public static PropertySet FromValues(IReadOnlyDictionary<string, double> values) => new(values);
}
