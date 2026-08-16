namespace Dungeons.Modifiers;

/// <summary>
/// The closed set of dimensions a modifier contribution may be scoped by
/// (docs/effect-foundation.md §4.2.1).
///
/// <para>Closed on purpose, like every other vocabulary here (D16). An open set would let
/// content invent a dimension nothing ever supplies, and a scope nothing supplies is a modifier
/// that silently never applies — which is the failure shape D-12 exists to close, arriving by a
/// different door.</para>
/// </summary>
public static class ScopeDimensions
{
    /// <summary>Damage lane — <c>physical</c>, <c>heat</c>, <c>charge</c>.</summary>
    public const string Lane = "lane";

    /// <summary>Damage aspect — the flavour riding a lane.</summary>
    public const string Aspect = "aspect";

    /// <summary>Material essence, for affixes that care what a thing is made of.</summary>
    public const string Essence = "essence";

    /// <summary>Profession id — the Melvor per-skill case (<c>−12% interval for Fishing</c>).</summary>
    public const string Profession = "profession";

    /// <summary>A tag on the move being used — <c>melee</c>, <c>spell</c>, <c>heavy</c>.</summary>
    public const string MoveTag = "move_tag";

    /// <summary>Stance or form the actor is in.</summary>
    public const string Form = "form";

    /// <summary>Which item the modifier belongs to — the PoE local-vs-global distinction.
    /// <c>item:self</c> is a weapon's own "+20% physical damage"; no scope is the ring's.</summary>
    public const string Item = "item";

    /// <summary>A status the target or self is under.</summary>
    public const string Status = "status";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Lane, Aspect, Essence, Profession, MoveTag, Form, Item, Status,
        };

    public static bool IsKnown(string dimension) => All.Contains(dimension);

    /// <summary>The canonical spelling, so equality and lookup never hinge on casing.</summary>
    internal static string Normalise(string value) => value.Trim().ToLowerInvariant();
}

/// <summary>
/// When a contribution applies — one dimension, one value (docs/effect-foundation.md §4.2).
///
/// <para>A contribution carries <b>at most one</b> scope. A modifier needing two dimensions
/// ("+8 damage to Melee attacks with Swords") is authored as two contributions from the same
/// source, both of which must match. That keeps the predicate a tag comparison rather than a
/// query language, and a query language in content is how the modifier system stops being
/// data.</para>
/// </summary>
public sealed record ModifierScope
{
    public ModifierScope(string dimension, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!ScopeDimensions.IsKnown(dimension))
        {
            throw new ArgumentException(
                $"'{dimension}' is not a modifier scope dimension. Valid: {string.Join(", ", ScopeDimensions.All)}.",
                nameof(dimension));
        }

        Dimension = ScopeDimensions.Normalise(dimension);
        Value = ScopeDimensions.Normalise(value);
    }

    public string Dimension { get; }

    public string Value { get; }

    public override string ToString() => $"{Dimension}:{Value}";
}

/// <summary>
/// The situation a modifier is being resolved in — which profession is acting, which lane the
/// hit is in, which move is swinging.
///
/// <para><see cref="ModifierSet.Resolve"/> takes one of these and it is <b>never</b> defaulted.
/// D-12's whole risk is that resolution stops being a pure key lookup, so a wrong context
/// produces a wrong <i>number</i> rather than an error — worse than a missing feature, because
/// nothing surfaces it. A required parameter stops a context being forgotten;
/// <see cref="ModifierKeyDefinition.ScopedBy"/> plus the throw in <c>Resolve</c> stops a wrong
/// one passing quietly. Callers that genuinely have no situation pass <see cref="None"/>, which
/// is greppable in a way an omitted argument is not.</para>
/// </summary>
public sealed class ModifierContext
{
    /// <summary>No situation at all. Valid for global keys; throws for anything
    /// <see cref="ModifierKeyDefinition.ScopedBy"/> names.</summary>
    public static readonly ModifierContext None = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private readonly Dictionary<string, string> _dimensions;

    private ModifierContext(Dictionary<string, string> dimensions) => _dimensions = dimensions;

    /// <summary>A context supplying a single dimension.</summary>
    public static ModifierContext For(string dimension, string value) => None.With(dimension, value);

    /// <summary>This context plus one dimension. Immutable — returns a new context.</summary>
    public ModifierContext With(string dimension, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!ScopeDimensions.IsKnown(dimension))
        {
            throw new ArgumentException(
                $"'{dimension}' is not a modifier scope dimension. Valid: {string.Join(", ", ScopeDimensions.All)}.",
                nameof(dimension));
        }

        var copy = new Dictionary<string, string>(_dimensions, StringComparer.OrdinalIgnoreCase)
        {
            [ScopeDimensions.Normalise(dimension)] = ScopeDimensions.Normalise(value),
        };

        return new ModifierContext(copy);
    }

    public bool Has(string dimension) => _dimensions.ContainsKey(dimension);

    /// <summary>The value supplied for <paramref name="dimension"/>, or null.</summary>
    public string? Value(string dimension) =>
        _dimensions.TryGetValue(dimension, out var value) ? value : null;

    /// <summary>
    /// Whether a contribution with this scope applies here. An unscoped contribution
    /// (<paramref name="scope"/> null) always applies — that is the global half of D-12.
    /// </summary>
    public bool Matches(ModifierScope? scope) =>
        scope is null
        || (_dimensions.TryGetValue(scope.Dimension, out var value)
            && string.Equals(value, scope.Value, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyDictionary<string, string> Dimensions => _dimensions;

    public override string ToString() =>
        _dimensions.Count == 0
            ? "(no scope)"
            : string.Join(" ", _dimensions.OrderBy(d => d.Key, StringComparer.Ordinal).Select(d => $"{d.Key}:{d.Value}"));
}
