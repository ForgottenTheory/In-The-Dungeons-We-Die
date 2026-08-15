using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The whole §8.7 pipeline, end to end (docs/emergent-item-system.md).
///
/// <para>§20's claim is that <b>P1 alone is a playable, genuinely emergent system</b> — "if
/// pure convergence + integrity + naming + stacking isn't already interesting to play with,
/// adding traits and signatures will not save it." These tests are where that claim is
/// checked: real content, real inventory, no authored recipes anywhere.</para>
/// </summary>
public class ReactionEngineTests
{
    private sealed class Harness
    {
        public Harness(int professionLevel = 99, int seed = 12345)
        {
            Content = new ContentBundle
            {
                Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
                Properties = TestPaths.LoadStore<PropertyDefinition>("properties"),
                Processes = TestPaths.LoadStore<ProcessDefinition>("processes"),
                Byproducts = TestPaths.LoadStore<ByproductDefinition>("byproducts"),
                NameGrammar = TestPaths.LoadStore<NameWordDefinition>("name_grammar"),
            };

            Inventory = new Inventory();
            Profiles = new MaterialProfileResolver(Content.Properties);
            Registry = new EmergentRegistry(Content.Materials);

            Engine = new ReactionEngine(
                Content,
                () => Inventory,
                Profiles,
                Registry,
                new NameGenerator(Content.Materials, Content.Properties, Content.NameGrammar),
                new TagDeriver(Content.Properties),
                new ByproductResolver(Content.Byproducts),
                _ => professionLevel,
                new SeededRandom(seed));
        }

        public ContentBundle Content { get; }
        public Inventory Inventory { get; }
        public MaterialProfileResolver Profiles { get; }
        public EmergentRegistry Registry { get; }
        public ReactionEngine Engine { get; }

        public Harness With(params string[] materialIds)
        {
            foreach (var id in materialIds)
                Inventory.Add(id, 10);
            return this;
        }

        public CraftOutcome Craft(string process, string substrate, params string[] reagents) =>
            Engine.Resolve(new CraftRequest(process, substrate, reagents));
    }

    // ---- A craft actually works ---------------------------------------------------------

    [Fact]
    public void ACraftConsumesItsInputsAndProducesAStackableNamedMaterial()
    {
        var harness = new Harness().With("material.iron_ingot", "material.ember_core");

        var outcome = harness.Craft("process.forge_infusion", "material.iron_ingot", "material.ember_core");

        Assert.True(outcome.Success, outcome.Failure.ToString());
        Assert.True(outcome.IsFirstDiscovery);
        Assert.StartsWith("emergent.", outcome.ResultItemId);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ResultName));

        Assert.Equal(9, harness.Inventory.GetQuantity("material.iron_ingot"));
        Assert.Equal(9, harness.Inventory.GetQuantity("material.ember_core"));
        Assert.Equal(1, harness.Inventory.GetQuantity(outcome.ResultItemId!));
    }

    /// <summary>§0 Decision 3: identical results stack, so inventory and save stay small.</summary>
    [Fact]
    public void RepeatingACraftStacksInsteadOfMintingSomethingNew()
    {
        var harness = new Harness().With("material.iron_ingot", "material.ember_core");

        var first = harness.Craft("process.forge_infusion", "material.iron_ingot", "material.ember_core");
        var second = harness.Craft("process.forge_infusion", "material.iron_ingot", "material.ember_core");

        Assert.Equal(first.ResultItemId, second.ResultItemId);
        Assert.True(first.IsFirstDiscovery);
        Assert.False(second.IsFirstDiscovery);
        Assert.Equal(2, harness.Inventory.GetQuantity(first.ResultItemId!));
        Assert.Equal(1, harness.Registry.Count);
    }

    /// <summary>
    /// The recursion the whole design exists for: a craft's output is an ordinary input to the
    /// next craft, with nothing special-cased for it being generated rather than authored.
    /// </summary>
    [Fact]
    public void AnEmergentResultIsAnOrdinaryInputToTheNextCraft()
    {
        var harness = new Harness().With("material.iron_ingot", "material.ember_core", "material.stormglass");

        var first = harness.Craft("process.forge_infusion", "material.iron_ingot", "material.ember_core");
        var second = harness.Craft("process.forge_infusion", first.ResultItemId!, "material.stormglass");

        Assert.True(second.Success, second.Failure.ToString());
        Assert.NotEqual(first.ResultItemId, second.ResultItemId);

        var profile = harness.Content.Materials.GetById(second.ResultItemId!).Profile!;
        Assert.Equal(3, profile.Generation);
        Assert.Contains(first.ResultItemId, profile.Lineage.ParentSignatures);
        Assert.Equal("material.iron_ingot", profile.Lineage.DominantRoot?.RootId);
    }

    // ---- The gate (§8.7 step 1) ------------------------------------------------------------

    [Theory]
    [InlineData("process.nope", "material.iron_ingot", CraftFailure.UnknownProcess)]
    [InlineData("process.forge_infusion", "material.nope", CraftFailure.UnknownSubstrate)]
    public void UnknownContentIsRejected(string process, string substrate, CraftFailure expected)
    {
        Assert.Equal(expected, new Harness().Craft(process, substrate, "material.ember_core").Failure);
    }

    [Fact]
    public void MissingMaterialsAreRejectedWithoutConsumingAnything()
    {
        var harness = new Harness().With("material.iron_ingot");

        var outcome = harness.Craft("process.forge_infusion", "material.iron_ingot", "material.ember_core");

        Assert.Equal(CraftFailure.MissingInputs, outcome.Failure);
        Assert.Equal(10, harness.Inventory.GetQuantity("material.iron_ingot"));
    }

    /// <summary>§7.2 <c>substrate_tags</c>: you cannot smelt a leaf.</summary>
    [Fact]
    public void AProcessRejectsASubstrateItCannotWork()
    {
        var harness = new Harness().With("material.sageleaf", "material.ember_core");

        var outcome = harness.Craft("process.forge_infusion", "material.sageleaf", "material.ember_core");

        Assert.Equal(CraftFailure.SubstrateRejected, outcome.Failure);
    }

    [Fact]
    public void AProcessRejectsACrafterWhoseSkillIsTooLow()
    {
        var harness = new Harness(professionLevel: 1).With("material.iron_ingot", "material.ember_core");

        Assert.Equal(
            CraftFailure.ProfessionTooLow,
            harness.Craft("process.forge_infusion", "material.iron_ingot", "material.ember_core").Failure);
    }

    /// <summary>Grind is the universal prep step and is deliberately ungated.</summary>
    [Fact]
    public void AnUngatedProcessWorksAtAnySkill()
    {
        var harness = new Harness(professionLevel: 1).With("material.granite", "material.iron_ore");

        Assert.True(harness.Craft("process.grind", "material.granite", "material.iron_ore").Success);
    }

    // ---- Order is the mechanic (§0 Decision 2) ------------------------------------------------

    /// <summary>
    /// Two reagents in either order, through the same process, must produce genuinely
    /// different materials — with nothing authored anywhere to say so.
    /// </summary>
    [Fact]
    public void ReagentOrderProducesDifferentMaterials()
    {
        var forward = new Harness().With("material.iron_ingot", "material.ember_core", "material.stormglass")
            .Craft("process.forge_infusion", "material.iron_ingot", "material.ember_core", "material.stormglass");

        var reverse = new Harness().With("material.iron_ingot", "material.ember_core", "material.stormglass")
            .Craft("process.forge_infusion", "material.iron_ingot", "material.stormglass", "material.ember_core");

        Assert.True(forward.Success && reverse.Success);
        Assert.NotEqual(forward.ResultItemId, reverse.ResultItemId);
    }

    /// <summary>§7.2's claim: the process choice is a first-class player decision.</summary>
    [Fact]
    public void ProcessChoiceProducesDifferentMaterials()
    {
        var steeped = new Harness().With("material.iron_ingot", "material.ember_sap")
            .Craft("process.steep", "material.iron_ingot", "material.ember_sap");
        var ground = new Harness().With("material.iron_ingot", "material.ember_sap")
            .Craft("process.grind", "material.iron_ingot", "material.ember_sap");

        Assert.NotEqual(steeped.ResultItemId, ground.ResultItemId);
    }

    // ---- Tag derivation (§4.2) ----------------------------------------------------------------

    [Fact]
    public void TheProcessAssertsTheResultingFormAndState()
    {
        var harness = new Harness().With("material.iron_ingot", "material.ember_sap");
        var outcome = harness.Craft("process.steep", "material.iron_ingot", "material.ember_sap");

        var tags = harness.Content.Materials.GetById(outcome.ResultItemId!).Tags;

        Assert.Contains("form:liquid", tags);
        Assert.Contains("state:extract", tags);
        Assert.DoesNotContain("form:metal", tags);  // form:* was cleared before the new form was set
        Assert.DoesNotContain("state:processed", tags);
    }

    /// <summary>
    /// §4.2 source 2: a result that ends up genuinely toxic is tagged venomous, however it got
    /// there. One steep does not reach the threshold — convergence only closes a fraction of
    /// the gap — so this concentrates it the way a player would, and checks the tag appears
    /// exactly when the property crosses.
    /// </summary>
    [Fact]
    public void StateThresholdsClassifyTheResult()
    {
        var harness = new Harness();
        harness.Inventory.Add("material.sageleaf", 200);
        harness.Inventory.Add("material.scorpion_queen_venom", 200);

        var current = "material.sageleaf";
        MaterialDefinition? result = null;

        for (var step = 0; step < 12; step++)
        {
            var outcome = harness.Craft("process.steep", current, "material.scorpion_queen_venom");
            Assert.True(outcome.Success, outcome.Failure.ToString());
            if (outcome.WasDestroyed)
                break;

            current = outcome.ResultItemId!;
            harness.Inventory.Add(current, 5);
            result = harness.Content.Materials.GetById(current);

            var toxicity = result.Profile!.Properties.Get("toxicity");
            Assert.Equal(toxicity >= 55, result.Tags.Contains("class:venomous"));

            if (toxicity >= 55)
                return;
        }

        Assert.Fail($"steeping in venom never became venomous (reached {result?.Profile!.Properties.Get("toxicity"):0}).");
    }

    /// <summary>§4.2: tag count stays bounded — the reason tags are derived, not inherited.</summary>
    [Fact]
    public void TagCountStaysBoundedAcrossGenerations()
    {
        var harness = new Harness().With(
            "material.iron_ingot", "material.ember_core", "material.stormglass", "material.granite");

        var current = "material.iron_ingot";
        for (var i = 0; i < 5; i++)
        {
            var outcome = harness.Craft("process.forge_infusion", current, "material.ember_core");
            if (!outcome.Success)
                break;
            current = outcome.ResultItemId!;

            Assert.True(harness.Content.Materials.GetById(current).Tags.Count <= 9,
                $"generation {i + 2} carries {harness.Content.Materials.GetById(current).Tags.Count} tags.");
        }
    }

    // ---- §6.2c projection and destruction --------------------------------------------------

    /// <summary>Destruction is never a surprise: the projection is available before committing,
    /// and it matches what the craft actually does.</summary>
    [Fact]
    public void TheProjectionMatchesWhatTheCraftDoes()
    {
        var harness = new Harness().With("material.iron_ingot", "material.ember_core");

        var projection = harness.Engine.Project(
            new CraftRequest("process.forge_infusion", "material.iron_ingot", new[] { "material.ember_core" }));

        Assert.True(projection.CanCraft);
        Assert.True(projection.WouldBeFirstDiscovery);
        Assert.False(projection.WarnsOfDestruction);
        Assert.True(projection.ProjectedIntegrity() < 90, "a craft has to cost something.");

        // Projecting must not consume, register, or otherwise change anything.
        Assert.Equal(10, harness.Inventory.GetQuantity("material.iron_ingot"));
        Assert.Equal(0, harness.Registry.Count);

        var outcome = harness.Craft("process.forge_infusion", "material.iron_ingot", "material.ember_core");
        Assert.Equal(projection.ProjectedName, outcome.ResultName);
    }

    [Fact]
    public void TheProjectionReportsWhyACraftCannotProceed()
    {
        var projection = new Harness().Engine.Project(
            new CraftRequest("process.forge_infusion", "material.sageleaf", new[] { "material.ember_core" }));

        Assert.False(projection.CanCraft);
        Assert.Equal(CraftFailure.SubstrateRejected, projection.Failure);
    }

    /// <summary>
    /// §6.2c end to end: push a material until integrity runs out. It must be destroyed rather
    /// than lingering at zero, and it must leave byproducts — a blown craft is a setback and a
    /// consolation prize, not a zero.
    /// </summary>
    [Fact]
    public void PushingAMaterialToZeroIntegrityDestroysItAndLeavesByproducts()
    {
        var harness = new Harness();
        harness.Inventory.Add("material.iron_ingot", 500);
        harness.Inventory.Add("material.ember_core", 500);

        var current = "material.iron_ingot";
        CraftOutcome? destruction = null;

        for (var step = 0; step < 400; step++)
        {
            var outcome = harness.Craft("process.forge_infusion", current, "material.ember_core");
            if (!outcome.Success)
                break;

            if (outcome.WasDestroyed)
            {
                destruction = outcome;
                break;
            }

            current = outcome.ResultItemId!;
            harness.Inventory.Add(current, 5); // keep enough on hand to continue pushing
        }

        Assert.NotNull(destruction);
        Assert.Null(destruction!.ResultItemId);
        Assert.NotEmpty(destruction.Byproducts);
        Assert.Equal("material.slag", destruction.Byproducts[0].ItemId); // form:metal → slag
        Assert.True(harness.Inventory.GetQuantity("material.slag") > 0);
        Assert.Contains(destruction.Log.Entries, e => e.Kind == ReactionLogKind.Destruction);
    }

    // ---- Determinism (§12.5) -------------------------------------------------------------------

    [Fact]
    public void TheSameCraftFromTheSameSeedReachesTheSameMaterial()
    {
        static CraftOutcome Run() => new Harness(seed: 999)
            .With("material.iron_ingot", "material.ember_core")
            .Craft("process.forge_infusion", "material.iron_ingot", "material.ember_core");

        Assert.Equal(Run().ResultItemId, Run().ResultItemId);
        Assert.Equal(Run().ResultName, Run().ResultName);
    }

    // ---- §20's claim: this is playable on its own ------------------------------------------------

    /// <summary>
    /// A broad sweep of the kind of experimenting a player actually does. Every craft must
    /// succeed or fail for a stated reason, produce a real named stackable material, and never
    /// throw — the algebra is a total function (§0 Decision 1).
    /// </summary>
    [Fact]
    public void BroadExperimentationAlwaysProducesRealMaterials()
    {
        var harness = new Harness();
        var substrates = new[] { "material.iron_ingot", "material.granite", "material.oak_log", "material.sageleaf" };
        var reagents = new[] { "material.ember_core", "material.ember_sap", "material.oak_bark", "material.springwater" };

        foreach (var id in substrates.Concat(reagents))
            harness.Inventory.Add(id, 200);

        var names = new HashSet<string>(StringComparer.Ordinal);
        var crafted = 0;

        foreach (var process in harness.Content.Processes.GetAll())
        foreach (var substrate in substrates)
        foreach (var reagent in reagents)
        {
            var outcome = harness.Craft(process.Id, substrate, reagent);
            if (!outcome.Success)
            {
                // The only acceptable refusals are the authored gates.
                Assert.Contains(outcome.Failure, new[] { CraftFailure.SubstrateRejected, CraftFailure.MissingInputs });
                continue;
            }

            if (outcome.WasDestroyed)
                continue;

            Assert.False(string.IsNullOrWhiteSpace(outcome.ResultName));
            Assert.True(outcome.ResultName.Split(' ').Length <= 3, $"'{outcome.ResultName}' is too long.");
            Assert.True(harness.Inventory.GetQuantity(outcome.ResultItemId!) > 0);
            names.Add(outcome.ResultName);
            crafted++;
        }

        Assert.True(crafted > 50, $"only {crafted} crafts succeeded across the sweep.");

        // Distinct experiments should mostly yield distinct materials, or the space is collapsing.
        Assert.True(names.Count > crafted * 0.5,
            $"{crafted} crafts produced only {names.Count} distinct names.");
    }
}

internal static class ProjectionExtensions
{
    public static int ProjectedIntegrity(this CraftProjection projection) => projection.Integrity.ProjectedIntegrity;
}
