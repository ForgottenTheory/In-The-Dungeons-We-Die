using Dungeons.Content;
using Dungeons.Crafting;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The pre-commit text (docs/emergent-item-system.md §6.2c).
///
/// <para>§6.2c makes destruction-at-zero conditional on three things, all of them about what
/// the player is <i>told</i>: the projected cost and result before committing, an explicit
/// warning at zero, and a percentage rather than a false certainty inside the risk band. That
/// makes this wording a rule, not decoration — which is why it is tested in Core rather than
/// eyeballed in the client.</para>
/// </summary>
public class CraftFormatTests
{
    private static CraftProjection Projection(
        double cost = 12,
        double spread = 0,
        int remaining = 78,
        double destructionChance = 0,
        string name = "Emberlit Iron",
        bool firstDiscovery = false) =>
        new(
            CraftFailure.None,
            new IntegrityProjection(cost, spread, remaining, destructionChance),
            ProjectedPotency: 49,
            ProjectedName: name,
            WouldBeFirstDiscovery: firstDiscovery,
            Preview: ReactionLog.Empty);

    [Fact]
    public void ASafeCraftShowsTheResultCostAndRemainingIntegrity()
    {
        var text = CraftFormat.Projection(Projection(), "Iron Ingot");

        Assert.Contains("Emberlit Iron", text);
        Assert.Contains("Potency 49", text);
        Assert.Contains("Integrity → 78", text);
        Assert.Contains("cost 12", text);
        Assert.DoesNotContain("⚠", text);
    }

    /// <summary>Discovery is the point of the system; the player should know before committing
    /// that they are about to make something nobody has made.</summary>
    [Fact]
    public void AnUnmadeMaterialIsFlaggedBeforeCommitting()
    {
        Assert.Contains("never made before", CraftFormat.Projection(Projection(firstDiscovery: true), "Iron Ingot"));
    }

    /// <summary>§6.2c: "must show an explicit destruction warning when the projection reaches
    /// zero." Not a percentage — a statement.</summary>
    [Fact]
    public void CertainDestructionIsStatedOutright()
    {
        var text = CraftFormat.Projection(
            Projection(cost: 40, remaining: 0, destructionChance: 1.0), "Tempestforged Iron");

        Assert.Contains("⚠", text);
        Assert.Contains("DESTROY", text);
        Assert.Contains("Tempestforged Iron", text);
        Assert.Contains("byproducts", text);
        Assert.DoesNotContain("%", text);
    }

    /// <summary>§6.2c: below the risk band the UI shows a destruction <i>chance</i>, so pushing
    /// a deep material is a legible gamble rather than a hidden cliff.</summary>
    [Fact]
    public void RiskIsShownAsAPercentage()
    {
        var text = CraftFormat.Projection(
            Projection(cost: 18, spread: 10, remaining: 2, destructionChance: 0.35), "Emberlit Iron");

        Assert.Contains("35% chance of destroying", text);
        Assert.Contains("± 10", text);
        Assert.DoesNotContain("DESTROY", text);
    }

    [Fact]
    public void AnImpossibleCraftExplainsItselfInsteadOfShowingNumbers()
    {
        var text = CraftFormat.Projection(CraftProjection.Failed(CraftFailure.SubstrateRejected), "Sageleaf");

        Assert.Equal("This process cannot work that material.", text);
        Assert.DoesNotContain("Potency", text);
    }

    [Fact]
    public void EveryFailureHasAPlayerFacingMessage()
    {
        foreach (var failure in Enum.GetValues<CraftFailure>())
        {
            var message = CraftFormat.Failure(failure);

            if (failure == CraftFailure.None)
                Assert.Equal(string.Empty, message);
            else
                Assert.False(string.IsNullOrWhiteSpace(message), $"{failure} has no message.");
        }
    }

    /// <summary>The process picker has to make "which process suits this ingredient" readable
    /// (§7.3) — the medium and the gate are the two facts that answer it.</summary>
    [Fact]
    public void ProcessLabelsCarryTheirMediumSeverityAndGate()
    {
        var processes = TestPaths.LoadStore<ProcessDefinition>("processes");

        var forge = CraftFormat.Process(processes.GetById("process.forge_infusion"), "Smithing");
        Assert.Contains("Forge Infusion", forge);
        Assert.Contains("thermal", forge);
        Assert.Contains("severity 0.55", forge);
        Assert.Contains("Smithing L15", forge);

        var grind = CraftFormat.Process(processes.GetById("process.grind"), string.Empty);
        Assert.Contains("any skill", grind);
    }

    /// <summary>§7.2: which properties a process opens is the answer to "why did nothing
    /// happen", so the bench shows it rather than making the player infer it.</summary>
    [Fact]
    public void TheChannelIsShownSoThePlayerKnowsWhatWillReact()
    {
        var channel = CraftFormat.Channel(
            TestPaths.LoadStore<ProcessDefinition>("processes").GetById("process.forge_infusion"));

        Assert.Contains("heat 0.8", channel);
        Assert.Contains("hardness 0.25", channel);
    }
}
