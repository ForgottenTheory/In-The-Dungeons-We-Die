using Dungeons.Content;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// P1 slice 1 of the emergent item system: every material kind has a
/// <see cref="MaterialState"/>. The ~470 authored materials carry no material strength/workability, so
/// theirs is derived from data they already have (docs/emergent-item-system.md §6). These
/// tests pin the derivation's <i>shape</i> — the relationships that must hold — rather than
/// exact constants, which are explicitly first-pass and expected to be tuned.
/// </summary>
public class MaterialStateTests
{
    private static MaterialStateResolver MaterialStates() =>
        new(TestPaths.LoadStore<PropertyDefinition>("properties"));

    private static DataStore<MaterialDefinition> Materials() =>
        TestPaths.LoadStore<MaterialDefinition>("materials");

    private static MaterialDefinition Material(
        string id,
        IReadOnlyList<string> tags,
        IDictionary<string, double>? properties = null) =>
        new()
        {
            Id = id,
            Name = id,
            Tags = tags,
            Properties = new Dictionary<string, double>(properties ?? new Dictionary<string, double>()),
        };

    // ---- Workability (§6.2) ---------------------------------------------------------------

    [Theory]
    [InlineData("raw", 100)]
    [InlineData("processed", 90)]
    [InlineData("refined", 90)]
    [InlineData("alloy", 85)]
    [InlineData("spent", 60)]
    public void Integrity_ComesFromTheStateTag(string state, int expected)
    {
        Assert.Equal(expected, MaterialStateResolver.DeriveWorkability(new[] { $"state:{state}" }));
    }

    [Fact]
    public void Integrity_FallsBackToFullBudget_WhenNoStateTagIsPresent()
    {
        Assert.Equal(
            MaterialStateTuning.DefaultWorkability,
            MaterialStateResolver.DeriveWorkability(new[] { "form:metal" }));
    }

    // ---- MaterialStrength (§6.1) -----------------------------------------------------------------

    [Fact]
    public void Potency_RisesWithRarity_WhenPropertiesMatch()
    {
        var materialStates = MaterialStates();
        var properties = new Dictionary<string, double> { ["hardness"] = 50 };

        var common = materialStates.StateOf(Material("m.common", new[] { "rarity:common" }, properties)).MaterialStrength;
        var rare = materialStates.StateOf(Material("m.rare", new[] { "rarity:rare" }, properties)).MaterialStrength;
        var exceptional = materialStates.StateOf(Material("m.exc", new[] { "rarity:exceptional" }, properties)).MaterialStrength;

        Assert.True(common < rare, $"common {common} should be under rare {rare}");
        Assert.True(rare < exceptional, $"rare {rare} should be under exceptional {exceptional}");
    }

    [Fact]
    public void Potency_RisesWithExpressiveProperties_WhenRarityMatches()
    {
        var materialStates = MaterialStates();

        var weak = materialStates.StateOf(Material("m.weak", new[] { "rarity:common" },
            new Dictionary<string, double> { ["hardness"] = 20 })).MaterialStrength;
        var strong = materialStates.StateOf(Material("m.strong", new[] { "rarity:common" },
            new Dictionary<string, double> { ["hardness"] = 90 })).MaterialStrength;

        Assert.True(weak < strong, $"weak {weak} should be under strong {strong}");
    }

    /// <summary>
    /// §6.1's stated intent: a high-material strength mundane material must be able to beat a
    /// low-material strength exotic one, which is what keeps base resources relevant forever. If
    /// rarity ever dominates the formula, this fails.
    /// </summary>
    [Fact]
    public void Potency_LetsAStrongCommonMaterialBeatAWeakExceptionalOne()
    {
        var materialStates = MaterialStates();

        var strongCommon = materialStates.StateOf(Material("m.granite", new[] { "rarity:common" },
            new Dictionary<string, double> { ["hardness"] = 68, ["mass"] = 66 })).MaterialStrength;
        var weakExceptional = materialStates.StateOf(Material("m.wisp", new[] { "rarity:exceptional" },
            new Dictionary<string, double> { ["mass"] = 4, ["affinity"] = 10 })).MaterialStrength;

        Assert.True(strongCommon > weakExceptional,
            $"strong common {strongCommon} should beat weak exceptional {weakExceptional}");
    }

    /// <summary>
    /// Sourcing properties describe how hard a thing was to <i>obtain</i> (§2.2) and must be
    /// inert here — otherwise material strength would "alloy the difficulty of mining."
    /// </summary>
    [Fact]
    public void Potency_IgnoresSourcingAndResponseProperties()
    {
        var materialStates = MaterialStates();

        var plain = materialStates.StateOf(Material("m.plain", new[] { "rarity:common" },
            new Dictionary<string, double> { ["hardness"] = 30 })).MaterialStrength;
        var padded = materialStates.StateOf(Material("m.padded", new[] { "rarity:common" },
            new Dictionary<string, double>
            {
                ["hardness"] = 30,
                ["harvest_resistance"] = 100,
                ["heat_resistance"] = 100,
            })).MaterialStrength;

        Assert.Equal(plain, padded);
    }

    /// <summary>
    /// The derivation was calibrated against the three materials §19 gives explicit
    /// potencies for. The tolerance is wide on purpose: the constants are first-pass, but if
    /// a tuning change moves a shipped material this far from the worked example, the example
    /// in the design doc no longer describes the game and one of the two must be updated.
    /// </summary>
    [Theory]
    [InlineData("material.iron_ingot", 40)]
    [InlineData("material.ember_sap", 45)]
    [InlineData("material.ember_core", 70)]
    public void Potency_TracksTheWorkedExample(string materialId, int specStrength)
    {
        var materialStrength = MaterialStates().StateOf(Materials().GetById(materialId)).MaterialStrength;
        Assert.InRange(materialStrength, specStrength - 8, specStrength + 8);
    }

    /// <summary>
    /// §6.1's ceiling rule (<c>max(input.material strength) + 8</c>) only means something if authored
    /// materials leave headroom above them — the top of the material strength range has to be earned
    /// by refinement, not handed out at the quarry.
    /// </summary>
    [Fact]
    public void AuthoredLibrary_LeavesHeadroomForRefinementToClimbInto()
    {
        var materialStates = MaterialStates();
        var potencies = Materials().GetAll().Select(m => materialStates.StateOf(m).MaterialStrength).ToList();

        Assert.True(potencies.Max() <= 75, $"the strongest authored material is {potencies.Max()}; no headroom left.");
        Assert.True(potencies.Max() - potencies.Min() >= 25, "the authored library is too flat to differentiate reagents.");
    }

    [Fact]
    public void Potency_StaysWithinRange_ForEveryShippedMaterial()
    {
        var materialStates = MaterialStates();

        foreach (var material in Materials().GetAll())
        {
            var state = materialStates.StateOf(material);
            Assert.InRange(state.MaterialStrength, 1, 100);
            Assert.InRange(state.Workability, 1, 100);
        }
    }

    // ---- Overrides ----------------------------------------------------------------------

    [Fact]
    public void AuthoredOverrides_WinOverTheDerivation()
    {
        var materialStates = MaterialStates();
        var definition = new MaterialDefinition
        {
            Id = "m.override",
            Name = "Override",
            Tags = new[] { "rarity:common", "state:raw" },
            Properties = new Dictionary<string, double> { ["hardness"] = 30 },
            MaterialStrength = 77,
            Workability = 42,
        };

        var state = materialStates.StateOf(definition);

        Assert.Equal(77, state.MaterialStrength);
        Assert.Equal(42, state.Workability);
    }

    /// <summary>No authored material overrides these yet; the whole library is derived. If one
    /// ever does, that is a deliberate act and this test should be updated with the reason.</summary>
    [Fact]
    public void NoShippedMaterial_OverridesItsProfile()
    {
        foreach (var material in Materials().GetAll())
        {
            Assert.Null(material.MaterialStrength);
            Assert.Null(material.Workability);
        }
    }

    // ---- Profile shape ------------------------------------------------------------------

    [Fact]
    public void AuthoredMaterials_AreTheirOwnArchetypeAtGenerationOne()
    {
        var materialStates = MaterialStates();
        var state = materialStates.StateOf(Materials().GetById("material.iron_ingot"));

        Assert.Equal("material.iron_ingot", state.Signature);
        Assert.Equal(1, state.Generation);
        Assert.Equal("material.iron_ingot", state.Lineage.DominantRoot?.RootId);
        Assert.Equal(1.0, state.Lineage.DominantRoot?.Weight);
        Assert.Empty(state.Lineage.ParentSignatures);
        Assert.False(state.IsDestroyed);
    }

    /// <summary>An emergent archetype is born with its state and must be returned unchanged
    /// — the derivation is only a fallback for the authored library.</summary>
    [Fact]
    public void EmergentDefinitions_KeepTheirOwnProfile()
    {
        var materialStates = MaterialStates();
        var state = new MaterialState(
            Properties: PropertySet.FromValues(new Dictionary<string, double> { ["heat"] = 35 }),
            MaterialStrength: 49,
            Workability: 72,
            Lineage: new Lineage(
                new[] { new RootShare("material.iron_ingot", 1.0) },
                Generation: 2,
                CraftingActionId: "process.forge_infusion",
                ParentSignatures: new[] { "material.iron_ingot" }),
            Signature: "emergent.7f3a91c4");

        var definition = new MaterialDefinition
        {
            Id = "emergent.7f3a91c4",
            Name = "Emberveined Iron",
            Tags = new[] { "state:alloy", "form:metal" },
            State = state,
        };

        Assert.Same(state, materialStates.StateOf(definition));
        Assert.Equal(2, materialStates.StateOf(definition).Generation);
    }

    /// <summary>The reaction engine will resolve profiles per craft step; resolution must be
    /// cheap and, more importantly, stable.</summary>
    [Fact]
    public void Resolution_IsDeterministicAndCached()
    {
        var materialStates = MaterialStates();
        var iron = Materials().GetById("material.iron_ingot");

        Assert.Same(materialStates.StateOf(iron), materialStates.StateOf(iron));
        Assert.Equal(
            MaterialStates().StateOf(iron).MaterialStrength,
            MaterialStates().StateOf(iron).MaterialStrength);
    }
}
