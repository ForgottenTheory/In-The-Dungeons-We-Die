using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The universal reaction algebra (docs/emergent-item-system.md §8).
///
/// <para>§19's worked example is the primary fixture: it uses real values from
/// <c>game/data/materials/</c> and states its intermediate results, so it pins the algebra
/// against the design rather than against itself. Its trait, signature and essence steps are
/// P2/P4/P3 and are not exercised here.</para>
///
/// <para>The rest are the invariants §17 lists as the counters to specific exploits. Those
/// must hold for <i>all</i> inputs, not just the worked example.</para>
/// </summary>
public class ReactionAlgebraTests
{
    private static DataStore<PropertyDefinition> Properties() =>
        TestPaths.LoadStore<PropertyDefinition>("properties");

    private static DataStore<MaterialDefinition> Materials() =>
        TestPaths.LoadStore<MaterialDefinition>("materials");

    private static DataStore<ProcessDefinition> Processes() =>
        TestPaths.LoadStore<ProcessDefinition>("processes");

    private static PropertySet Props(params (string Key, double Value)[] values) =>
        PropertySet.FromValues(values.ToDictionary(v => v.Key, v => v.Value));

    // ---- §8.1 coefficients ---------------------------------------------------------------

    /// <summary>§19: "A = 0.25 + 0.75×0.30 = 0.475 (iron barely wants to bond)".</summary>
    [Fact]
    public void Acceptance_MatchesTheWorkedExample()
    {
        var iron = Materials().GetById("material.iron_ingot").BaseProperties;
        Assert.Equal(0.475, ReactionCoefficients.ComputeAcceptance(iron), 3);
    }

    /// <summary>§19: sap dissolves willingly under a solvent (0.663); ember core gives freely
    /// under heat because it is volatile (0.925). Same reagent pairing, different medium.</summary>
    [Theory]
    [InlineData("material.ember_sap", TransferMedium.Solvent, 0.663)]
    [InlineData("material.ember_core", TransferMedium.Thermal, 0.925)]
    public void Release_MatchesTheWorkedExample(string materialId, TransferMedium medium, double expected)
    {
        var reagent = Materials().GetById(materialId).BaseProperties;
        Assert.Equal(expected, ReactionCoefficients.ComputeRelease(medium, reagent), 3);
    }

    /// <summary>§7.3: soft things grind readily, hard things resist the mill.</summary>
    [Fact]
    public void Release_UnderAMechanicalMedium_IsInverseHardness()
    {
        var soft = ReactionCoefficients.ComputeRelease(TransferMedium.Mechanical, Props(("hardness", 10)));
        var hard = ReactionCoefficients.ComputeRelease(TransferMedium.Mechanical, Props(("hardness", 90)));

        Assert.True(soft > hard, $"soft {soft:0.###} should release more readily than hard {hard:0.###}");
        Assert.Equal(0.925, soft, 3);
    }

    [Fact]
    public void IntegrityFactor_MatchesTheWorkedExample()
    {
        Assert.Equal(0.950, ReactionCoefficients.ComputeIntegrityFactor(90), 3);
    }

    /// <summary>§8.1's shared shape: even a wholly unwilling substrate accepts a little, so no
    /// craft is ever a total no-op.</summary>
    [Fact]
    public void Coefficients_NeverReachZero_EvenForTheWorstInputs()
    {
        Assert.Equal(0.25, ReactionCoefficients.ComputeAcceptance(PropertySet.Empty), 3);
        Assert.Equal(0.25, ReactionCoefficients.ComputeRelease(TransferMedium.Solvent, PropertySet.Empty), 3);
        Assert.Equal(0.50, ReactionCoefficients.ComputeIntegrityFactor(0), 3);
    }

    // ---- §19 attempt 1: the naive craft ---------------------------------------------------

    /// <summary>
    /// Steeping iron in Ember Sap. §19: heat lands at 7 — "barely anything. Iron's low
    /// affinity is the whole story." The lesson has to be legible: solvents don't open metal.
    /// </summary>
    [Fact]
    public void WorkedExample_SteepingIronInEmberSap_BarelyMovesHeat()
    {
        var result = ReactionAlgebra.ApplyReagent(
            Materials().GetById("material.iron_ingot").BaseProperties,
            Materials().GetById("material.ember_sap").BaseProperties,
            Processes().GetById("process.steep"),
            Properties(),
            substrateIntegrity: 90);

        Assert.Equal(0.475, result.Coefficients.Acceptance, 3);
        Assert.Equal(0.663, result.Coefficients.Release, 3);
        Assert.Equal(0.950, result.Coefficients.IntegrityFactor, 3);
        Assert.Equal(7.0, result.Properties.Get("heat"), 0);
    }

    // ---- §19 attempt 2: the informed craft ------------------------------------------------

    /// <summary>
    /// Forge Infusion of Iron Ingot with Ember Core at quality 1.05. §19 computes
    /// heat 0 → 35 and hardness 65 → 62. Both fall out of §8.2 with no special cases.
    /// </summary>
    [Fact]
    public void WorkedExample_ForgeInfusingIronWithEmberCore_MatchesStatedResults()
    {
        var result = ReactionAlgebra.ApplyReagent(
            Materials().GetById("material.iron_ingot").BaseProperties,
            Materials().GetById("material.ember_core").BaseProperties,
            Processes().GetById("process.forge_infusion"),
            Properties(),
            substrateIntegrity: 90,
            qualityMultiplier: 1.05);

        Assert.Equal(0.925, result.Coefficients.Release, 3);
        Assert.Equal(35.0, result.Properties.Get("heat"), 0);
        Assert.Equal(62.0, result.Properties.Get("hardness"), 0);
    }

    /// <summary>
    /// The same pair through a solvent versus a forge must produce genuinely different
    /// materials — that is §7.2's entire claim, and what makes process choice a real decision.
    /// </summary>
    [Fact]
    public void ProcessChoice_ChangesTheOutcomeForTheSameInputs()
    {
        var iron = Materials().GetById("material.iron_ingot").BaseProperties;
        var core = Materials().GetById("material.ember_core").BaseProperties;

        var steeped = ReactionAlgebra.ApplyReagent(
            iron, core, Processes().GetById("process.steep"), Properties(), 90);
        var forged = ReactionAlgebra.ApplyReagent(
            iron, core, Processes().GetById("process.forge_infusion"), Properties(), 90);

        Assert.True(forged.Properties.Get("heat") > steeped.Properties.Get("heat") * 2,
            "the forge should drive far more heat into metal than a steep does.");
    }

    // ---- §8.2 convergence: the anti-inflation core ----------------------------------------

    /// <summary>
    /// The single rule that kills unbounded stat escalation. Exhaustive over a grid rather
    /// than a happy path, because this must hold for every input the game can produce.
    /// </summary>
    [Fact]
    public void Convergence_CanNeverExceedTheStrongerInput()
    {
        foreach (var before in new[] { 0.0, 15.0, 50.0, 99.0, 100.0 })
        foreach (var target in new[] { 0.0, 15.0, 50.0, 99.0, 100.0 })
        foreach (var rate in new[] { 0.05, 0.5, 1.0 })
        foreach (var product in new[] { 0.1, 0.5, 1.0, 2.0 })
        {
            var after = ReactionAlgebra.Converge(before, target, rate, product);

            Assert.InRange(after, Math.Min(before, target), Math.Max(before, target));
        }
    }

    /// <summary>§8.2 caps a single step at 0.85 of the gap, so one step can never fully
    /// replace the substrate's value however favourable the coefficients.</summary>
    [Fact]
    public void Convergence_LeavesSomeGapEvenAtAbsurdRates()
    {
        var after = ReactionAlgebra.Converge(before: 0, target: 100, rate: 1.0, coefficientProduct: 10.0);
        Assert.Equal(ReactionTuning.MaxConvergence * 100.0, after, 6);
    }

    /// <summary>Repeatedly applying the same reagent must converge toward it and stop, never
    /// ratchet past it — the counter to A→B→A grinding loops (§17).</summary>
    [Fact]
    public void RepeatedApplication_ConvergesAndNeverOvershoots()
    {
        var properties = Properties();
        var process = Processes().GetById("process.forge_infusion");
        var reagent = Props(("heat", 80), ("instability", 90), ("affinity", 50));
        var state = Props(("affinity", 60), ("hardness", 50), ("mass", 50));

        for (var step = 0; step < 40; step++)
        {
            state = ReactionAlgebra.ApplyReagent(state, reagent, process, properties, 100).Properties;
            Assert.True(state.Get("heat") <= 80.0001, $"heat overshot the reagent at step {step}.");
        }

        Assert.True(state.Get("heat") > 70, "forty forge steps should have driven heat most of the way.");
    }

    // ---- §8.3 off-channel: the anti-accumulation rule --------------------------------------

    /// <summary>Off-channel reactive properties receive nothing and drift toward zero, so a
    /// material focuses along the channel rather than collecting everything it ever touched.</summary>
    [Fact]
    public void OffChannelReactiveProperties_DiluteAndReceiveNothing()
    {
        var result = ReactionAlgebra.ApplyReagent(
            Props(("toxicity", 60), ("affinity", 50)),
            Props(("toxicity", 100), ("instability", 80)),
            Processes().GetById("process.forge_infusion"), // channel: heat/hardness/affinity
            Properties(),
            substrateIntegrity: 100);

        Assert.True(result.Properties.Get("toxicity") < 60, "off-channel toxicity should have diluted.");
        Assert.True(result.Properties.Get("toxicity") > 50, "dilution is gentle, not a wipe.");
    }

    /// <summary>§8.3: "an alloy does get a bit heavier if you add heavy things."</summary>
    [Fact]
    public void OffChannelStructuralProperties_BlendTowardTheMassWeightedMixture()
    {
        var properties = Properties();
        var quench = Processes().GetById("process.quench"); // conductivity is off-channel

        var withConductor = ReactionAlgebra.ApplyReagent(
            Props(("conductivity", 20), ("mass", 50), ("affinity", 50)),
            Props(("conductivity", 100), ("mass", 50)),
            quench, properties, 100);

        var withInsulator = ReactionAlgebra.ApplyReagent(
            Props(("conductivity", 20), ("mass", 50), ("affinity", 50)),
            Props(("conductivity", 0), ("mass", 50)),
            quench, properties, 100);

        Assert.True(withConductor.Properties.Get("conductivity") > 20, "a conductive reagent should raise it.");
        Assert.True(withInsulator.Properties.Get("conductivity") < 20, "a non-conductive reagent should lower it.");
    }

    /// <summary>
    /// The accumulation counter, tested the way it actually matters: a long chain of varied
    /// reagents through varied processes must not leave a material carrying everything.
    /// </summary>
    [Fact]
    public void ALongVariedChain_DoesNotAccumulateAMuddyVector()
    {
        var properties = Properties();
        var processes = Processes();
        var materials = Materials();
        var reagents = new[]
        {
            "material.ember_core", "material.stormglass", "material.oak_bark",
            "material.granite", "material.ember_sap",
        };
        var order = new[] { "process.forge_infusion", "process.quench", "process.alloy", "process.smelt" };

        var state = materials.GetById("material.iron_ingot").BaseProperties;
        for (var step = 0; step < 12; step++)
        {
            state = ReactionAlgebra.ApplyReagent(
                state,
                materials.GetById(reagents[step % reagents.Length]).BaseProperties,
                processes.GetById(order[step % order.Length]),
                properties,
                substrateIntegrity: 100).Properties;
        }

        Assert.True(state.Count <= 12, $"a generation-12 material carries {state.Count} properties: {string.Join(", ", state.Keys)}");
        Assert.All(state.AsDictionary().Values, v => Assert.InRange(v, 0.0, 100.0));
    }

    [Fact]
    public void PropertiesBelowTheirFloor_ArePrunedToZero()
    {
        var result = ReactionAlgebra.ApplyReagent(
            Props(("toxicity", 3), ("affinity", 50), ("mass", 40)),
            Props(("instability", 50)),
            Processes().GetById("process.forge_infusion"),
            Properties(),
            substrateIntegrity: 100);

        Assert.False(result.Properties.Has("toxicity"));
        Assert.Contains(result.Changes, c => c.Kind == PropertyChangeKind.Pruned && c.Property == "toxicity");
    }

    // ---- §8.5 opposition -------------------------------------------------------------------

    /// <summary>Only the asymmetry survives — you cannot stockpile opposites.</summary>
    [Fact]
    public void OpposedProperties_AnnihilateLeavingOnlyTheAsymmetry()
    {
        var result = ReactionAlgebra.ApplyReagent(
            Props(("cold", 60), ("affinity", 100), ("instability", 100)),
            Props(("heat", 100), ("instability", 100)),
            Processes().GetById("process.forge_infusion"),
            Properties(),
            substrateIntegrity: 100,
            qualityMultiplier: 1.12);

        var heat = result.Properties.Get("heat");
        var cold = result.Properties.Get("cold");

        Assert.True(Math.Min(heat, cold) < 6, $"heat {heat:0.#} and cold {cold:0.#} should not coexist.");
        Assert.True(result.StrainReleased > 0, "annihilation must release strain.");
    }

    /// <summary>The released energy is what integrity is later charged for (§6.2a), so it has
    /// to be reported rather than silently discarded.</summary>
    [Fact]
    public void StrainReleased_IsZero_WhenNothingOpposes()
    {
        var result = ReactionAlgebra.ApplyReagent(
            Props(("affinity", 50), ("mass", 50)),
            Props(("heat", 80), ("instability", 60)),
            Processes().GetById("process.forge_infusion"),
            Properties(),
            substrateIntegrity: 100);

        Assert.Equal(0.0, result.StrainReleased);
    }

    // ---- Roles (§2.2, §2.3) -----------------------------------------------------------------

    /// <summary>Sourcing is inert in crafting — otherwise every craft alloys the difficulty of
    /// mining, and a hard-to-harvest ancestor would haunt its descendants forever.</summary>
    [Fact]
    public void SourcingProperties_PassThroughUntouched()
    {
        var result = ReactionAlgebra.ApplyReagent(
            Props(("harvest_resistance", 90), ("affinity", 50), ("mass", 50)),
            Props(("harvest_resistance", 10), ("instability", 50), ("heat", 90)),
            Processes().GetById("process.forge_infusion"),
            Properties(),
            substrateIntegrity: 100);

        Assert.Equal(90.0, result.Properties.Get("harvest_resistance"), 6);
    }

    /// <summary>
    /// Resistances are derived from what the material now is (§2.2), not carried. Keeping a
    /// stale authored value would make it an override for every later generation, so it is
    /// dropped and <see cref="ResistanceCalculator"/> stays the single read path.
    /// </summary>
    [Fact]
    public void ResponseProperties_AreDroppedSoResistanceStaysDerived()
    {
        var properties = Properties();
        var result = ReactionAlgebra.ApplyReagent(
            Materials().GetById("material.iron_ingot").BaseProperties, // authored heat_resistance 60
            Materials().GetById("material.ember_core").BaseProperties,
            Processes().GetById("process.forge_infusion"),
            properties,
            substrateIntegrity: 90);

        Assert.False(result.Properties.Has("heat_resistance"));
        Assert.Contains(result.Changes, c => c.Kind == PropertyChangeKind.DerivedResistance);

        // Resistance still answers, now from what the material became rather than an override.
        Assert.True(ResistanceCalculator.Resistance("heat", result.Properties, properties) > 0);
    }

    /// <summary>Dropping a derived resistance is bookkeeping, not a transformation the player
    /// caused, so it must not inflate the integrity charged for the step.</summary>
    [Fact]
    public void DroppedResistances_DoNotCountTowardStateDelta()
    {
        var properties = Properties();
        var process = Processes().GetById("process.forge_infusion");
        var reagent = Materials().GetById("material.ember_core").BaseProperties;

        var withResistance = ReactionAlgebra.ApplyReagent(
            Props(("affinity", 30), ("mass", 62), ("hardness", 65), ("heat_resistance", 60)),
            reagent, process, properties, 90);
        var without = ReactionAlgebra.ApplyReagent(
            Props(("affinity", 30), ("mass", 62), ("hardness", 65)),
            reagent, process, properties, 90);

        Assert.Equal(without.StateDelta, withResistance.StateDelta, 6);
    }

    // ---- Determinism (§12.5) -----------------------------------------------------------------

    /// <summary>Everything here is pure. Given the same inputs the result must be identical —
    /// the invariant the whole system's testability and reproducibility rests on.</summary>
    [Fact]
    public void TheAlgebraIsPure()
    {
        var properties = Properties();
        var iron = Materials().GetById("material.iron_ingot").BaseProperties;
        var core = Materials().GetById("material.ember_core").BaseProperties;
        var process = Processes().GetById("process.forge_infusion");

        var first = ReactionAlgebra.ApplyReagent(iron, core, process, properties, 90, 1.05);
        var second = ReactionAlgebra.ApplyReagent(iron, core, process, properties, 90, 1.05);

        Assert.Equal(first.Properties.AsDictionary(), second.Properties.AsDictionary());
        Assert.Equal(first.StrainReleased, second.StrainReleased);

        // The substrate it was handed must be untouched.
        Assert.Equal(65.0, iron.Get("hardness"));
        Assert.Equal(0.0, iron.Get("heat"));
    }

    /// <summary>
    /// §0 Decision 2: order-dependence is free, because step 2 acts on a different
    /// intermediate state. Nothing authored makes this true.
    /// </summary>
    [Fact]
    public void ReagentOrderChangesTheOutcome_WithNothingAuthoredToSaySo()
    {
        var properties = Properties();
        var process = Processes().GetById("process.forge_infusion");
        var substrate = Materials().GetById("material.iron_ingot").BaseProperties;
        var a = Materials().GetById("material.ember_core").BaseProperties;
        var b = Materials().GetById("material.stormglass").BaseProperties;

        var ab = ReactionAlgebra.ApplyReagent(
            ReactionAlgebra.ApplyReagent(substrate, a, process, properties, 90).Properties,
            b, process, properties, 80).Properties;
        var ba = ReactionAlgebra.ApplyReagent(
            ReactionAlgebra.ApplyReagent(substrate, b, process, properties, 90).Properties,
            a, process, properties, 80).Properties;

        Assert.NotEqual(ab.Get("heat"), ba.Get("heat"));
    }

    /// <summary>A total function: every combination produces a result, including the
    /// degenerate ones. No input may throw or produce a nonsense vector (§0 Decision 1).</summary>
    [Fact]
    public void EveryProcessAcceptsEveryMaterialWithoutFailing()
    {
        var properties = Properties();
        var materials = Materials().GetAll().Take(60).ToList();
        var processes = Processes().GetAll();

        foreach (var process in processes)
        foreach (var substrate in materials.Take(12))
        foreach (var reagent in materials.TakeLast(12))
        {
            var result = ReactionAlgebra.ApplyReagent(
                substrate.BaseProperties, reagent.BaseProperties, process, properties, 100);

            Assert.All(result.Properties.AsDictionary().Values, v => Assert.InRange(v, 0.0, 100.0));
            Assert.True(result.StrainReleased >= 0.0);
            Assert.True(result.StateDelta >= 0.0);
        }
    }
}
