namespace Dungeons.Rules;

/// <summary>
/// Proc-safety limits (docs/effect-foundation.md §6).
///
/// <para>The brief's fear, stated exactly: <i>thorns → counts as hit → triggers Shock → Shock
/// triggers retaliation → retaliation triggers thorns → game achieves nuclear fusion.</i></para>
/// </summary>
public static class ProcSafety
{
    /// <summary>
    /// A proc may proc once more, and no further. Depth 1 was rejected as breaking every
    /// two-step combination in the catalog (an affix-applied status could trigger nothing);
    /// depth 3 as multiplying a combination surface nobody could model across ~250 affixes.
    /// </summary>
    public const int MaxDepth = 2;

    /// <summary>
    /// The ceiling an <b>Anomalous</b> affix — obtainable only from Overreach — may raise its own
    /// chain to. Exactly one more, never unbounded: the recursion safety valve is also the
    /// top-end reward of the crafting casino, and even the casino has a floor.
    /// </summary>
    public const int AnomalousMaxDepth = 3;

    /// <summary>
    /// Hard fuse. Exceeding it aborts the chain and is a bug, not a balance problem — shipped
    /// content must never approach it, and a test asserts as much.
    /// </summary>
    public const int MaxEffectsPerChain = 64;
}

/// <summary>
/// The identity of one causal chain, carried by every effect it fires
/// (docs/effect-foundation.md §6.1).
///
/// <para>Depth is the backstop, not the fix. The rules that actually break the fusion loop are
/// <see cref="TriggerRule.Proc"/>'s once-per-chain, retaliation damage carrying
/// <c>CanTrigger = false</c>, and ailment ticks never raising a hit event. Depth bounds the
/// chains that <i>don't</i> repeat a rule.</para>
/// </summary>
/// <param name="ChainId">Unique per originating action. Once-per-chain bookkeeping keys on it.</param>
/// <param name="OriginSource">Who started the chain — not necessarily who fired this effect.</param>
/// <param name="ImmediateSource">Whose rule fired this particular effect.</param>
/// <param name="Depth">0 for a real action, 1 for a proc, 2 for a proc's proc.</param>
/// <param name="OriginTags">Markers that survive the whole chain, e.g. <c>thorns</c>.</param>
public sealed record EffectContext(
    string ChainId,
    string OriginSource,
    string ImmediateSource,
    int Depth,
    IReadOnlySet<string> OriginTags)
{
    private static readonly HashSet<string> NoTags = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A fresh chain, for a real action rather than a proc.</summary>
    public static EffectContext Origin(string chainId, string source) =>
        new(chainId, source, source, 0, NoTags);

    /// <summary>The context a rule owned by <paramref name="source"/> fires its effects in.</summary>
    public EffectContext Next(string source, IEnumerable<string>? addedTags = null) =>
        this with
        {
            ImmediateSource = source,
            Depth = Depth + 1,
            OriginTags = addedTags is null
                ? OriginTags
                : new HashSet<string>(OriginTags.Concat(addedTags), StringComparer.OrdinalIgnoreCase),
        };

    public bool HasOriginTag(string tag) => OriginTags.Contains(tag);
}

/// <summary>
/// Per-rule proc limits. Defaults are the safe ones — content opts <i>into</i> risk, never out of
/// safety by omission.
/// </summary>
public sealed class ProcRules
{
    /// <summary>
    /// Fires at most once per causal chain. This is what kills A→B→A ping-pong even inside the
    /// depth budget, and the validator turns it on automatically for any rule whose effects can
    /// raise the event it listens for.
    /// </summary>
    public bool OncePerChain { get; init; } = true;

    /// <summary>Cooldown scoped to one target rather than to the rule as a whole.</summary>
    public int IcdTicks { get; init; }

    /// <summary>
    /// Raises this rule's own chain ceiling. <b>Anomalous affixes only</b> — the validator
    /// rejects it anywhere else, so "may recurse one level further" stays a thing you win from
    /// Overreach rather than a field anyone can type.
    /// </summary>
    public int MaxDepth { get; init; } = ProcSafety.MaxDepth;
}
