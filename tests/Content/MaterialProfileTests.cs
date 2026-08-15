using Dungeons.Content;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// P1 slice 1 of the emergent item system: every material kind has a
/// <see cref="MaterialProfile"/>. The ~470 authored materials carry no potency/integrity, so
/// theirs is derived from data they already have (docs/emergent-item-system.md §6). These
/// tests pin the derivation's <i>shape</i> — the relationships that must hold — rather than
/// exact constants, which are explicitly first-pass and expected to be tuned.
/// </summary>
public class MaterialProfileTests
{
    private static MaterialProfileResolver Resolver() =>
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

    // ---- Integrity (§6.2) ---------------------------------------------------------------

    [Theory]
    [InlineData("raw", 100)]
    [InlineData("processed", 90)]
    [InlineData("refined", 90)]
    [InlineData("alloy", 85)]
    [InlineData("spent", 60)]
    public void Integrity_ComesFromTheStateTag(string state, int expected)
    {
        Assert.Equal(expected, MaterialProfileResolver.DeriveIntegrity(new[] { $"state:{state}" }));
    }

    [Fact]
    public void Integrity_FallsBackToFullBudget_WhenNoStateTagIsPresent()
    {
        Assert.Equal(
            MaterialProfileTuning.DefaultIntegrity,
            MaterialProfileResolver.DeriveIntegrity(new[] { "form:metal" }));
    }

    // ---- Potency (§6.1) -----------------------------------------------------------------

    [Fact]
    public void Potency_RisesWithRarity_WhenPropertiesMatch()
    {
        var resolver = Resolver();
        var properties = new Dictionary<string, double> { ["hardness"] = 50 };

        var common = resolver.Resolve(Material("m.common", new[] { "rarity:common" }, properties)).Potency;
        var rare = resolver.Resolve(Material("m.rare", new[] { "rarity:rare" }, properties)).Potency;
        var exceptional = resolver.Resolve(Material("m.exc", new[] { "rarity:exceptional" }, properties)).Potency;

        Assert.True(common < rare, $"common {common} should be under rare {rare}");
        Assert.True(rare < exceptional, $"rare {rare} should be under exceptional {exceptional}");
    }

    [Fact]
    public void Potency_RisesWithExpressiveProperties_WhenRarityMatches()
    {
        var resolver = Resolver();

        var weak = resolver.Resolve(Material("m.weak", new[] { "rarity:common" },
            new Dictionary<string, double> { ["hardness"] = 20 })).Potency;
        var strong = resolver.Resolve(Material("m.strong", new[] { "rarity:common" },
            new Dictionary<string, double> { ["hardness"] = 90 })).Potency;

        Assert.True(weak < strong, $"weak {weak} should be under strong {strong}");
    }

    /// <summary>
    /// §6.1's stated intent: a high-potency mundane material must be able to beat a
    /// low-potency exotic one, which is what keeps base resources relevant forever. If
    /// rarity ever dominates the formula, this fails.
    /// </summary>
    [Fact]
    public void Potency_LetsAStrongCommonMaterialBeatAWeakExceptionalOne()
    {
        var resolver = Resolver();

        var strongCommon = resolver.Resolve(Material("m.granite", new[] { "rarity:common" },
            new Dictionary<string, double> { ["hardness"] = 68, ["mass"] = 66 })).Potency;
        var weakExceptional = resolver.Resolve(Material("m.wisp", new[] { "rarity:exceptional" },
            new Dictionary<string, double> { ["mass"] = 4, ["affinity"] = 10 })).Potency;

        Assert.True(strongCommon > weakExceptional,
            $"strong common {strongCommon} should beat weak exceptional {weakExceptional}");
    }

    /// <summary>
    /// Sourcing properties describe how hard a thing was to <i>obtain</i> (§2.2) and must be
    /// inert here — otherwise potency would "alloy the difficulty of mining."
    /// </summary>
    [Fact]
    public void Potency_IgnoresSourcingAndResponseProperties()
    {
        var resolver = Resolver();

        var plain = resolver.Resolve(Material("m.plain", new[] { "rarity:common" },
            new Dictionary<string, double> { ["hardness"] = 30 })).Potency;
        var padded = resolver.Resolve(Material("m.padded", new[] { "rarity:common" },
            new Dictionary<string, double>
            {
                ["hardness"] = 30,
                ["harvest_resistance"] = 100,
                ["heat_resistance"] = 100,
            })).Potency;

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
    public void Potency_TracksTheWorkedExample(string materialId, int specPotency)
    {
        var potency = Resolver().Resolve(Materials().GetById(materialId)).Potency;
        Assert.InRange(potency, specPotency - 8, specPotency + 8);
    }

    /// <summary>
    /// §6.1's ceiling rule (<c>max(input.potency) + 8</c>) only means something if authored
    /// materials leave headroom above them — the top of the potency range has to be earned
    /// by refinement, not handed out at the quarry.
    /// </summary>
    [Fact]
    public void AuthoredLibrary_LeavesHeadroomForRefinementToClimbInto()
    {
        var resolver = Resolver();
        var potencies = Materials().GetAll().Select(m => resolver.Resolve(m).Potency).ToList();

        Assert.True(potencies.Max() <= 75, $"the strongest authored material is {potencies.Max()}; no headroom left.");
        Assert.True(potencies.Max() - potencies.Min() >= 25, "the authored library is too flat to differentiate reagents.");
    }

    [Fact]
    public void Potency_StaysWithinRange_ForEveryShippedMaterial()
    {
        var resolver = Resolver();

        foreach (var material in Materials().GetAll())
        {
            var profile = resolver.Resolve(material);
            Assert.InRange(profile.Potency, 1, 100);
            Assert.InRange(profile.Integrity, 1, 100);
        }
    }

    // ---- Overrides ----------------------------------------------------------------------

    [Fact]
    public void AuthoredOverrides_WinOverTheDerivation()
    {
        var resolver = Resolver();
        var definition = new MaterialDefinition
        {
            Id = "m.override",
            Name = "Override",
            Tags = new[] { "rarity:common", "state:raw" },
            Properties = new Dictionary<string, double> { ["hardness"] = 30 },
            Potency = 77,
            Integrity = 42,
        };

        var profile = resolver.Resolve(definition);

        Assert.Equal(77, profile.Potency);
        Assert.Equal(42, profile.Integrity);
    }

    /// <summary>No authored material overrides these yet; the whole library is derived. If one
    /// ever does, that is a deliberate act and this test should be updated with the reason.</summary>
    [Fact]
    public void NoShippedMaterial_OverridesItsProfile()
    {
        foreach (var material in Materials().GetAll())
        {
            Assert.Null(material.Potency);
            Assert.Null(material.Integrity);
        }
    }

    // ---- Profile shape ------------------------------------------------------------------

    [Fact]
    public void AuthoredMaterials_AreTheirOwnArchetypeAtGenerationOne()
    {
        var resolver = Resolver();
        var profile = resolver.Resolve(Materials().GetById("material.iron_ingot"));

        Assert.Equal("material.iron_ingot", profile.Signature);
        Assert.Equal(1, profile.Generation);
        Assert.Equal("material.iron_ingot", profile.Lineage.DominantRoot?.RootId);
        Assert.Equal(1.0, profile.Lineage.DominantRoot?.Weight);
        Assert.Empty(profile.Lineage.ParentSignatures);
        Assert.False(profile.IsDestroyed);
    }

    /// <summary>An emergent archetype is born with its profile and must be returned unchanged
    /// — the derivation is only a fallback for the authored library.</summary>
    [Fact]
    public void EmergentDefinitions_KeepTheirOwnProfile()
    {
        var resolver = Resolver();
        var profile = new MaterialProfile(
            Properties: PropertySet.FromValues(new Dictionary<string, double> { ["heat"] = 35 }),
            Potency: 49,
            Integrity: 72,
            Lineage: new Lineage(
                new[] { new RootShare("material.iron_ingot", 1.0) },
                Generation: 2,
                ProcessId: "process.forge_infusion",
                ParentSignatures: new[] { "material.iron_ingot" }),
            Signature: "emergent.7f3a91c4");

        var definition = new MaterialDefinition
        {
            Id = "emergent.7f3a91c4",
            Name = "Emberveined Iron",
            Tags = new[] { "state:alloy", "form:metal" },
            Profile = profile,
        };

        Assert.Same(profile, resolver.Resolve(definition));
        Assert.Equal(2, resolver.Resolve(definition).Generation);
    }

    /// <summary>The reaction engine will resolve profiles per craft step; resolution must be
    /// cheap and, more importantly, stable.</summary>
    [Fact]
    public void Resolution_IsDeterministicAndCached()
    {
        var resolver = Resolver();
        var iron = Materials().GetById("material.iron_ingot");

        Assert.Same(resolver.Resolve(iron), resolver.Resolve(iron));
        Assert.Equal(
            Resolver().Resolve(iron).Potency,
            Resolver().Resolve(iron).Potency);
    }
}
