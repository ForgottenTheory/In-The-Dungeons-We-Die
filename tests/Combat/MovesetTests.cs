using Dungeons.Actions;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Rules;
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Combat;

/// <summary>
/// The moveset builder and the op interpreter (docs/moves.md §3, §6) — the runtime guarantees:
/// golden resolution, idempotence, source-order independence, replacement reported not silent.
/// </summary>
public class MovesetTests
{
    private static MoveDefinition Slash() => new()
    {
        Id = "move.slash",
        Name = "Slash",
        Tags = new[] { "action:attack", "delivery:melee", "form:sword" },
        Timing = new ActionTiming { TelegraphTicks = 4, WindupTicks = 10, RecoveryTicks = 18 },
        Costs = new[] { new ActionCost { Resource = "stamina", Amount = 10 } },
        Packets = new[] { new Packet(DamageType.Slashing, 20) },
    };

    /// <summary>The stormbrand modifier from the spec (§3.1), verbatim.</summary>
    private static MoveModifierDefinition Stormbrand() => new()
    {
        Id = "mod.stormbrand",
        Match = new MoveMatch { TagsAll = new[] { "action:attack" }, TagsAny = new[] { "form:sword", "form:dagger" } },
        Ops = new[]
        {
            new MoveOpSpec { Op = "convert", From = "physical", To = "charge", Fraction = 0.30 },
            new MoveOpSpec { Op = "addEffect", Effect = new EffectSpec { Kind = "applyStatus", Text = "status.shock", Chance = 0.15 } },
            new MoveOpSpec { Op = "scaleTiming", Field = "windup", Value = 0.92 },
        },
    };

    private static MoveModifierGrant Grant(MoveModifierDefinition def, string source = "test") => new(def, source);

    // --- Golden resolution (§6: "base + N modifiers → assert every field") --------------------

    [Fact]
    public void StormbrandOnASlash_TheGoldenResolution()
    {
        var resolved = MovesetBuilder.Resolve(Slash(), "sword", new[] { Grant(Stormbrand()) });

        // convert: 30% of the physical lane moves to charge, riding the same damage type.
        Assert.Equal(2, resolved.Packets.Count);
        Assert.Equal(14, resolved.Packets[0].Amount, 3);
        Assert.Null(resolved.Packets[0].Aspect);
        Assert.Equal(6, resolved.Packets[1].Amount, 3);
        Assert.Equal("charge", resolved.Packets[1].Aspect);
        Assert.Equal(DamageType.Slashing, resolved.Packets[1].Type);   // still a sword hit
        Assert.Equal(20, resolved.Packets.Sum(p => p.Amount), 3);      // convert relabels, never adds

        // addEffect: the rider arrives with its own chance.
        var rider = Assert.Single(resolved.Effects);
        Assert.Equal("status.shock", rider.Text);
        Assert.Equal(0.15, rider.Chance, 3);

        // scaleTiming: windup 10 × 0.92 → 9 (rounded); everything else untouched.
        Assert.Equal(9, resolved.Timing.WindupTicks);
        Assert.Equal(4, resolved.Timing.TelegraphTicks);
        Assert.Equal(18, resolved.Timing.RecoveryTicks);

        // Provenance names both the grantor and the modifier.
        Assert.Contains("sword", resolved.Provenance);
        Assert.Contains(resolved.Provenance, p => p.Contains("mod.stormbrand"));
    }

    [Fact]
    public void AddAsExtraDuplicatesWithoutReducingTheSource()
    {
        var extra = new MoveModifierDefinition
        {
            Id = "mod.heat",
            Match = new MoveMatch { TagsAll = new[] { "action:attack" } },
            Ops = new[] { new MoveOpSpec { Op = "addAsExtra", From = "physical", To = "heat", Fraction = 0.2 } },
        };

        var resolved = MovesetBuilder.Resolve(Slash(), "sword", new[] { Grant(extra) });

        Assert.Equal(20, resolved.Packets[0].Amount, 3);   // source untouched
        Assert.Equal(4, resolved.Packets[1].Amount, 3);    // 20% duplicated into heat
        Assert.Equal(24, resolved.Packets.Sum(p => p.Amount), 3);
    }

    /// <summary>`addTag` is the composition lever: one modifier tags the move, a second matches
    /// the new tag. That is how affixes compose into builds instead of stacking into a list.</summary>
    [Fact]
    public void AddTagMakesTheMoveEligibleForALaterModifier()
    {
        var tagger = new MoveModifierDefinition
        {
            Id = "mod.chains",
            Match = new MoveMatch { TagsAll = new[] { "action:attack" } },
            Ops = new[] { new MoveOpSpec { Op = "addTag", Tag = "mech:chain" } },
        };

        var resolved = MovesetBuilder.Resolve(Slash(), "sword", new[] { Grant(tagger) });
        Assert.True(resolved.HasTag("mech:chain"));

        // The second modifier matches against the SOURCE definition, so within one resolution
        // pass it does not see the added tag — chained availability lands on the next rebuild.
        // That is the cached-resolution bargain (§3.3), pinned here so it is a decision, not a bug.
        var chainer = new MoveModifierDefinition
        {
            Id = "mod.on_chain",
            Match = new MoveMatch { TagsAll = new[] { "mech:chain" } },
            Ops = new[] { new MoveOpSpec { Op = "addTargets", Value = 1 } },
        };

        var samePass = MovesetBuilder.Resolve(Slash(), "sword", new[] { Grant(tagger), Grant(chainer) });
        Assert.Equal(1, samePass.MaxTargets);

        var nextPass = MovesetBuilder.Resolve(samePass.Snapshot(), "sword", new[] { Grant(chainer) });
        Assert.Equal(2, nextPass.MaxTargets);
    }

    // --- Idempotence and order independence (§6) ----------------------------------------------

    [Fact]
    public void ResolvingTwiceWithTheSameModifiersYieldsTheSameMove()
    {
        var a = MovesetBuilder.Resolve(Slash(), "sword", new[] { Grant(Stormbrand()) });
        var b = MovesetBuilder.Resolve(Slash(), "sword", new[] { Grant(Stormbrand()) });

        Assert.Equal(a.Packets.Select(p => p.ToString()), b.Packets.Select(p => p.ToString()));
        Assert.Equal(a.Timing.WindupTicks, b.Timing.WindupTicks);
        Assert.Equal(a.Costs.Select(c => c.ToString()), b.Costs.Select(c => c.ToString()));
    }

    /// <summary>Shuffling the SOURCE order of modifiers must not change the result — the fixed
    /// op order guarantees it; this proves it (§3.3: "the same three affixes on different items
    /// must produce the same move").</summary>
    [Fact]
    public void ModifierSourceOrderDoesNotChangeTheResolvedMove()
    {
        var scale = new MoveModifierDefinition
        {
            Id = "mod.sharp",
            Match = new MoveMatch { TagsAll = new[] { "action:attack" } },
            Ops = new[] { new MoveOpSpec { Op = "scaleDamage", Value = 1.5 } },
        };

        var oneWay = MovesetBuilder.Resolve(Slash(), "sword", new[] { Grant(Stormbrand()), Grant(scale) });
        var otherWay = MovesetBuilder.Resolve(Slash(), "sword", new[] { Grant(scale), Grant(Stormbrand()) });

        // Semantic equality: same total, same per-lane split, same timing — whatever the list order.
        Assert.Equal(oneWay.Packets.Sum(p => p.Amount), otherWay.Packets.Sum(p => p.Amount), 3);
        Assert.Equal(
            oneWay.Packets.Where(p => p.Lane == "charge").Sum(p => p.Amount),
            otherWay.Packets.Where(p => p.Lane == "charge").Sum(p => p.Amount), 3);
        Assert.Equal(oneWay.Timing.WindupTicks, otherWay.Timing.WindupTicks);

        // And the order itself is the spec'd one: scaleDamage runs BEFORE convert, so increases
        // apply to the lane the damage started in — 20 × 1.5 = 30, then 30% converts.
        Assert.Equal(9, oneWay.Packets.Where(p => p.Lane == "charge").Sum(p => p.Amount), 3);
    }

    // --- Grants, replacement, conflicts (§3.3) ------------------------------------------------

    [Fact]
    public void ReplacementSwapsTheMoveAndMissingTargetsAreReported()
    {
        var store = Moves(
            Slash(),
            Move("move.frenzy", DamageType.Slashing, 9, 2, 6, 10));

        var builder = new MovesetBuilder(store);

        var conflicts = builder.Build(
            new[]
            {
                new MoveGrant(new MoveGrantSpec { Id = "move.slash" }, "sword"),
                new MoveGrant(new MoveGrantSpec { Id = "move.frenzy", Replaces = "move.slash" }, "form"),
            },
            Array.Empty<MoveModifierGrant>(),
            out var moveset);

        Assert.Empty(conflicts);
        Assert.Equal("move.frenzy", Assert.Single(moveset).Id);

        conflicts = builder.Build(
            new[] { new MoveGrant(new MoveGrantSpec { Id = "move.frenzy", Replaces = "move.ghost" }, "form") },
            Array.Empty<MoveModifierGrant>(),
            out moveset);

        Assert.Contains(conflicts, c => c.Contains("move.ghost"));
        Assert.Single(moveset);   // the grant itself still lands
    }

    [Fact]
    public void UnknownAndDuplicateGrantsAreConflictsNotCrashes()
    {
        var builder = new MovesetBuilder(Moves(Slash()));

        var conflicts = builder.Build(
            new[]
            {
                new MoveGrant(new MoveGrantSpec { Id = "move.slash" }, "sword"),
                new MoveGrant(new MoveGrantSpec { Id = "move.slash" }, "ring"),
                new MoveGrant(new MoveGrantSpec { Id = "move.ghost" }, "curse"),
            },
            Array.Empty<MoveModifierGrant>(),
            out var moveset);

        Assert.Single(moveset);
        Assert.Contains(conflicts, c => c.Contains("ring"));
        Assert.Contains(conflicts, c => c.Contains("move.ghost"));
    }

    [Fact]
    public void SetFlagUninterruptibleFlipsTheMove()
    {
        var flag = new MoveModifierDefinition
        {
            Id = "mod.resolute",
            Match = new MoveMatch { MoveId = "move.slash" },
            Ops = new[] { new MoveOpSpec { Op = "setFlag", Field = "uninterruptible" } },
        };

        var resolved = MovesetBuilder.Resolve(Slash(), "sword", new[] { Grant(flag) });
        Assert.False(resolved.Interruptible);
    }
}
