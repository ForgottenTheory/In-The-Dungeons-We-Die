using Dungeons.Items;
using Dungeons.Professions;
using Xunit;
using static Dungeons.Tests.Professions.ProfessionsTestData;

namespace Dungeons.Tests.Professions;

/// <summary>
/// The active layer. The rule the design turns on is that active play must never be "the same
/// button, more often" — so the load-bearing assertion in this file is
/// <see cref="PassiveNeverDiscoversAnything"/>: passive does not roll for opportunities at all,
/// which makes "fewer rare outcomes offline" structural rather than a tuning number
/// (docs/professions.md §4).
/// </summary>
public class OpportunityTests
{
    private const string RichVein = "opportunity.rich_vein";

    private static ProfessionOpportunityDefinition Vein(double risk = 0.0, double discoveryChance = 0.5) => new()
    {
        Id = RichVein,
        Name = "Rich Vein",
        Prompt = "The seam widens.",
        DiscoveryChance = discoveryChance,
        ExtraIntervalTicks = 200,
        RiskWeight = risk,
        Outputs = new[] { Amount("material.iron_ore", 4) },
        Experience = 40,
    };

    private static ProfessionActionDefinition MineIron(ProfessionOpportunityDefinition? opportunity = null) => new()
    {
        Id = "action.mine_iron",
        ProfessionId = "profession.mining",
        BaseIntervalTicks = 100,
        Experience = 10,
        Outputs = new[] { Amount("material.iron_ore") },
        Opportunities = opportunity is null
            ? Array.Empty<ProfessionOpportunityDefinition>()
            : new[] { opportunity },
    };

    /// <summary>A system whose every random draw returns <paramref name="everyDraw"/>, so a
    /// chance of C fires exactly when <c>everyDraw &lt; C</c>.</summary>
    private static ProfessionSystem SystemWith(ProfessionActionDefinition action, Inventory inventory, double everyDraw) =>
        new(Store(action), inventory, new FakeRandom(@default: everyDraw));

    [Fact]
    public void ActiveAttempt_CanSurfaceAnOpportunity()
    {
        var inventory = new Inventory();
        var system = SystemWith(MineIron(Vein()), inventory, 0.1); // 0.1 < 0.5

        var outcome = system.Execute("action.mine_iron", performance: 0.0, isActive: true);

        Assert.NotNull(outcome.DiscoveredOpportunity);
        Assert.Equal(RichVein, outcome.DiscoveredOpportunity!.Id);
    }

    /// <summary>
    /// The structural guarantee. Passive execution never even rolls, so an offline player
    /// cannot be unlucky with opportunities — they simply do not happen, which is the honest
    /// version of "fewer rare outcomes".
    /// </summary>
    [Fact]
    public void PassiveNeverDiscoversAnything()
    {
        var inventory = new Inventory();
        // A discovery chance of 1.0 and a draw of 0.0: the roll would fire if it happened.
        var system = SystemWith(MineIron(Vein(discoveryChance: 1.0)), inventory, 0.0);

        var outcome = system.Execute("action.mine_iron", performance: 0.0, isActive: false);

        Assert.Null(outcome.DiscoveredOpportunity);
    }

    [Fact]
    public void DiscoveringIsNotCollecting_TheOfferMustBePursued()
    {
        var inventory = new Inventory();
        var system = SystemWith(MineIron(Vein()), inventory, 0.1);

        system.Execute("action.mine_iron", performance: 0.0, isActive: true);

        // One ore from the action itself — the vein's four are not banked until it is pursued.
        Assert.Equal(1, inventory.GetQuantity("material.iron_ore"));
    }

    [Fact]
    public void PursuingBanksThePayoffAndItsXp()
    {
        var inventory = new Inventory();
        var system = SystemWith(MineIron(Vein()), inventory, 0.1);
        system.Execute("action.mine_iron", performance: 0.0, isActive: true);

        var outcome = system.PursueOpportunity("action.mine_iron", RichVein);

        Assert.True(outcome.Success);
        Assert.Equal(40, outcome.XpGained);
        Assert.Equal(5, inventory.GetQuantity("material.iron_ore")); // 1 from the action + 4
    }

    /// <summary>Declining is simply never calling PursueOpportunity — nothing is owed, nothing
    /// is lost, and the attempt's own yield stands.</summary>
    [Fact]
    public void DecliningCostsNothing()
    {
        var inventory = new Inventory();
        var system = SystemWith(MineIron(Vein()), inventory, 0.1);

        var outcome = system.Execute("action.mine_iron", performance: 0.0, isActive: true);

        Assert.NotNull(outcome.DiscoveredOpportunity);
        Assert.Equal(1, inventory.GetQuantity("material.iron_ore"));
        Assert.Equal(10, outcome.XpGained);
    }

    [Fact]
    public void RiskRealised_SpendsTheAttemptAndPaysOnlyPartialXp()
    {
        var inventory = new Inventory();
        // Risk 0.5, every draw 0.1: 0.1 < 0.5, so the gamble is lost.
        var system = SystemWith(MineIron(Vein(risk: 0.5)), inventory, 0.1);
        system.Execute("action.mine_iron", performance: 0.0, isActive: true);

        var outcome = system.PursueOpportunity("action.mine_iron", RichVein);

        Assert.False(outcome.Success);
        Assert.Equal(OpportunityFailure.RiskRealised, outcome.Failure);
        Assert.Empty(outcome.Produced);
        Assert.Equal(10, outcome.XpGained); // 40 × MissedAttemptXpFraction
        Assert.Equal(1, inventory.GetQuantity("material.iron_ore"));
    }

    [Fact]
    public void PursuingConsumesTheOpportunitysOwnInputs()
    {
        var inventory = new Inventory();
        var vein = new ProfessionOpportunityDefinition
        {
            Id = RichVein,
            Prompt = "Costs a torch.",
            DiscoveryChance = 0.5,
            ExtraIntervalTicks = 100,
            Inputs = new[] { Amount("material.charcoal", 2) },
            Outputs = new[] { Amount("material.iron_ore", 4) },
            Experience = 40,
        };

        var system = SystemWith(MineIron(vein), inventory, 0.1);

        var missing = system.PursueOpportunity("action.mine_iron", RichVein);
        Assert.Equal(OpportunityFailure.MissingInputs, missing.Failure);

        inventory.Add("material.charcoal", 2);
        var paid = system.PursueOpportunity("action.mine_iron", RichVein);

        Assert.True(paid.Success);
        Assert.Equal(0, inventory.GetQuantity("material.charcoal"));
    }

    [Fact]
    public void UnknownOpportunityIsRefusedRatherThanThrowing()
    {
        var system = SystemWith(MineIron(Vein()), new Inventory(), 0.1);

        Assert.Equal(OpportunityFailure.UnknownOpportunity,
            system.PursueOpportunity("action.mine_iron", "opportunity.nonexistent").Failure);
        Assert.Equal(OpportunityFailure.UnknownAction,
            system.PursueOpportunity("action.nonexistent", RichVein).Failure);
    }

    /// <summary>The shipped ladder, so these read the real numbers rather than a fixture's
    /// guess at them (Phase 8 moved the magnitudes into <c>game/data/mastery/</c>).</summary>
    private static readonly MasteryBenefits Ladder =
        new(TestPaths.LoadStore<MasteryBenefitDefinition>("mastery"));

    private static double MasteryOpportunityBonus(int mastery) =>
        Ladder.ValueOf(MasteryBenefitKind.OpportunityChance, "profession.mining", mastery);

    private static double MasteryRiskReduction(int mastery) =>
        Ladder.ValueOf(MasteryBenefitKind.OpportunityRisk, "profession.mining", mastery);

    [Fact]
    public void MasteryAndPerformanceBothRaiseTheDiscoveryChance()
    {
        var flat = ProfessionTuning.OpportunityDiscoveryChance(0.10, MasteryOpportunityBonus(0), performance: 0.0);
        var skilled = ProfessionTuning.OpportunityDiscoveryChance(0.10, MasteryOpportunityBonus(50), performance: 0.0);
        var focused = ProfessionTuning.OpportunityDiscoveryChance(0.10, MasteryOpportunityBonus(0), performance: 1.0);

        Assert.Equal(0.10, flat, 6);
        Assert.True(skilled > flat);
        Assert.True(focused > flat);
        Assert.True(ProfessionTuning.OpportunityDiscoveryChance(1.0, MasteryOpportunityBonus(99), 1.0) <= 1.0);
    }

    [Fact]
    public void MasteryTalksRiskDown_ButNeverToZero()
    {
        Assert.Equal(0.5, ProfessionTuning.EffectiveRisk(0.5, MasteryRiskReduction(0)), 6);
        Assert.True(ProfessionTuning.EffectiveRisk(0.5, MasteryRiskReduction(100)) < 0.5);
        Assert.True(ProfessionTuning.EffectiveRisk(0.5, MasteryRiskReduction(10_000)) > 0.0);
    }

    /// <summary>Only the first opportunity that fires is offered: two at once would turn a
    /// decision into a menu.</summary>
    [Fact]
    public void OnlyOneOpportunityIsOfferedAtATime()
    {
        var action = new ProfessionActionDefinition
        {
            Id = "action.mine_iron",
            ProfessionId = "profession.mining",
            Outputs = new[] { Amount("material.iron_ore") },
            Opportunities = new[]
            {
                new ProfessionOpportunityDefinition { Id = "opportunity.first", Prompt = "a", DiscoveryChance = 1.0, Experience = 1 },
                new ProfessionOpportunityDefinition { Id = "opportunity.second", Prompt = "b", DiscoveryChance = 1.0, Experience = 1 },
            },
        };

        var system = SystemWith(action, new Inventory(), 0.0);
        var outcome = system.Execute("action.mine_iron", performance: 1.0, isActive: true);

        Assert.Equal("opportunity.first", outcome.DiscoveredOpportunity!.Id);
    }
}
