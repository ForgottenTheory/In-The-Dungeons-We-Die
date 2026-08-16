using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// C1a — the §10 trait pass: birth from state, property consumption, the cap of 3 with
/// weakest-displaced, supersession merges, and traits joining the identity signature without
/// disturbing trait-less P1 archetypes.
/// </summary>
public class TraitTests
{
    private static DataStore<TraitDefinition> Library(params TraitDefinition[] traits)
    {
        var store = new DataStore<TraitDefinition>();
        foreach (var trait in traits)
            store.Add(trait);
        return store;
    }

    private static TraitDefinition Resilient => new()
    {
        Id = "trait.resilient", Name = "Resilient",
        Condition = new() { ["hardness"] = new PropertyRange { Min = 70 }, ["flexibility"] = new PropertyRange { Min = 60 } },
        MagnitudeOf = new[] { "hardness", "flexibility" },
        Consumes = new() { ["hardness"] = 5, ["flexibility"] = 5 },
    };

    private static PropertySet State(params (string Key, double Value)[] values) =>
        new(values.ToDictionary(v => v.Key, v => v.Value));

    // ---- Birth ---------------------------------------------------------------------------

    [Fact]
    public void ATraitIsBornWhenItsConditionHolds_AndEatsItsProperties()
    {
        var resolver = new TraitResolver(Library(Resilient));

        var result = resolver.Apply(State(("hardness", 72), ("flexibility", 64)), Array.Empty<TraitInstance>());

        var born = Assert.Single(result.Born);
        Assert.Equal("trait.resilient", born.Id);
        Assert.Equal(64, born.Magnitude); // min(hardness, flexibility), pre-consumption
        Assert.Equal(67, result.Properties.Get("hardness"));   // 72 − 5
        Assert.Equal(59, result.Properties.Get("flexibility")); // 64 − 5
        Assert.Equal(1, result.TraitsCreated);
    }

    [Fact]
    public void AHeldTraitIsNotBornTwice_AndConditionFailureBirthsNothing()
    {
        var resolver = new TraitResolver(Library(Resilient));
        var held = new[] { new TraitInstance("trait.resilient", 60) };

        var again = resolver.Apply(State(("hardness", 90), ("flexibility", 90)), held);
        Assert.Empty(again.Born);
        Assert.Equal(90, again.Properties.Get("hardness")); // nothing re-eaten

        var never = resolver.Apply(State(("hardness", 69), ("flexibility", 90)), Array.Empty<TraitInstance>());
        Assert.Empty(never.Born);
    }

    /// <summary>Two traits contending for one property resolve by id order — the earlier birth
    /// eats first and can starve the later condition. Deterministic, and pinned as such.</summary>
    [Fact]
    public void BirthsAreSequentialInIdOrder_SoConsumptionCanStarveALaterCondition()
    {
        var eater = new TraitDefinition
        {
            Id = "trait.a_eater", Name = "Eater",
            Condition = new() { ["hardness"] = new PropertyRange { Min = 60 } },
            MagnitudeOf = new[] { "hardness" },
            Consumes = new() { ["hardness"] = 15 },
        };
        var needy = new TraitDefinition
        {
            Id = "trait.b_needy", Name = "Needy",
            Condition = new() { ["hardness"] = new PropertyRange { Min = 60 } },
            MagnitudeOf = new[] { "hardness" },
        };

        var result = new TraitResolver(Library(eater, needy))
            .Apply(State(("hardness", 65)), Array.Empty<TraitInstance>());

        // a_eater births at 65 and eats to 50; b_needy's min-60 condition now fails.
        var born = Assert.Single(result.Born);
        Assert.Equal("trait.a_eater", born.Id);
    }

    // ---- The cap -------------------------------------------------------------------------

    [Fact]
    public void AFourthTraitDisplacesTheWeakest_AndConsumptionIsNotRefunded()
    {
        TraitDefinition Simple(string id, string property, double min) => new()
        {
            Id = id, Name = id,
            Condition = new() { [property] = new PropertyRange { Min = min } },
            MagnitudeOf = new[] { property },
            Consumes = new() { [property] = 2 },
        };

        var resolver = new TraitResolver(Library(
            Simple("trait.a", "heat", 10),
            Simple("trait.b", "cold", 10),
            Simple("trait.c", "charge", 10),
            Simple("trait.d", "growth", 10)));

        var result = resolver.Apply(
            State(("heat", 80), ("cold", 70), ("charge", 60), ("growth", 12)),
            Array.Empty<TraitInstance>());

        Assert.Equal(3, result.Traits.Count);
        var displaced = Assert.Single(result.Displaced);
        Assert.Equal("trait.d", displaced.Id); // weakest by magnitude (12)
        Assert.Equal(10, result.Properties.Get("growth")); // 12 − 2: still eaten, not refunded
    }

    // ---- Supersession --------------------------------------------------------------------

    [Fact]
    public void SupersessionMergesAPair_FreesTheSlot_AndKeepsTheStrongerMagnitude()
    {
        var ember = new TraitDefinition
        {
            Id = "trait.ember", Name = "Ember",
            Condition = new() { ["heat"] = new PropertyRange { Min = 50 } },
            MagnitudeOf = new[] { "heat" },
            Merges = new[] { new TraitMerge { With = "trait.storm", Into = "trait.tempest" } },
        };
        var storm = new TraitDefinition
        {
            Id = "trait.storm", Name = "Storm",
            Condition = new() { ["charge"] = new PropertyRange { Min = 50 } },
            MagnitudeOf = new[] { "charge" },
        };
        var tempest = new TraitDefinition { Id = "trait.tempest", Name = "Tempest" };

        var result = new TraitResolver(Library(ember, storm, tempest))
            .Apply(State(("heat", 55), ("charge", 75)), Array.Empty<TraitInstance>());

        var merged = Assert.Single(result.Traits);
        Assert.Equal("trait.tempest", merged.Id);
        Assert.Equal(75, merged.Magnitude); // the stronger parent
        var supersession = Assert.Single(result.Superseded);
        Assert.Equal("trait.tempest", supersession.Into.Id);
        Assert.Equal(2, result.TraitsCreated); // both parents were born; the merge itself is free
    }

    /// <summary>A merge-only trait (no condition) can never be born from state, however
    /// extreme the state is.</summary>
    [Fact]
    public void AMergeOnlyTraitIsNeverStateBorn()
    {
        var tempest = new TraitDefinition { Id = "trait.tempest", Name = "Tempest" };

        var result = new TraitResolver(Library(tempest))
            .Apply(State(("heat", 100), ("charge", 100)), Array.Empty<TraitInstance>());

        Assert.Empty(result.Traits);
    }

    // ---- Identity ------------------------------------------------------------------------

    [Fact]
    public void TraitsJoinTheSignature_ByTier_AndAnEmptySetChangesNothing()
    {
        var lineage = new Lineage(
            new[] { new RootShare("material.iron_ore", 1.0) }, 2, "process.forge_infusion",
            new[] { "material.iron_ingot" });
        var bare = new MaterialProfile(State(("hardness", 60)), 40, 70, lineage, string.Empty);
        var tags = new[] { "form:metal", "state:alloy" };

        // Trait-less: the canonical string still carries an empty traits section — P1 ids hold.
        Assert.Contains("|traits=|", MaterialSignature.Canonical(bare, tags));

        var with62 = bare with { Traits = new[] { new TraitInstance("trait.resilient", 62) } };
        var with64 = bare with { Traits = new[] { new TraitInstance("trait.resilient", 64) } };
        var with85 = bare with { Traits = new[] { new TraitInstance("trait.resilient", 85) } };

        // Same tier (62, 64 → tier 4) is the same material; a different tier is not.
        Assert.Equal(MaterialSignature.Compute(with62, tags), MaterialSignature.Compute(with64, tags));
        Assert.NotEqual(MaterialSignature.Compute(with62, tags), MaterialSignature.Compute(with85, tags));
        Assert.NotEqual(MaterialSignature.Compute(bare, tags), MaterialSignature.Compute(with62, tags));
    }

    /// <summary>The §10.4 report lines render as specified — births, mergers, displacements,
    /// each explicit, because "which three?" is only a decision the player can make if the
    /// log says what each craft did to the set.</summary>
    [Fact]
    public void TheTraitReportRendersBirthsMergersAndDisplacements()
    {
        var resolution = new TraitResolution(
            Traits: new[] { new TraitInstance("trait.tempest", 75) },
            Properties: PropertySet.Empty,
            Born: new[] { new TraitInstance("trait.storm", 75) },
            Superseded: new[] { (new TraitInstance("trait.ember", 55), new TraitInstance("trait.storm", 75), new TraitInstance("trait.tempest", 75)) },
            Displaced: new[] { new TraitInstance("trait.warmed", 22) });

        var text = new ReactionLogBuilder(new DataStore<PropertyDefinition>())
            .Traits(resolution, id => id)
            .Build()
            .ToString();

        Assert.Contains("✦ Trait gained: trait.storm (75)", text);
        Assert.Contains("⚡ Traits superseded: trait.ember + trait.storm → trait.tempest (75)", text);
        Assert.Contains("⚠ Trait lost: trait.warmed (22) — displaced, trait cap 3/3", text);
    }

    // ---- End to end through the engine ----------------------------------------------------

    /// <summary>Forge Infusion of Ember Core into Iron Ingot pushes heat with conductivity
    /// already present — the Emberveined region. The trait must be born, logged, charged
    /// (traits_created × 4), and carried on the resulting archetype.</summary>
    [Fact]
    public void ACraftThatEntersATraitRegionBirthsItEndToEnd()
    {
        var content = new ContentBundle
        {
            Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
            Properties = TestPaths.LoadStore<PropertyDefinition>("properties"),
            Processes = TestPaths.LoadStore<ProcessDefinition>("processes"),
            Byproducts = TestPaths.LoadStore<ByproductDefinition>("byproducts"),
            NameGrammar = TestPaths.LoadStore<NameWordDefinition>("name_grammar"),
            Traits = TestPaths.LoadStore<TraitDefinition>("traits"),
        };
        var inventory = new Inventory();
        inventory.Add("material.iron_ingot", 10);
        inventory.Add("material.ember_core", 10);

        var registry = new EmergentRegistry(content.Materials);
        var engine = new ReactionEngine(
            content, () => inventory,
            new MaterialProfileResolver(content.Properties),
            registry,
            new NameGenerator(content.Materials, content.Properties, content.NameGrammar),
            new TagDeriver(content.Properties),
            new ByproductResolver(content.Byproducts),
            new TraitResolver(content.Traits),
            _ => 99,
            new Dungeons.Randomness.SeededRandom(12345));

        // Convergence pulls heat toward the reagent's a step at a time (34 → 56 → ~70), so
        // three infusions cross Emberveined's heat-60 threshold. Read the trace, not just
        // the asserts — the failure message prints it.
        var result = engine.Resolve(
            new CraftRequest("process.forge_infusion", "material.iron_ingot", new[] { "material.ember_core" }));
        Assert.True(result.Success, result.Failure.ToString());
        for (var i = 0; i < 2; i++)
        {
            result = engine.Resolve(
                new CraftRequest("process.forge_infusion", result.ResultItemId!, new[] { "material.ember_core" }));
            Assert.True(result.Success, result.Failure.ToString());
        }

        var archetypes = registry.All.ToList();
        var traited = archetypes.FirstOrDefault(m => m.Profile is { Traits.Count: > 0 });

        Assert.True(traited is not null,
            "expected at least one archetype to carry a trait after stacking heat into iron; " +
            "trace of the last craft:\n" + result.Log);
        Assert.Contains(traited!.Profile!.Traits, t => t.Id == "trait.emberveined");
    }
}
