using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// C1b — the §5/§8.4 essence layer: additive transfer with the anchor bonus, opposition
/// annihilating the overlap into strain, resonance capacity making "attune first, then
/// infuse" real, and essence joining identity and persistence.
/// </summary>
public class EssenceTests
{
    private static DataStore<EssenceDefinition> Essences(params EssenceDefinition[] defs)
    {
        var store = new DataStore<EssenceDefinition>();
        foreach (var def in defs)
            store.Add(def);
        return store;
    }

    private static EssenceDefinition Fire => new() { Id = "essence.fire", Name = "Fire", Anchor = "heat", Opposes = new[] { "frost" } };
    private static EssenceDefinition Frost => new() { Id = "essence.frost", Name = "Frost", Anchor = "cold" };

    private static CraftingActionDefinition Forge(double essenceRate = 0.45) => new()
    {
        Id = "process.test_forge", Name = "Test Forge",
        Medium = TransferMedium.Thermal, Severity = 0.5, EssenceRate = essenceRate,
        AffectedQualities = new[] { new AffectedQuality { Property = "heat", Rate = 0.8 } },
    };

    private static TransferCoefficients Coefficients(double acceptance = 0.5, double release = 0.8) =>
        new(acceptance, release, WorkabilityFactor: 1.0, Catalyst: 1.0, QualityMultiplier: 1.0);

    private static Dictionary<string, double> Vector(params (string Key, double Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    // ---- §8.4 transfer -------------------------------------------------------

    [Fact]
    public void TransferIsAdditive_AndTheAnchorChannelBonusApplies()
    {
        var store = Essences(Fire, Frost);

        // fire's anchor (heat) is in the channel: gain = 60 × 0.45 × 0.5 × 0.8 × 1.0 × 1.5.
        var anchored = EssenceAlgebra.Apply(
            Vector(), Vector(("fire", 60)), Forge(), Coefficients(), store);
        Assert.Equal(16.2, anchored.Essence["fire"], 3);

        // frost's anchor (cold) is not: same numbers without the ×1.5.
        var unanchored = EssenceAlgebra.Apply(
            Vector(), Vector(("frost", 60)), Forge(), Coefficients(), store);
        Assert.Equal(10.8, unanchored.Essence["frost"], 3);

        // Additive over an existing charge — essence never converges toward zero on its own.
        var stacked = EssenceAlgebra.Apply(
            Vector(("fire", 50)), Vector(("fire", 60)), Forge(), Coefficients(), store);
        Assert.Equal(66.2, stacked.Essence["fire"], 3);
    }

    /// <summary>§8.5 — opposition annihilates the overlap at the shared rate; the release is
    /// strain the workability cost feels; only the asymmetry survives. Authored one-sided here,
    /// deliberately — the pair must still fire.</summary>
    [Fact]
    public void OppositionAnnihilatesTheOverlap_EvenWhenAuthoredOneSided()
    {
        var store = Essences(Fire, Frost); // only fire declares the opposition

        var result = EssenceAlgebra.Apply(
            Vector(("fire", 40), ("frost", 25)), Vector(), Forge(essenceRate: 0), Coefficients(), store);

        // overlap 25 × 0.9 = 22.5 cancelled from both sides.
        Assert.Equal(17.5, result.Essence["fire"], 3);
        Assert.Equal(2.5, result.Essence["frost"], 3);
        Assert.Equal(22.5, result.StressReleased, 3);
    }

    [Fact]
    public void TraceEssenceIsPruned()
    {
        var result = EssenceAlgebra.Apply(
            Vector(("fire", 0.4)), Vector(), Forge(essenceRate: 0), Coefficients(), Essences(Fire, Frost));
        Assert.False(result.Essence.ContainsKey("fire"));
    }

    // ---- §5.3 capacity / §5.2.1 amplification --------------------------------

    [Fact]
    public void StrainIsEssenceBeyondResonanceCapacity_AndFeedsEffectiveInstability()
    {
        Assert.Equal(30, EssenceTuning.Capacity(20), 3);        // resonance × 1.5
        Assert.Equal(12, EssenceTuning.Stress(Vector(("fire", 42)), 20), 3);
        Assert.Equal(0, EssenceTuning.Stress(Vector(("fire", 30)), 20), 3);

        // §6.2b: instability + strain + lost-workability × 0.4.
        var calm = WorkabilityCalculator.EffectiveInstability(10, 100);
        var strained = WorkabilityCalculator.EffectiveInstability(10, 100, essenceStress: 12);
        Assert.Equal(calm + 12, strained, 3);
    }

    [Fact]
    public void ArcaneAmplifiesEssenceExpression()
    {
        Assert.Equal(36, EssenceTuning.Expression(60, 0), 3);    // mundane host: ×0.6
        Assert.Equal(60, EssenceTuning.Expression(60, 100), 3);  // fully charged: ×1.0
        Assert.Equal(48, EssenceTuning.Expression(60, 50), 3);
    }

    // ---- Identity ------------------------------------------------------------

    [Fact]
    public void EssenceJoinsTheSignature_AndAnEmptyVectorChangesNothing()
    {
        var lineage = new Lineage(
            new[] { new RootShare("material.iron_ore", 1.0) }, 2, "process.forge_infusion",
            new[] { "material.iron_ingot" });
        var bare = new MaterialState(PropertySet.Empty.With("hardness", 60), 40, 70, lineage, string.Empty);
        var tags = new[] { "form:metal" };

        Assert.EndsWith("|essence=", MaterialSignature.Canonical(bare, tags));

        var fire20 = bare with { Essence = Vector(("fire", 20)) };
        var fire22 = bare with { Essence = Vector(("fire", 22)) };  // same 5-bucket
        var fire40 = bare with { Essence = Vector(("fire", 40)) };

        Assert.Equal(MaterialSignature.Compute(fire20, tags), MaterialSignature.Compute(fire22, tags));
        Assert.NotEqual(MaterialSignature.Compute(fire20, tags), MaterialSignature.Compute(fire40, tags));
        Assert.NotEqual(MaterialSignature.Compute(bare, tags), MaterialSignature.Compute(fire20, tags));
    }

    // ---- End to end: infuse, strain, attune ----------------------------------

    private sealed class Harness
    {
        public Harness()
        {
            Content = new ContentBundle
            {
                Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
                Properties = TestPaths.LoadStore<PropertyDefinition>("properties"),
                CraftingActions = TestPaths.LoadStore<CraftingActionDefinition>("processes"),
                Byproducts = TestPaths.LoadStore<ByproductDefinition>("byproducts"),
                NameGrammar = TestPaths.LoadStore<NameWordDefinition>("name_grammar"),
                Traits = TestPaths.LoadStore<TraitDefinition>("traits"),
                Essences = TestPaths.LoadStore<EssenceDefinition>("essences"),
            };
            Inventory = new Inventory();
            Registry = new EmergentRegistry(Content.Materials);
            Engine = new MaterialTransformationEngine(
                Content, () => Inventory,
                new MaterialStateResolver(Content.Properties),
                Registry,
                new NameGenerator(Content.Materials, Content.Properties, Content.NameGrammar),
                new TagDeriver(Content.Properties),
                new ByproductResolver(Content.Byproducts),
                new TraitResolver(Content.Traits),
                _ => 99,
                new SeededRandom(4242));
        }

        public ContentBundle Content { get; }
        public Inventory Inventory { get; }
        public EmergentRegistry Registry { get; }
        public MaterialTransformationEngine Engine { get; }
    }

    /// <summary>The §19-style worked lesson, end to end: infusing fire essence into mundane
    /// iron strains the unworthy vessel (resonance 0 → capacity 0), and the log says so;
    /// Attuning with a Ley Crystal raises resonance, so the vessel becomes worthy.</summary>
    [Fact]
    public void InfuseStrainAttune_TheWholeLessonEndToEnd()
    {
        var harness = new Harness();
        harness.Inventory.Add("material.iron_ingot", 5);
        harness.Inventory.Add("material.ember_core", 5);
        harness.Inventory.Add("material.ley_crystal", 5);

        // 1. Infuse: fire essence arrives on iron; capacity 0 → the strain warning fires.
        var infused = harness.Engine.RunCraft(
            new CraftRequest("process.forge_infusion", "material.iron_ingot", new[] { "material.ember_core" }));
        Assert.True(infused.Success, infused.Failure.ToString());

        var profile = harness.Registry.All.Single(m => m.Id == infused.ResultItemId).State!;
        Assert.True(profile.Essence.GetValueOrDefault("fire") > 0, "fire essence should have transferred");
        Assert.Contains("essence.fire", infused.Log.ToString());     // the movement line renders
        Assert.Contains("Strained vessel", infused.Log.ToString());  // and the §5.3 warning

        // 2. Attune with a Ley Crystal: resonance rises toward the reagent's 80.
        var attuned = harness.Engine.RunCraft(
            new CraftRequest("process.attune", infused.ResultItemId!, new[] { "material.ley_crystal" }));
        Assert.True(attuned.Success, attuned.Failure.ToString());

        var attunedProfile = harness.Registry.All.Single(m => m.Id == attuned.ResultItemId).State!;
        Assert.True(
            attunedProfile.Properties.Get("resonance") > profile.Properties.Get("resonance"),
            $"Attune should raise resonance; trace:\n{attuned.Log}");
    }

    // ---- Persistence ---------------------------------------------------------

    [Fact]
    public void EssenceAndTraitsSurviveTheArchetypeRoundTrip()
    {
        var harness = new Harness();
        harness.Inventory.Add("material.iron_ingot", 5);
        harness.Inventory.Add("material.ember_core", 5);

        var outcome = harness.Engine.RunCraft(
            new CraftRequest("process.forge_infusion", "material.iron_ingot", new[] { "material.ember_core" }));
        Assert.True(outcome.Success);
        var original = harness.Registry.All.Single(m => m.Id == outcome.ResultItemId).State!;
        Assert.True(original.Essence.Count > 0);

        var stash = new Inventory();
        var professions = new Dungeons.Professions.ProfessionSystem(
            new DataStore<Dungeons.Professions.ProfessionActionDefinition>(), stash, new SeededRandom(1));
        var save = Dungeons.Persistence.SaveMapper.Capture(
            null, stash, professions, new DiscoverySystem(), new Dictionary<string, int>(),
            savedAtTick: 1, emergentRegistry: harness.Registry);
        var loaded = new Dungeons.Persistence.SaveSerializer()
            .Deserialize(new Dungeons.Persistence.SaveSerializer().Serialize(save));

        var freshMaterials = TestPaths.LoadStore<MaterialDefinition>("materials");
        var freshRegistry = new EmergentRegistry(freshMaterials);
        var freshStash = new Inventory();
        Dungeons.Persistence.SaveMapper.Apply(
            loaded, freshStash,
            new Dungeons.Professions.ProfessionSystem(
                new DataStore<Dungeons.Professions.ProfessionActionDefinition>(), freshStash, new SeededRandom(1)),
            new DiscoverySystem(), new Dictionary<string, int>(), emergentRegistry: freshRegistry);

        var restored = freshRegistry.All.Single(m => m.Id == outcome.ResultItemId).State!;
        Assert.Equal(original.Essence, restored.Essence);
        Assert.Equal(original.Traits, restored.Traits);
    }
}
