using Dungeons.Actions;
using Dungeons.Content;
using Dungeons.Rules;

namespace Dungeons.Combat;

/// <summary>
/// A move after every matching modifier has been applied — what combat actually executes.
///
/// <para>Resolution is cached at build time, not recomputed per hit (docs/moves.md §3.3); the
/// exception is duration-limited <c>modifyMove</c> grants, which apply at execution because
/// "the next attack is empowered" cannot be pre-baked into a cache.</para>
/// </summary>
public sealed class ResolvedMove
{
    public required MoveDefinition Source { get; init; }

    public string Id => Source.Id;
    public string Name => Source.Name;
    public MoveKind Kind => Source.Kind;

    public required IReadOnlySet<string> Tags { get; init; }
    public required ActionTiming Timing { get; init; }
    public required IReadOnlyList<ActionCost> Costs { get; init; }
    public required IReadOnlyList<Packet> Packets { get; init; }
    public required IReadOnlyList<EffectSpec> Effects { get; init; }

    public IReadOnlyList<ConditionSpec> Requires => Source.Requires;
    public Targeting Targeting => Source.Targeting;

    public required int MaxTargets { get; init; }
    public required double StaggerPower { get; init; }
    public required bool Interruptible { get; init; }

    /// <summary>Extra targets a landed hit chains to, with damage falloff per jump.</summary>
    public required int ChainTargets { get; init; }

    public int CooldownTicks => Source.CooldownTicks;

    /// <summary>Who granted the move and every modifier that touched it — the Move Viewer's
    /// "why does my slash do that?" answer.</summary>
    public required IReadOnlyList<string> Provenance { get; init; }

    public bool HasTag(string tag) => Tags.Contains(tag);

    /// <summary>
    /// This move's current state as a definition, so the op interpreter can run again on top of
    /// it — how execution-time `modifyMove` grants stack on a cached resolution.
    /// </summary>
    public MoveDefinition Snapshot() => new()
    {
        Id = Source.Id,
        Name = Source.Name,
        Description = Source.Description,
        Kind = Source.Kind,
        Tags = Tags.ToList(),
        Timing = Timing,
        Costs = Costs,
        Requires = Source.Requires,
        Targeting = Source.Targeting,
        MaxTargets = MaxTargets,
        CooldownTicks = Source.CooldownTicks,
        Interruptible = Interruptible,
        Packets = Packets,
        StaggerPower = StaggerPower,
        Effects = Effects,
    };

    /// <summary>The `action:` tag's bare value (`attack`, `spell`…), for `CanAct` gating.</summary>
    public string ActionKind =>
        Tags.FirstOrDefault(t => t.StartsWith("action:", StringComparison.OrdinalIgnoreCase))?["action:".Length..]
        ?? "attack";
}

/// <summary>One grant with its provenance, ready for the builder.</summary>
public sealed record MoveGrant(MoveGrantSpec Spec, string Source);

/// <summary>One modifier with its provenance.</summary>
public sealed record MoveModifierGrant(MoveModifierDefinition Definition, string Source);

/// <summary>
/// Composes a moveset from grants and modifiers (docs/moves.md §3.3).
///
/// <para>Pure — same inputs, same moveset. Caching and invalidation are the caller's
/// (equipment change, build change, status apply/expire); a pure builder is what makes the
/// idempotence and order-independence guarantees testable at all.</para>
///
/// <para><b>Ops apply in the fixed <see cref="MoveOps.ApplicationOrder"/> regardless of source
/// order.</b> Within one op kind, sources apply in grant order; every op is either commutative
/// (scaling), or append-only (packets, effects, tags), so shuffling sources changes at most
/// list order, never semantics — and the tests assert exactly that.</para>
/// </summary>
public sealed class MovesetBuilder
{
    private readonly DataStore<MoveDefinition> _moves;

    public MovesetBuilder(DataStore<MoveDefinition> moves)
    {
        _moves = moves ?? throw new ArgumentNullException(nameof(moves));
    }

    /// <summary>Conflicts found while building (replacement of a missing move, unknown grant).
    /// Reported, never silently resolved.</summary>
    public IReadOnlyList<string> Build(
        IEnumerable<MoveGrant> grants,
        IEnumerable<MoveModifierGrant> modifiers,
        out IReadOnlyList<ResolvedMove> moveset)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(modifiers);

        var conflicts = new List<string>();
        var granted = new List<(MoveDefinition Move, string Source)>();

        foreach (var grant in grants)
        {
            if (!_moves.TryGetById(grant.Spec.Id, out var move))
            {
                conflicts.Add($"{grant.Source} grants unknown move '{grant.Spec.Id}'.");
                continue;
            }

            if (grant.Spec.Replaces is { } replaced)
            {
                var index = granted.FindIndex(g => string.Equals(g.Move.Id, replaced, StringComparison.Ordinal));
                if (index < 0)
                    conflicts.Add($"{grant.Source} replaces '{replaced}', which nothing granted.");
                else
                    granted.RemoveAt(index);
            }

            // The same move from two sources is one move — provenance keeps both names.
            if (granted.Any(g => g.Move.Id == move.Id))
            {
                conflicts.Add($"'{move.Id}' granted twice (again by {grant.Source}).");
                continue;
            }

            granted.Add((move, grant.Source));
        }

        var modifierList = modifiers.ToList();
        moveset = granted
            .Select(g => Resolve(g.Move, g.Source, modifierList))
            .ToList();

        return conflicts;
    }

    /// <summary>Applies every matching modifier to one move, in the fixed op order.</summary>
    public static ResolvedMove Resolve(
        MoveDefinition move, string grantSource, IReadOnlyList<MoveModifierGrant> modifiers)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(modifiers);

        var matching = modifiers.Where(m => m.Definition.Match.Matches(move)).ToList();

        var provenance = new List<string> { grantSource };
        provenance.AddRange(matching.Select(m => $"{m.Source} ({m.Definition.Id})"));

        // Every op from every matching modifier, then sorted into the canonical order. The sort
        // is stable, so within one op kind sources keep their grant order.
        var ops = matching
            .SelectMany(m => m.Definition.Ops)
            .OrderBy(op => IndexOf(op.Op))
            .ToList();

        return Apply(move, ops, provenance);
    }

    private static int IndexOf(string op)
    {
        for (var i = 0; i < MoveOps.ApplicationOrder.Count; i++)
        {
            if (string.Equals(MoveOps.ApplicationOrder[i], op, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return MoveOps.ApplicationOrder.Count; // unknown ops sort last; the validator rejects them
    }

    /// <summary>
    /// The op interpreter. Also used at execution time for duration-limited grants — which is
    /// why it takes a move rather than being folded into <see cref="Resolve"/>.
    /// </summary>
    public static ResolvedMove Apply(MoveDefinition move, IReadOnlyList<MoveOpSpec> ops, IReadOnlyList<string> provenance)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(ops);

        var tags = new HashSet<string>(move.Tags, StringComparer.OrdinalIgnoreCase);
        var packets = move.Packets.ToList();
        var effects = move.Effects.ToList();
        var costs = move.Costs.Select(c => new ActionCost { Resource = c.Resource, Amount = c.Amount }).ToList();
        var telegraph = (double)move.Timing.TelegraphTicks;
        var windup = (double)move.Timing.WindupTicks;
        var recovery = (double)move.Timing.RecoveryTicks;
        var maxTargets = move.MaxTargets;
        var chainTargets = 0;
        var stagger = move.StaggerPower;
        var interruptible = move.Interruptible;

        foreach (var op in ops)
        {
            switch (op.Op.ToLowerInvariant())
            {
                case "addpacket" when op.Packet is not null:
                    packets.Add(op.Packet);
                    break;

                case "scaledamage":
                    packets = packets
                        .Select(p => op.From is null || string.Equals(p.Lane, op.From, StringComparison.OrdinalIgnoreCase)
                            ? p.WithAmount(p.Amount * op.Value)
                            : p)
                        .ToList();
                    break;

                case "convert" when op.From is not null && op.To is not null:
                    packets = MoveLane(packets, op.From, op.To, op.Fraction, keepSource: true);
                    break;

                case "addasextra" when op.From is not null && op.To is not null:
                    packets = MoveLane(packets, op.From, op.To, op.Fraction, keepSource: false);
                    break;

                case "addtargets":
                    maxTargets += (int)op.Value;
                    break;

                case "addchain":
                    chainTargets += (int)op.Value;
                    break;

                case "scaletiming":
                    switch (op.Field.ToLowerInvariant())
                    {
                        case "telegraph": telegraph *= op.Value; break;
                        case "windup": windup *= op.Value; break;
                        case "recovery": recovery *= op.Value; break;
                    }
                    break;

                case "scalecost":
                    for (var i = 0; i < costs.Count; i++)
                    {
                        if (string.IsNullOrEmpty(op.Resource)
                            || string.Equals(costs[i].Resource, op.Resource, StringComparison.OrdinalIgnoreCase))
                        {
                            costs[i] = new ActionCost { Resource = costs[i].Resource, Amount = costs[i].Amount * op.Value };
                        }
                    }
                    break;

                case "addeffect" when op.Effect is not null:
                    effects.Add(op.Effect);
                    break;

                case "addtag" when !string.IsNullOrWhiteSpace(op.Tag):
                    tags.Add(op.Tag);
                    break;

                case "setflag":
                    if (string.Equals(op.Field, "uninterruptible", StringComparison.OrdinalIgnoreCase))
                        interruptible = false;
                    tags.Add("flag:" + op.Field.ToLowerInvariant());
                    break;
            }
        }

        return new ResolvedMove
        {
            Source = move,
            Tags = tags,
            Timing = new ActionTiming
            {
                TelegraphTicks = (int)Math.Round(telegraph, MidpointRounding.AwayFromZero),
                WindupTicks = (int)Math.Round(windup, MidpointRounding.AwayFromZero),
                RecoveryTicks = (int)Math.Round(recovery, MidpointRounding.AwayFromZero),
            },
            Costs = costs,
            Packets = packets,
            Effects = effects,
            MaxTargets = maxTargets,
            ChainTargets = chainTargets,
            StaggerPower = stagger,
            Interruptible = interruptible,
            Provenance = provenance,
        };
    }

    /// <summary>
    /// Moves (or duplicates) a fraction of a lane's damage into another lane. The one way
    /// damage changes lanes, and it always states a fraction (D-01) — so a reviewer can see at
    /// a glance whether an op adds damage or relabels it.
    /// </summary>
    private static List<Packet> MoveLane(List<Packet> packets, string from, string to, double fraction, bool keepSource)
    {
        var result = new List<Packet>();
        var moved = new List<Packet>();

        foreach (var packet in packets)
        {
            if (!string.Equals(packet.Lane, from, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(packet);
                continue;
            }

            var portion = packet.Amount * fraction;

            // keepSource=true is convert: the source loses the portion. false is added-as-extra.
            result.Add(keepSource ? packet.WithAmount(packet.Amount - portion) : packet);

            // The destination lane rides as an aspect on the packet's own type, so armour still
            // answers the delivery (a converted sword hit is still a sword hit).
            var aspect = string.Equals(to, DamageLanes.Physical, StringComparison.OrdinalIgnoreCase) ? null : to.ToLowerInvariant();
            moved.Add(new Packet(packet.Type, aspect, portion));
        }

        result.AddRange(moved.Where(p => p.Amount > 0));
        return result.Where(p => p.Amount > 0).ToList();
    }
}
