using Dungeons.Content;

namespace Dungeons.Modifiers;

/// <summary>
/// One contribution toward a modifier key, with its provenance and — since D-12 — when it
/// applies.
///
/// <para><see cref="Source"/> is not decoration. With Base, Prefix, Suffix, equipment,
/// statuses, professions and Realm effects all contributing, "why is my interval 87?" is a
/// question the Character Lab has to be able to answer, and it can only do that if every
/// contribution remembers where it came from.</para>
///
/// <para><see cref="Scope"/> is the local/global answer. Null means global — it applies
/// wherever the key is read. A scope narrows it to one situation, which is the difference
/// between a weapon's own <c>+20% physical damage</c> and a ring's.</para>
/// </summary>
public sealed record ModifierContribution(string Key, double Value, string Source, ModifierScope? Scope = null)
{
    public override string ToString() =>
        $"{Source}: {Key} {Value:+0.###;-0.###;0}{(Scope is null ? string.Empty : $" [{Scope}]")}";
}

/// <summary>
/// An accumulated set of modifier contributions, resolvable per key.
///
/// <para>Immutable-by-convention: build it up, then read it. Resolution honours the key's
/// <see cref="ModifierKind"/> and clamps, so callers never need to know whether a given key
/// is additive or multiplicative — they ask for a value and get one.</para>
///
/// <para>Since D-12 resolution also honours <see cref="ModifierScope"/>, which means it is no
/// longer a pure key lookup and a wrong <see cref="ModifierContext"/> would produce a wrong
/// number rather than an error. Three structural guards close that: a key declares its
/// <see cref="ModifierKeyDefinition.ScopedBy"/> dimension, <see cref="Add"/> rejects a scope
/// that disagrees with it, and <see cref="Resolve"/> throws when the context omits it.
/// <b>Do not add an overload that defaults the context</b> — the explicitness is the
/// mechanism.</para>
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

    /// <summary>
    /// Adds a contribution. Unknown keys throw — a mistyped key is a content bug, and silently
    /// ignoring it would make a modifier quietly do nothing. A scope that disagrees with the
    /// key's declared dimension throws for the same reason: it could only ever match nothing.
    /// </summary>
    public ModifierSet Add(string key, double value, string source, ModifierScope? scope = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_keys.TryGetById(key, out var definition))
            throw new KeyNotFoundException($"Unknown modifier key '{key}' from '{source}'.");

        if (scope is not null)
        {
            if (!definition.IsScoped)
            {
                throw new ArgumentException(
                    $"Modifier key '{key}' is global and takes no scope, but '{source}' scoped it to {scope}.",
                    nameof(scope));
            }

            if (!string.Equals(scope.Dimension, definition.ScopedBy, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Modifier key '{key}' is scoped by '{definition.ScopedBy}', but '{source}' scoped it to {scope}.",
                    nameof(scope));
            }
        }

        _contributions.Add(new ModifierContribution(key, value, source, scope));
        return this;
    }

    public ModifierSet AddRange(IEnumerable<ModifierContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        foreach (var contribution in contributions)
            Add(contribution.Key, contribution.Value, contribution.Source, contribution.Scope);
        return this;
    }

    /// <summary>Everything contributing to <paramref name="key"/>, for provenance display.</summary>
    public IReadOnlyList<ModifierContribution> For(string key) =>
        _contributions.Where(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// The contributions to <paramref name="key"/> that actually apply in
    /// <paramref name="context"/> — what <see cref="Resolve"/> sums, and what a trace should
    /// render. Showing each applied contribution <i>with its scope</i> is the visibility guard
    /// behind the three structural ones: a context bug that somehow survives all of them still
    /// shows up as a line that should be in the trace and isn't.
    /// </summary>
    public IReadOnlyList<ModifierContribution> Applicable(string key, ModifierContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return For(key).Where(c => context.Matches(c.Scope)).ToList();
    }

    /// <summary>True if any contribution has been made toward <paramref name="key"/>.</summary>
    public bool Has(string key) => For(key).Count > 0;

    /// <summary>
    /// The resolved value of <paramref name="key"/> in <paramref name="context"/>, starting from
    /// <paramref name="baseValue"/> (or the key's baseline when omitted).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The key is not registered.</exception>
    /// <exception cref="InvalidOperationException">
    /// The key declares a <see cref="ModifierKeyDefinition.ScopedBy"/> dimension the context does
    /// not supply. It does <b>not</b> fall back to the unscoped subtotal and it does not return
    /// the baseline — either would be a silently wrong number, which is the worst failure shape
    /// in the package (docs/effect-foundation.md §4.2.2).
    /// </exception>
    public double Resolve(string key, ModifierContext context, double? baseValue = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_keys.TryGetById(key, out var definition))
            throw new KeyNotFoundException($"Unknown modifier key '{key}'.");

        if (definition.IsScoped && !context.Has(definition.ScopedBy))
        {
            throw new InvalidOperationException(
                $"Modifier key '{key}' is scoped by '{definition.ScopedBy}', but the context supplies " +
                $"{context}. Resolving it here would return a number that looks right and isn't — " +
                $"pass the {definition.ScopedBy} explicitly.");
        }

        var contributions = Applicable(key, context);
        var value = baseValue ?? definition.Baseline;

        value = definition.Kind switch
        {
            ModifierKind.Additive => value + contributions.Sum(c => c.Value),
            ModifierKind.Multiplicative => contributions.Aggregate(value, (acc, c) => acc * c.Value),
            ModifierKind.Flag => contributions.Any(c => c.Value != 0.0) ? 1.0 : value,

            // 1 − Π(1 − x), with the base value as one more term so an inherent 5% avoidance
            // combines with granted avoidance the same way granted sources combine with each other.
            ModifierKind.Diminishing =>
                1.0 - contributions.Aggregate(1.0 - value, (acc, c) => acc * (1.0 - c.Value)),

            ModifierKind.HighestOnly =>
                contributions.Count == 0 ? value : Math.Max(value, contributions.Max(c => c.Value)),

            _ => value,
        };

        if (definition.Min is { } min)
            value = Math.Max(min, value);
        if (definition.Max is { } max)
            value = Math.Min(max, value);

        return value;
    }

    /// <summary>Convenience for <see cref="ModifierKind.Flag"/> keys.</summary>
    public bool IsSet(string key, ModifierContext context) => Resolve(key, context) != 0.0;
}
