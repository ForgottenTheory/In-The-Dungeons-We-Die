using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Events;
using Dungeons.Tests;
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Combat;

/// <summary>
/// E2b — statuses and Resolve (docs/statuses.md).
///
/// <para>Before this slice the game had <b>no status system at all</b> while shipping fourteen
/// authored status ids that sat in <c>TriggerRuleEngine.Unhandled</c>. These tests pin the
/// taxonomy rules — the category decides stacking, removal and what resists it — and the Resolve
/// model that answers the brief's "Freeze → Freeze → boss becomes furniture" problem.</para>
/// </summary>
public class StatusTests
{
    private static DataStore<StatusDefinition> Statuses() => TestPaths.LoadStore<StatusDefinition>("statuses");

    private static (StatusController controller, List<GameEvent> log, TickRef tick) Build()
    {
        var tick = new TickRef();
        var bus = new GameEventBus();
        var log = new List<GameEvent>();
        bus.Subscribe(log.Add);
        return (new StatusController(Statuses(), bus, () => tick.Now), log, tick);
    }

    private sealed class TickRef { public long Now; }

    private static Combatant Target(double resolve = 0) =>
        new("Target", CombatTeam.Enemy,
            new Dungeons.Characters.ResourcePool(Dungeons.Characters.ResourceType.Health, 100),
            new Dungeons.Characters.ResourcePool(Dungeons.Characters.ResourceType.Stamina, 100),
            new Dungeons.Characters.ResourcePool(Dungeons.Characters.ResourceType.Mana, 0),
            Array.Empty<ResolvedMove>(), () => Attrs()) { Resolve = resolve };

    // --- The roster ---------------------------------------------------------

    [Fact]
    public void TheFourteenCoreStatusesShip_AndIgniteAndSlowDoNot()
    {
        var statuses = Statuses();

        foreach (var id in new[]
                 {
                     "status.bleed", "status.poison", "status.burn",
                     "status.chill", "status.shock", "status.corroded", "status.weaken",
                     "status.stun", "status.freeze", "status.fear", "status.silence",
                     "status.guarded", "status.vulnerable", "status.barrier",
                 })
            Assert.True(statuses.Contains(id), $"{id} should ship in v1");

        // D-09, contradicting GDD §5.9 deliberately: Burn supersedes Ignite, Chill supersedes
        // Slow. Shipping either pair means one is strictly a worse version of the other.
        Assert.False(statuses.Contains("status.ignite"));
        Assert.False(statuses.Contains("status.slow"));
    }

    [Fact]
    public void EveryStatusIdAuthoredInShippedContentNowResolves()
    {
        // E2's thirteen, plus the fourteenth: `status.recalled_move` needed MoveDefinition to
        // exist (it stores a move), so E4 is where the last dangling id went live and the
        // validator's allowlist died.
        var statuses = Statuses();

        foreach (var id in new[]
                 {
                     "status.toxin", "status.planted_charge", "status.feint_ready",
                     "status.illuminated", "status.phased", "status.rooted_growth",
                     "status.latched", "status.spreading", "status.dissonance",
                     "status.fault", "status.filed_intent", "status.liability",
                     "status.liability_credit", "status.recalled_move",
                 })
            Assert.True(statuses.Contains(id), $"{id} is referenced by shipped content and must exist");

        Assert.True(statuses.GetById("status.recalled_move").StoresMove);
    }

    [Fact]
    public void BurnAndPoisonAreDeliberateOpposites()
    {
        // The pairing that makes heat and toxin play differently rather than being reskins.
        var statuses = Statuses();
        var burn = statuses.GetById("status.burn");
        var poison = statuses.GetById("status.poison");

        Assert.Equal(StackPolicy.RefreshHighest, burn.StackPolicy);
        Assert.Equal(1, burn.MaxStacks);

        Assert.Equal(StackPolicy.Stack, poison.StackPolicy);
        Assert.True(poison.MaxStacks > 10);

        Assert.True(burn.Magnitude.Coefficient > poison.Magnitude.Coefficient, "Burn hits harder");
        Assert.True(poison.DurationTicks > burn.DurationTicks, "Poison lasts far longer");
    }

    // --- Stacking policies --------------------------------------------------

    [Fact]
    public void PoisonStacksUpToItsCap()
    {
        var (controller, _, _) = Build();
        var target = Target();

        for (var i = 0; i < 30; i++)
            controller.Apply(target, "status.poison", "attacker", magnitude: 2);

        Assert.Equal(20, controller.Find(target, "status.poison")!.Stacks);
    }

    [Fact]
    public void BurnKeepsTheStrongerApplication_AndRefreshesOnTheWeaker()
    {
        var (controller, _, tick) = Build();
        var target = Target();

        controller.Apply(target, "status.burn", "a", magnitude: 10);
        controller.Apply(target, "status.burn", "a", magnitude: 4);

        var burn = controller.Find(target, "status.burn")!;
        Assert.Equal(10, burn.Magnitude);   // the weaker application did not overwrite
        Assert.Equal(1, burn.Stacks);

        tick.Now = 30;
        controller.Apply(target, "status.burn", "a", magnitude: 4);
        Assert.Equal(30 + 60, controller.Find(target, "status.burn")!.ExpiresTick); // but it did refresh
    }

    [Fact]
    public void AUniqueStatusIgnoresLaterApplications()
    {
        var (controller, _, tick) = Build();
        var target = Target();

        controller.Apply(target, "status.phased", "a");
        var applied = controller.Find(target, "status.phased")!.AppliedTick;

        tick.Now = 20;
        controller.Apply(target, "status.phased", "a");

        Assert.Equal(applied, controller.Find(target, "status.phased")!.AppliedTick);
    }

    // --- Modifiers ----------------------------------------------------------

    [Fact]
    public void ChillContributesItsSlowAsAnOrdinaryModifier()
    {
        // Chill is literally `{ key: combat.windup.mult, value: 1.25 }`. No status-specific code
        // anywhere — which is why 27 statuses cost roughly what 3 would.
        var (controller, _, _) = Build();
        var target = Target();

        controller.Apply(target, "status.chill", "a");

        Assert.Equal(1.25, controller.ModifierTotal(target, "combat.windup.mult", multiplicative: true), 3);
    }

    [Fact]
    public void CorrodedStripsArmourPerStack()
    {
        var (controller, _, _) = Build();
        var target = Target();

        for (var i = 0; i < 3; i++)
            controller.Apply(target, "status.corroded", "a");

        Assert.Equal(-9, controller.ModifierTotal(target, "combat.armor"), 3); // −3 per stack
    }

    // --- The clock ----------------------------------------------------------

    [Fact]
    public void AnAilmentTicksOnItsIntervalAndThenExpires()
    {
        var (controller, _, tick) = Build();
        var target = Target();
        var ticks = 0;
        controller.Ticked += (_, _) => ticks++;

        controller.Apply(target, "status.burn", "a", magnitude: 5); // 60 ticks, every 15

        for (tick.Now = 1; tick.Now <= 80; tick.Now++)
            controller.Advance(new[] { target });

        Assert.Equal(4, ticks); // 15, 30, 45, 60 — then it is gone
        Assert.False(controller.Has(target, "status.burn"));
    }

    [Fact]
    public void CleansingAGroupRemovesEveryStatusInIt()
    {
        var (controller, _, _) = Build();
        var target = Target();

        controller.Apply(target, "status.burn", "a", magnitude: 5);
        controller.Apply(target, "status.poison", "a", magnitude: 5);
        controller.Apply(target, "status.chill", "a");

        Assert.Equal(2, controller.CleanseGroup(target, "ailment"));
        Assert.False(controller.Has(target, "status.burn"));
        Assert.True(controller.Has(target, "status.chill")); // an impairment, not an ailment
    }

    // --- Resolve: the CC answer ---------------------------------------------

    [Fact]
    public void AControlDoesNotLandUntilBuildupCrossesResolve()
    {
        var (controller, log, _) = Build();
        var target = Target(resolve: 100); // Fear contributes 30 per application

        Assert.Equal(ControlOutcome.Resisted, controller.Apply(target, "status.fear", "a"));
        Assert.Equal(ControlOutcome.Resisted, controller.Apply(target, "status.fear", "a"));
        Assert.Equal(ControlOutcome.Resisted, controller.Apply(target, "status.fear", "a"));
        Assert.False(controller.Has(target, "status.fear"));

        Assert.Equal(ControlOutcome.Applied, controller.Apply(target, "status.fear", "a")); // 120 ≥ 100
        Assert.True(controller.Has(target, "status.fear"));

        // Every failed attempt is a hook for "when you resist control…" affixes.
        Assert.Equal(3, log.Count(e => e.Kind == GameEvents.ControlResisted));
    }

    [Fact]
    public void LandingAControlBlocksEveryOtherControl_SoRotationCannotLock()
    {
        // The property that makes Resolve one mechanism rather than five: Fear → Stun → Freeze
        // rotation cannot keep a target permanently disabled.
        var (controller, _, tick) = Build();
        var target = Target(resolve: 30);

        Assert.Equal(ControlOutcome.Applied, controller.Apply(target, "status.fear", "a"));
        Assert.Equal(ControlOutcome.Immune, controller.Apply(target, "status.silence", "a"));

        tick.Now = CombatTuning.ControlImmunityTicks + 1;
        Assert.NotEqual(ControlOutcome.Immune, controller.Apply(target, "status.silence", "a"));
    }

    [Fact]
    public void EachLandedControlMakesTheNextOneHarder_ForTheRestOfTheEncounter()
    {
        // The in-fight arc — the thing a flat diminishing-returns ladder cannot produce, and the
        // reason Resolve was chosen over direct-chance.
        var (controller, _, tick) = Build();
        var target = Target(resolve: 100);

        var before = controller.ResolveOf(target);
        Assert.Equal(100, before, 3);

        for (var i = 0; i < 4; i++)
            controller.Apply(target, "status.fear", "a"); // 4 × 30 = 120 ≥ 100

        Assert.Equal(125, controller.ResolveOf(target), 3); // +25% of base

        tick.Now = CombatTuning.ControlImmunityTicks + 1;
        for (var i = 0; i < 5; i++)
            controller.Apply(target, "status.fear", "a"); // now needs 125

        // Escalation is +25% OF BASE per landed control, not compounding: 100 → 125 → 150 → 175.
        // Compounding would reach 9× after ten controls, which stops being a curve and becomes a
        // wall; linear is both gentler and far easier to state in a tooltip.
        Assert.Equal(150, controller.ResolveOf(target), 3);
    }

    [Fact]
    public void FreezeCannotBeAppliedToAnUnchilledTarget()
    {
        // A gate, not a resistance: an unchilled target accumulates NO freeze buildup at all.
        // This is what makes cold two-step rather than a third burst aspect.
        var (controller, _, _) = Build();
        var target = Target(resolve: 30);

        Assert.Equal(ControlOutcome.Ungated, controller.Apply(target, "status.freeze", "a"));
        Assert.Equal(0, controller.BuildupOn(target, "status.freeze"));

        controller.Apply(target, "status.chill", "a");
        Assert.Equal(ControlOutcome.Applied, controller.Apply(target, "status.freeze", "a"));
    }

    [Fact]
    public void ControlBuildupDecaysSoPressureMustBeSustained()
    {
        var (controller, _, tick) = Build();
        var target = Target(resolve: 200);

        controller.Apply(target, "status.fear", "a");
        var initial = controller.BuildupOn(target, "status.fear");
        Assert.Equal(30, initial, 3);

        for (tick.Now = 1; tick.Now <= 10; tick.Now++)
            controller.Advance(new[] { target });

        Assert.True(controller.BuildupOn(target, "status.fear") < initial,
            "buildup must bleed off, or a whole fight's worth accumulates");
    }

    [Fact]
    public void ClearingDropsResolveEscalation_BecauseItIsPerEncounter()
    {
        var (controller, _, _) = Build();
        var target = Target(resolve: 30);

        controller.Apply(target, "status.fear", "a");
        Assert.True(controller.ResolveOf(target) > 30);

        controller.Clear();
        Assert.Equal(30, controller.ResolveOf(target), 3);
    }
}
