using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// C2a — the fabrication boundary (§16): single-slot forms, the 0–100 → combat-unit
/// calibration (pinned by iron-sword parity), trait expression-gated traits with dormancy, terminal
/// consumption, signature dedup, and derived definitions surviving a save round-trip.
/// </summary>
public class EquipmentAssemblyTests
{
    private sealed class Harness
    {
        public Harness()
        {
            Content = new ContentBundle
            {
                Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
                Properties = TestPaths.LoadStore<PropertyDefinition>("properties"),
                Traits = TestPaths.LoadStore<TraitDefinition>("traits"),
                Forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms"),
                Equipment = TestPaths.LoadStore<EquipmentDefinition>("equipment"),
                Moves = TestPaths.LoadStore<Dungeons.Combat.MoveDefinition>("moves"),
            };
            Inventory = new Inventory();
            InstanceIds = new InstanceIdSource();
            Engine = new EquipmentAssemblyEngine(
                Content, () => Inventory, new MaterialStateResolver(Content.Properties), InstanceIds);
        }

        public ContentBundle Content { get; }
        public Inventory Inventory { get; }
        public InstanceIdSource InstanceIds { get; }
        public EquipmentAssemblyEngine Engine { get; }

        public EquipmentAssemblyOutcome Assemble(string form, string material)
        {
            var template = Content.Forms.GetById(form);
            return Engine.Assemble(new EquipmentAssemblyRequest(
                form, template.Slots.Keys.ToDictionary(s => s, _ => material)));
        }

        public EquipmentAssemblyOutcome Assemble(string form, params (string Slot, string Material)[] slots) =>
            Engine.Assemble(new EquipmentAssemblyRequest(form, slots.ToDictionary(s => s.Slot, s => s.Material)));

        public EquipmentAssemblyOutcome Longsword(string edge, string core = "material.iron_ingot", string binding = "material.leather") =>
            Assemble("form.longsword", ("edge", edge), ("core", core), ("binding", binding));
    }

    /// <summary>THE reconciliation pin: a plain iron-ingot longsword must land within a hair
    /// of the authored Iron Sword — same moves, comparable packets through the same resolver
    /// seam. Ordinary materials ≈ starter gear.</summary>
    [Fact]
    public void AnIronIngotLongswordMatchesTheAuthoredIronSword()
    {
        var harness = new Harness();
        harness.Inventory.Add("material.iron_ingot", 4);
        harness.Inventory.Add("material.leather", 2);

        var outcome = harness.Longsword("material.iron_ingot");
        Assert.True(outcome.Success, outcome.Failure.ToString());

        // Instance properties are in combat units (~0–5), beside the authored sword's 3/4.
        var mass = outcome.Item!.Properties.Get(ItemProperties.Mass);
        var hardness = outcome.Item.Properties.Get(ItemProperties.Hardness);
        Assert.InRange(mass, 2.5, 3.7);      // authored: 3
        Assert.InRange(hardness, 2.5, 4.5);  // authored: 4

        // Same seam, comparable output: resolve both weapons' iron slash and compare damage.
        var authored = harness.Content.Equipment.GetById("equip.iron_sword");
        var fabricated = harness.Content.Equipment.GetById(outcome.Item.BaseDefinitionId);
        double Slash(EquipmentDefinition def, ItemInstance? instance) =>
            EquipmentResolver.ResolveWeaponMoves(def, instance, harness.Content.Moves)
                .Single(m => m.Id == "move.iron_slash").Packets.Sum(p => p.Amount);

        var authoredDamage = Slash(authored, null);
        var fabricatedDamage = Slash(fabricated, outcome.Item);
        Assert.True(Math.Abs(authoredDamage - fabricatedDamage) <= 1.5,
            $"authored {authoredDamage} vs fabricated {fabricatedDamage}");
    }

    [Fact]
    public void FabricationIsTerminal_AndIdenticalInputsDeduplicate()
    {
        var harness = new Harness();
        harness.Inventory.Add("material.iron_ingot", 5);
        harness.Inventory.Add("material.leather", 2);

        var first = harness.Longsword("material.iron_ingot");
        var second = harness.Longsword("material.iron_ingot");

        Assert.Equal(1, harness.Inventory.GetQuantity("material.iron_ingot")); // 2 per sword consumed
        Assert.Equal(0, harness.Inventory.GetQuantity("material.leather"));
        Assert.True(first.IsFirstOfItsKind);
        Assert.False(second.IsFirstOfItsKind);
        Assert.Equal(first.Item!.BaseDefinitionId, second.Item!.BaseDefinitionId);
        Assert.NotEqual(first.Item.InstanceId, second.Item.InstanceId); // instances stay unique
    }

    [Fact]
    public void ASlotRejectsAMaterialWithoutTheRequiredTags()
    {
        var harness = new Harness();
        harness.Inventory.Add("material.sageleaf", 1);
        harness.Inventory.Add("material.iron_ingot", 2);
        harness.Inventory.Add("material.leather", 1);
        Assert.Equal(EquipmentAssemblyFailure.SlotRejected,
            harness.Longsword("material.sageleaf").Failure); // herbs make poor edges
    }

    /// <summary>An armour form derives lane resistances from the material's response
    /// properties — the first time authored heat resistance reaches combat through crafting.</summary>
    [Fact]
    public void AnArmourFormDerivesLaneResistances()
    {
        var harness = new Harness();
        harness.Inventory.Add("material.iron_ingot", 1); // heat_resistance 60, cold_resistance 60

        var outcome = harness.Assemble("form.buckler", "material.iron_ingot");
        Assert.True(outcome.Success, outcome.Failure.ToString());

        var definition = harness.Content.Equipment.GetById(outcome.Item!.BaseDefinitionId);
        Assert.NotNull(definition.Armor);
        Assert.Equal(0.18, definition.Armor!.Resistances["heat"], 3); // 60/100 × 0.30
        Assert.Contains("shield", definition.Tags);                   // Shield Bash becomes reachable
    }

    /// <summary>§16.3 steps 3–4: expression = magnitude × trait expression[category]; the cap keeps
    /// the top by expressed value and the rest go dormant on the definition.</summary>
    [Fact]
    public void TraitsExpressThroughTheApertureAndTheRestGoDormant()
    {
        var harness = new Harness();
        var traited = new MaterialDefinition
        {
            Id = "material.test_traited", Name = "Traited Iron",
            Tags = new[] { "form:metal", "state:alloy", "rarity:common", "comp:inorganic", "origin:mineral" },
            Properties = new() { ["hardness"] = 65, ["mass"] = 62 },
            State = new MaterialState(
                new PropertySet(new Dictionary<string, double> { ["hardness"] = 65, ["mass"] = 62 }),
                50, 60, Lineage.ForBase("material.test_traited"), "material.test_traited")
            {
                Traits = new[]
                {
                    new TraitInstance("trait.emberveined", 60),  // thermal ×1.0 → 60
                    new TraitInstance("trait.verdant", 80),      // vital   ×0.2 → 16
                },
            },
        };
        harness.Content.Materials.Add(traited);
        harness.Inventory.Add(traited.Id, 2);
        harness.Inventory.Add("material.iron_ingot", 2);
        harness.Inventory.Add("material.leather", 2);

        var onEdge = harness.Longsword(traited.Id);
        Assert.True(onEdge.Success, onEdge.Failure.ToString());

        // Edge trait expression: emberveined ×1.0 → 60, verdant ×0.2 → 16.
        Assert.Equal("trait.emberveined", onEdge.Expressed[0].Id);
        Assert.Equal(60, onEdge.Expressed[0].Magnitude);
        Assert.Equal(16, onEdge.Expressed.Single(t => t.Id == "trait.verdant").Magnitude);
        Assert.StartsWith("Emberveined Traited Longsword", onEdge.Name); // §16.5 naming

        // Placement matters (§16.2): the same material as the CORE gates emberveined to
        // 60×0.3 → 18 while verdant's 80×0.3 → 24 now DOMINATES — the core's trait expression
        // reorders which trait defines the weapon, computed, never authored. §16.5's
        // exception holds too: the non-primary component names the item.
        var onCore = harness.Assemble("form.longsword",
            ("edge", "material.iron_ingot"), ("core", traited.Id), ("binding", "material.leather"));
        Assert.True(onCore.Success, onCore.Failure.ToString());
        Assert.Equal(18, onCore.Expressed.Single(t => t.Id == "trait.emberveined").Magnitude);
        Assert.Equal(24, onCore.Expressed.Single(t => t.Id == "trait.verdant").Magnitude);
        Assert.StartsWith("Verdant Iron Longsword", onCore.Name);
        Assert.NotEqual(onEdge.Item!.BaseDefinitionId, onCore.Item!.BaseDefinitionId);
    }

    [Fact]
    public void DerivedEquipmentSurvivesTheSaveRoundTrip()
    {
        var harness = new Harness();
        harness.Inventory.Add("material.iron_ingot", 2);
        harness.Inventory.Add("material.leather", 1);
        var outcome = harness.Longsword("material.iron_ingot");
        var derived = harness.Content.Equipment.GetById(outcome.Item!.BaseDefinitionId);

        var stash = new Inventory();
        var professions = new Dungeons.Professions.ProfessionSystem(
            new DataStore<Dungeons.Professions.ProfessionActionDefinition>(), stash,
            new Dungeons.Randomness.SeededRandom(1));
        var save = Dungeons.Persistence.SaveMapper.Capture(
            null, stash, professions, new DiscoverySystem(), new Dictionary<string, int>(),
            savedAtTick: 1,
            emergentEquipment: new[] { derived });
        var loaded = new Dungeons.Persistence.SaveSerializer()
            .Deserialize(new Dungeons.Persistence.SaveSerializer().Serialize(save));

        var freshEquipment = TestPaths.LoadStore<EquipmentDefinition>("equipment");
        var freshStash = new Inventory();
        Dungeons.Persistence.SaveMapper.Apply(
            loaded, freshStash,
            new Dungeons.Professions.ProfessionSystem(
                new DataStore<Dungeons.Professions.ProfessionActionDefinition>(), freshStash,
                new Dungeons.Randomness.SeededRandom(1)),
            new DiscoverySystem(), new Dictionary<string, int>(), equipmentStore: freshEquipment);

        var restored = freshEquipment.GetById(derived.Id);
        Assert.Equal(derived.Name, restored.Name);
        Assert.Equal(derived.Properties, restored.Properties);
        Assert.Equal(derived.Moves.Select(m => m.Id), restored.Moves.Select(m => m.Id));
    }

    // ---- R3: the pre-commit fabrication projection --------------------------------------------

    /// <summary>One computation, two callers: the projection must match what Fabricate then
    /// mints, and must touch nothing — components are consumed forever, so the preview being
    /// wrong or the preview consuming anything would each break the §6.2c fairness extension.</summary>
    [Fact]
    public void ProjectMatchesFabricateAndHasNoSideEffects()
    {
        var harness = new Harness();
        harness.Inventory.Add("material.iron_ingot", 4);
        harness.Inventory.Add("material.leather", 2);

        var request = new EquipmentAssemblyRequest("form.longsword", new Dictionary<string, string>
        {
            ["edge"] = "material.iron_ingot",
            ["core"] = "material.iron_ingot",
            ["binding"] = "material.leather",
        });

        var equipmentBefore = harness.Content.Equipment.Count;
        var projection = harness.Engine.Preview(request);

        Assert.True(projection.CanFabricate, projection.Failure.ToString());
        Assert.True(projection.WouldBeFirstOfItsKind);
        Assert.Equal(4, harness.Inventory.GetQuantity("material.iron_ingot"));   // nothing consumed
        Assert.Equal(equipmentBefore, harness.Content.Equipment.Count);          // nothing registered
        Assert.Equal(("edge", "Iron Ingot"), projection.ComponentNames[0]);      // identity slot leads

        var outcome = harness.Engine.Assemble(request);
        Assert.True(outcome.Success);
        Assert.Equal(projection.Name, outcome.Name);
        Assert.Equal(
            projection.Stats.OrderBy(s => s.Key).Select(s => (s.Key, s.Value)),
            outcome.Item!.Properties.AsDictionary().OrderBy(s => s.Key).Select(s => (s.Key, s.Value)));
        Assert.Equal(projection.Expressed.Select(t => t.Id), outcome.Expressed.Select(t => t.Id));
    }

    [Fact]
    public void ProjectionReportsGateFailuresWithoutThrowing()
    {
        var harness = new Harness(); // empty inventory

        var missing = harness.Engine.Preview(new EquipmentAssemblyRequest("form.longsword", new Dictionary<string, string>
        {
            ["edge"] = "material.iron_ingot",
            ["core"] = "material.iron_ingot",
            ["binding"] = "material.leather",
        }));
        Assert.Equal(EquipmentAssemblyFailure.MissingInputs, missing.Failure);

        var rejected = harness.Engine.Preview(new EquipmentAssemblyRequest("form.longsword", new Dictionary<string, string>
        {
            ["edge"] = "material.leather",
            ["core"] = "material.iron_ingot",
            ["binding"] = "material.leather",
        }));
        Assert.Equal(EquipmentAssemblyFailure.SlotRejected, rejected.Failure);
    }
}
