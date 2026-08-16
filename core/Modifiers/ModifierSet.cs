using Dungeons.Content;

namespace Dungeons.Modifiers;

/// <summary>
/// One contribution toward a modifier key, with its provenance.
///
/// <para><see cref="Source"/> is not decoration. With Base, Prefix, Suffix, equipment,
/// statuses, professions and Realm effects all contributing, "why is my interval 87?" is a
/// question the Character Lab has to be able to answer, and it can only do that if every
/// contribution remembers where it came from.</para>
/// </summary>
public sealed record ModifierContribution(string Key, double Value, string Source)
{
    public override string ToString() => $"{Source}: {Key} {Value:+0.###;-0.###;0}";
}

/// <summary>
/// An accumulated set of modifier contributions, resolvable per key.
///
/// <para>Immutable-by-convention: build it up, then read it. Resolution honours the key's
/// <see cref="ModifierKind"/> and clamps, so callers never need to know whether a given key
/// is additive or multiplicative — they ask for a value and get one.</para>
/// </summary>
public sealed class ModifierSet
{
    private readonly List<ModifierContribution> _contributions = new();
    private readonly DataStore<ModifierKeyDefinition> _keys;

    public ModifierSet(DataStore<ModifierKeyDefinition> keys)
    {
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
    }

    public IReadOnlyList<ModifierContribution> Contributions => _contributions;

    /// <summary>Adds a contribution. Unknown keys throw — a mistyped key is a content bug, and
    /// silently ignoring it would make a modifier quietly do nothing.</summary>
    public ModifierSet Add(string key, double value, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_keys.Contains(key))
            throw new KeyNotFoundException($"Unknown modifier key '{key}' from '{source}'.");

        _contributions.Add(new ModifierContribution(key, value, source));
        return this;
    }

    public ModifierSet AddRange(IEnumerable<ModifierContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        foreach (var contribution in contributions)
            Add(contribution.Key, contribution.Value, contribution.Source);
        return this;
    }

    /// <summary>Everything contributing to <paramref name="key"/>, for provenance display.</summary>
    public IReadOnlyList<ModifierContribution> For(string key) =>
        _contributions.Where(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>True if any contribution has been made toward <paramref name="key"/>.</summary>
    public bool Has(string key) => For(key).Count > 0;

    /// <summary>
    /// The resolved value of <paramref name="key"/>, starting from <paramref name="baseValue"/>
    /// (or the key's baseline when omitted).
    /// </summary>
    public double Resolve(string key, double? baseValue = null)
    {
        if (!_keys.TryGetById(key, out var definition))
            throw new KeyNotFoundException($"Unknown modifier key '{key}'.");

        var contributions = For(key);
        var value = baseValue ?? definition.Baseline;

        value = definition.Kind switch
        {
            ModifierKind.Additive => value + contributions.Sum(c => c.Value),
            ModifierKind.Multiplicative => contributions.Aggregate(value, (acc, c) => acc * c.Value),
            ModifierKind.Flag => contributions.Any(c => c.Value != 0.0) ? 1.0 : value,
            _ => value,
        };

        if (definition.Min is { } min)
            value = Math.Max(min, value);
        if (definition.Max is { } max)
            value = Math.Min(max, value);

        return value;
    }

    /// <summary>Convenience for <see cref="ModifierKind.Flag"/> keys.</summary>
    public bool IsSet(string key) => Resolve(key) != 0.0;
}
