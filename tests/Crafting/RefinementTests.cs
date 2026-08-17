using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The meta fields — material strength, workability, volatility (docs/emergent-item-system.md §6).
///
/// <para>§17 lists these as the counters to named exploits: material strength-as-weighted-mean kills
/// the God Ingot, the material strength ceiling kills conjuring quality from nothing, and the workability
/// budget kills infinite refinement chains. Those are the tests that matter most here — a
/// counter that does not actually hold is worse than no counter at all.</para>
/// </summary>
public class RefinementTests
{
    private static readonly RoleWeights StandardWeights =
        new() { Substrate = 0.65, Reagent = 0.30, Catalyst = 0.05 };

    private static DataStore<PropertyDefinition> Properties() =>
        TestPaths.LoadStore<PropertyDefinition>("properties");

    private static DataStore<MaterialDefinition> Materials() =>
        TestPaths.LoadStore<MaterialDefinition>("materials");

    private static DataStore<CraftingActionDefinition> CraftingActions() =>
        TestPaths.LoadStore<CraftingActionDefinition>("processes");

    // ---- §6.1 material strength --------------------------------------------------------------------

    /// <summary>§19: "MaterialStrength = (40×0.65 + 70×0.30 + 0) × 1.05 = 49 ; ceiling = 70 + 8 = 78".</summary>
    [Fact]
    public void Potency_MatchesTheWorkedExample()
    {
        var materialStrength = MaterialStrengthCalculator.Compute(
            substrateStrength: 40,
            reagentPotencies: new[] { 70 },
            catalystStrength: null,
            StandardWeights,
            qualityMultiplier: 1.05,
            craftQuality: 1.0);

        Assert.Equal(49, materialStrength);
    }

    /// <summary>The God Ingot counter: material strength is a mean, so a junk input lowers it. Feeding
    /// everything you own into one item makes it worse, not better.</summary>
    [Fact]
    public void Potency_FallsWhenAJunkReagentIsAdded()
    {
        var good = MaterialStrengthCalculator.Compute(60, new[] { 70 }, null, StandardWeights, 1.0, 0.5);
        var diluted = MaterialStrengthCalculator.Compute(60, new[] { 70, 5 }, null, StandardWeights, 1.0, 0.5);

        Assert.True(diluted < good, $"adding junk raised materialStrength from {good} to {diluted}.");
    }

    /// <summary>Refinement can improve a material a little; it cannot conjure quality from
    /// nothing. Exhaustive, because this is the cap the whole economy leans on.</summary>
    [Fact]
    public void Potency_NeverExceedsTheBestInputByMoreThanTheCeilingBonus()
    {
        foreach (var substrate in new[] { 1, 20, 50, 80, 100 })
        foreach (var reagent in new[] { 1, 20, 50, 80, 100 })
        foreach (var craftQuality in new[] { 0.0, 0.5, 1.0 })
        {
            var materialStrength = MaterialStrengthCalculator.Compute(
                substrate, new[] { reagent }, catalystStrength: 100, StandardWeights,
                MaterialStrengthCalculator.QualityMultiplier(craftQuality), craftQuality);

            var best = Math.Max(Math.Max(substrate, reagent), 100);
            Assert.True(materialStrength <= best + RefinementTuning.MaterialStrengthCeilingBonus,
                $"materialStrength {materialStrength} exceeded the ceiling for inputs {substrate}/{reagent}.");
        }
    }

    /// <summary>At zero skill the ceiling is exactly the best input — no free improvement.</summary>
    [Fact]
    public void Potency_CannotImproveOnTheBestInput_WithoutSkill()
    {
        Assert.Equal(70.0, MaterialStrengthCalculator.Ceiling(40, new[] { 70 }, null, craftQuality: 0.0));
        Assert.Equal(78.0, MaterialStrengthCalculator.Ceiling(40, new[] { 70 }, null, craftQuality: 1.0));
    }

    /// <summary>An empty catalyst slot contributes zero rather than being renormalized away
    /// (§19 computes it that way), which is what gives catalysts a job in P1.</summary>
    [Fact]
    public void Potency_RewardsFillingTheCatalystSlot()
    {
        var without = MaterialStrengthCalculator.Compute(50, new[] { 60 }, null, StandardWeights, 1.0, 0.5);
        var with = MaterialStrengthCalculator.Compute(50, new[] { 60 }, 80, StandardWeights, 1.0, 0.5);

        Assert.True(with > without, $"a catalyst should help: {without} → {with}.");
    }

    // ---- §6.2 workability -------------------------------------------------------------------

    /// <summary>Cost scales with how violent the change was, not with how many times you
    /// crafted — which is what makes elegant paths mechanically rewarded (§6.2a).</summary>
    [Fact]
    public void IntegrityCost_ScalesWithChangeAndSeverity()
    {
        var gentlePrecise = WorkabilityCalculator.Cost(stateDelta: 0.10, severity: 0.20, stressReleased: 0, craftQuality: 0.5);
        var bruteForce = WorkabilityCalculator.Cost(stateDelta: 0.80, severity: 0.60, stressReleased: 0, craftQuality: 0.5);

        Assert.True(bruteForce > gentlePrecise * 4,
            $"thrashing the vector ({bruteForce:0.##}) should cost far more than a precise step ({gentlePrecise:0.##}).");
    }

    [Fact]
    public void IntegrityCost_RisesWithStrainReleased()
    {
        var calm = WorkabilityCalculator.Cost(0.3, 0.5, stressReleased: 0, craftQuality: 0.5);
        var violent = WorkabilityCalculator.Cost(0.3, 0.5, stressReleased: 40, craftQuality: 0.5);

        Assert.True(violent > calm, "annihilating opposites should cost workability.");
    }

    /// <summary>Skill mitigates the cost but can never make a step free — otherwise a master
    /// could ratchet a material through A→B→A loops forever (§17).</summary>
    [Fact]
    public void IntegrityCost_IsMitigatedBySkill_ButNeverReachesZero()
    {
        var unskilled = WorkabilityCalculator.Cost(0.4, 0.5, 0, craftQuality: 0.0);
        var master = WorkabilityCalculator.Cost(0.4, 0.5, 0, craftQuality: 1.0);

        Assert.True(master < unskilled);
        Assert.True(WorkabilityCalculator.Cost(0.0, 0.0, 0, craftQuality: 1.0) >= RefinementTuning.MinimumWorkabilityCost);
    }

    /// <summary>
    /// The cheapest step a master crafter can possibly make — a near-zero change through the
    /// gentlest crafting action at perfect skill — still costs the minimum, so even this bottoms out.
    /// That is what forecloses ratcheting A→B→A loops (§17).
    /// </summary>
    [Fact]
    public void EvenTheCheapestPossibleStep_ExhaustsIntegrityEventually()
    {
        var workability = 100;
        var steps = 0;

        while (workability > 0)
        {
            var next = WorkabilityCalculator.Apply(workability, WorkabilityCalculator.Cost(0.0, 0.0, 0, craftQuality: 1.0));
            Assert.True(next < workability, "no step may restore or preserve workability.");
            workability = next;

            Assert.True(++steps <= 100, "the chain must terminate.");
        }

        Assert.Equal(0, workability);
    }

    // ---- §6.2b volatility ------------------------------------------------------------------

    /// <summary>Low workability is the frontier, not a wall: outcomes widen rather than stop.</summary>
    [Fact]
    public void EffectiveInstability_RisesAsIntegrityIsSpent()
    {
        var fresh = WorkabilityCalculator.EffectiveInstability(baseInstability: 20, workability: 100);
        var worn = WorkabilityCalculator.EffectiveInstability(baseInstability: 20, workability: 20);

        Assert.Equal(20.0, fresh, 6);
        Assert.True(worn > fresh, "a worn material should be less predictable.");
    }

    /// <summary>§7.4/§12.3: mastery means <i>control</i>. At perfect execution the outcome is
    /// exactly the material the player aimed at.</summary>
    [Fact]
    public void Variance_VanishesAtPerfectSkill_AndWidensWithoutIt()
    {
        Assert.Equal(0.0, WorkabilityCalculator.VarianceMagnitude(80, craftQuality: 1.0, severity: 0.6));
        Assert.True(WorkabilityCalculator.VarianceMagnitude(80, craftQuality: 0.1, severity: 0.6) > 0);
    }

    // ---- §6.2c destruction and its projection -------------------------------------------------

    [Fact]
    public void Projection_ReportsNoRisk_WhenTheCostIsComfortablyAffordable()
    {
        var projection = WorkabilityCalculator.ProjectRemaining(currentWorkability: 90, expectedCost: 12, varianceMagnitude: 4);

        Assert.Equal(0.0, projection.DestructionChance);
        Assert.False(projection.IsAtRisk);
        Assert.Equal(78, projection.ProjectedWorkability);
    }

    /// <summary>Destruction is never a surprise: when it is unavoidable the projection says so
    /// outright rather than showing a percentage.</summary>
    [Fact]
    public void Projection_ReportsCertainDestruction_WhenTheCostExceedsWhatIsLeft()
    {
        var projection = WorkabilityCalculator.ProjectRemaining(currentWorkability: 6, expectedCost: 20, varianceMagnitude: 2);

        Assert.True(projection.IsCertainDestruction);
        Assert.Equal(0, projection.ProjectedWorkability);
    }

    /// <summary>The edge is a visible risk band, not a hidden cliff (§6.2c) — so pushing a
    /// deep material is a legible gamble the player chooses to take.</summary>
    [Fact]
    public void Projection_ReportsAPercentage_InsideTheRiskBand()
    {
        var projection = WorkabilityCalculator.ProjectRemaining(currentWorkability: 18, expectedCost: 18, varianceMagnitude: 20);

        Assert.True(projection.IsAtRisk, $"expected a percentage, got {projection.DestructionChance:P0}.");
        Assert.Equal(0.5, projection.DestructionChance, 3);
    }

    [Fact]
    public void DestructionChance_RisesWithCostAndNeverLeavesZeroToOne()
    {
        var previous = -1.0;
        foreach (var cost in new[] { 1.0, 5.0, 10.0, 20.0, 40.0, 80.0 })
        {
            var chance = WorkabilityCalculator.DestructionChance(30, cost, spread: 10);
            Assert.InRange(chance, 0.0, 1.0);
            Assert.True(chance >= previous, "a costlier craft cannot be safer.");
            previous = chance;
        }
    }

    // ---- §6.2c byproducts -----------------------------------------------------------------

    /// <summary>Which byproduct you get follows from the material's own form tag — no item id
    /// is named anywhere in the table.</summary>
    [Theory]
    [InlineData("material.iron_ingot", "material.slag")]   // form:metal, form:ingot
    [InlineData("material.granite", "material.slag")]      // form:stone
    [InlineData("material.oak_bark", "material.cinders")]  // form:bark
    [InlineData("material.wolf_hide", "material.dross")]   // form:hide
    [InlineData("material.ember_sap", "material.residue")] // form:liquid
    public void Byproducts_FollowTheDominantFormTag(string materialId, string expected)
    {
        var byproducts = new ByproductResolver(TestPaths.LoadStore<ByproductDefinition>("byproducts"));
        var byproduct = byproducts.ByproductFor(Materials().GetById(materialId).Tags);

        Assert.Equal(expected, byproduct?.ItemId);
    }

    /// <summary>
    /// Destruction is never total loss (§6.2c). The table has to be total — including for
    /// emergent forms nobody authored — or a blown craft returns a zero and players stop
    /// experimenting.
    /// </summary>
    [Fact]
    public void EveryMaterialYieldsAByproduct_IncludingUnknownForms()
    {
        var byproducts = new ByproductResolver(TestPaths.LoadStore<ByproductDefinition>("byproducts"));

        foreach (var material in Materials().GetAll())
            Assert.NotNull(byproducts.ByproductFor(material.Tags));

        Assert.Equal("material.residue", byproducts.ByproductFor(new[] { "form:antimatter" })?.ItemId);
        Assert.Equal("material.residue", byproducts.ByproductFor(Array.Empty<string>())?.ItemId);
    }

    /// <summary>"Some byproducts should be genuinely useful reagents in their own right"
    /// (§6.2c) — a consolation prize that is useless is not a consolation prize.</summary>
    [Fact]
    public void ByproductMaterials_AreUsableReagents()
    {
        var materials = Materials();
        var materialStates = new MaterialStateResolver(Properties());

        foreach (var id in new[] { "material.slag", "material.cinders", "material.dross", "material.residue" })
        {
            var material = materials.GetById(id);
            var profile = materialStates.StateOf(material);

            Assert.Contains("state:spent", material.Tags);
            Assert.True(profile.MaterialStrength >= 25, $"{id} at materialStrength {profile.MaterialStrength} is not worth picking up.");
            Assert.True(profile.Workability < 100, $"{id} should arrive already worked.");
            Assert.True(material.GetProperty("affinity") > 0 || material.GetProperty("solubility") > 0,
                $"{id} cannot participate in anything.");
        }
    }

    // ---- The loop actually closes ------------------------------------------------------------

    /// <summary>
    /// §6.1's central claim, tested end to end: "climbing from 40 to 90 requires many
    /// generations, and the Workability budget won't allow it. That's the intersection that
    /// closes the loop." Run with <i>perfect</i> skill and an unchanging high-material strength reagent,
    /// which is the most favourable case the player can construct.
    /// </summary>
    [Fact]
    public void RefinementRunsOutOfIntegrityBeforePotencyRunsAway()
    {
        var properties = Properties();
        var craftingAction = CraftingActions().GetById("process.forge_infusion");
        var reagent = Materials().GetById("material.ember_core");
        var reagentProfile = new MaterialStateResolver(properties).StateOf(reagent);

        var state = Materials().GetById("material.iron_ingot").BaseProperties;
        var materialStrength = 40;
        var workability = 90;
        var generation = 1;

        const double craftQuality = 1.0;
        var qualityMultiplier = MaterialStrengthCalculator.QualityMultiplier(craftQuality);

        while (workability > 0 && generation < 100)
        {
            var step = MaterialTransformationRules.ApplyReagent(
                state, reagent.BaseProperties, craftingAction, properties, workability, qualityMultiplier);

            var cost = WorkabilityCalculator.Cost(step.StateDelta, craftingAction.Severity, step.StressReleased, craftQuality);

            state = step.Properties;
            materialStrength = MaterialStrengthCalculator.Compute(
                materialStrength, new[] { reagentProfile.MaterialStrength }, null, craftingAction.RoleWeights, qualityMultiplier, craftQuality);
            workability = WorkabilityCalculator.Apply(workability, cost);
            generation++;

            Assert.True(materialStrength < 90, $"materialStrength reached {materialStrength} at generation {generation}.");
        }

        Assert.Equal(0, workability);
        Assert.True(generation < 100, "the chain must terminate.");
    }

    /// <summary>Generation is a depth counter, not a gate — workability is the gate (§6.4).</summary>
    [Fact]
    public void Generation_CountsDepthWithoutGatingAnything()
    {
        var lineage = Lineage.ForBase("material.iron_ingot");
        Assert.Equal(1, lineage.Generation);

        var next = lineage with { Generation = lineage.Generation + 1 };
        Assert.Equal(2, next.Generation);
    }
}
