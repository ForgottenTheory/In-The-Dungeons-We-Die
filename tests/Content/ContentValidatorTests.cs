using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Realms;
using Dungeons.Tests;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// Exercises <see cref="ContentValidator"/> two ways: the real shipped content must
/// pass with zero problems, and each cross-reference rule must catch a deliberately
/// broken in-memory store. This is the load-time counterpart to the per-system
/// *ContentValidationTests (ROADMAP Phase 4 — validate at load, not only in tests).
/// </summary>
public class ContentValidatorTests
{
    // --- Shipped content -----------------------------------------------------

    [Fact]
    public void ShippedContentHasNoProblems()
    {
        var problems = ContentValidator.Validate(
            Load<MaterialDefinition>("materials"),
            Load<ProfessionDefinition>("professions"),
            Load<ProfessionActionDefinition>("profession_actions"),
            Load<CraftingInteractionDefinition>("crafting_interactions"),
            Load<AbilityDefinition>("abilities"),
            Load<ActorDefinition>("actors"),
            Load<RealmDefinition>("realms"),
            Load<ConsumableDefinition>("consumables"),
            Load<EquipmentDefinition>("equipment"));

        Assert.True(problems.Count == 0,
            "Shipped content should validate cleanly, but found:" + Environment.NewLine +
            string.Join(Environment.NewLine, problems));
    }

    private static readonly string[] ValidTags = { "origin:flora", "comp:organic", "form:wood", "state:raw", "rarity:common" };

    [Fact]
    public void ShippedMaterialLibrary_IsSubstantial_LegacyIdsSurvive_AndProfilesAreCoherent()
    {
        var materials = Load<MaterialDefinition>("materials");

        Assert.True(materials.Count >= 400, $"Expected a large material library, found {materials.Count}.");

        // Ids referenced by professions/crafting/realm/actor content must survive.
        foreach (var id in new[]
        {
            "material.oak_log", "material.oak_bark", "material.sageleaf", "material.marsh_root",
            "material.iron_ore", "material.iron_ingot", "material.barkbound_iron", "material.goblin_scrap",
        })
            Assert.True(materials.Contains(id), $"legacy material {id} went missing.");

        // Every material carries exactly one rarity: tag (availability, not power).
        foreach (var material in materials.GetAll())
            Assert.True(material.Tags.Count(t => t.StartsWith("rarity:", StringComparison.Ordinal)) == 1,
                $"{material.Id} must have exactly one rarity: tag.");

        // Spot-check that profiles express their intended identity.
        var copper = materials.GetById("material.copper_ore");
        Assert.True(copper.GetProperty("conductivity") > copper.GetProperty("insulation")); // copper conducts

        var stormCore = materials.GetById("material.storm_core");
        Assert.Equal(100, stormCore.GetProperty("charge"));
        Assert.True(stormCore.GetProperty("instability") >= 80); // deliberately volatile

        Assert.True(materials.GetById("material.frost_core").GetProperty("cold") >= 80);          // frost = cold
        Assert.True(materials.GetById("material.scorpion_venom").GetProperty("toxicity") >= 50);  // venom = toxic
        Assert.True(materials.GetById("material.wolf_fur").GetProperty("insulation") >= 60);      // fur insulates

        var ingot = materials.GetById("material.iron_ingot");
        var ore = materials.GetById("material.iron_ore");
        Assert.True(ingot.GetProperty("hardness") > ore.GetProperty("hardness")); // processing hardens
    }

    // --- Each rule catches a broken reference --------------------------------

    [Fact]
    public void ValidBaseline_ProducesNoProblems()
    {
        var content = ValidBaseline();
        Assert.Empty(content.Validate());
    }

    [Fact]
    public void Material_WithCoherentProperties_IsAccepted()
    {
        var content = ValidBaseline();
        content.Materials.Add(new MaterialDefinition
        {
            Id = "material.copper_ore",
            Name = "Copper Ore",
            Tags = new[] { "origin:mineral", "comp:inorganic", "form:ore", "state:raw", "rarity:common" },
            Properties = new Dictionary<string, double>
            {
                ["hardness"] = 40, ["mass"] = 50, ["conductivity"] = 85, ["heat_resistance"] = 55,
            },
        });
        Assert.Empty(content.Validate());
    }

    [Fact]
    public void Material_WithUnknownProperty_IsFlagged()
    {
        var content = ValidBaseline();
        content.Materials.Add(new MaterialDefinition
        {
            Id = "material.bad",
            Name = "Bad",
            Properties = new Dictionary<string, double> { ["sparkliness"] = 10 },
        });
        AssertHasProblem(content, "materials", "sparkliness");
    }

    [Fact]
    public void Material_WithOutOfRangeValue_IsFlagged()
    {
        var content = ValidBaseline();
        content.Materials.Add(new MaterialDefinition
        {
            Id = "material.bad",
            Name = "Bad",
            Properties = new Dictionary<string, double> { ["hardness"] = 250 },
        });
        AssertHasProblem(content, "materials", "range");
    }

    [Fact]
    public void Material_WithNegativeValue_IsFlagged()
    {
        var content = ValidBaseline();
        content.Materials.Add(new MaterialDefinition
        {
            Id = "material.bad",
            Name = "Bad",
            Properties = new Dictionary<string, double> { ["mass"] = -5 },
        });
        AssertHasProblem(content, "materials", "range");
    }

    // --- Tag family namespacing + cardinality --------------------------------

    private static MaterialDefinition Tagged(params string[] tags) =>
        new() { Id = "material.bad", Name = "Bad", Tags = tags };

    [Fact]
    public void Tags_MustBeNamespaced()
    {
        var content = ValidBaseline();
        content.Materials.Add(Tagged("origin:flora", "comp:organic", "form:wood", "rarity:common", "legacy"));
        AssertHasProblem(content, "tags", "un-namespaced");
    }

    [Fact]
    public void Tags_UnknownFamily_IsFlagged()
    {
        var content = ValidBaseline();
        content.Materials.Add(Tagged("origin:flora", "comp:organic", "form:wood", "rarity:common", "badfam:x"));
        AssertHasProblem(content, "tags", "unknown family");
    }

    [Fact]
    public void Tags_InvalidClosedValue_IsFlagged()
    {
        var content = ValidBaseline();
        content.Materials.Add(Tagged("origin:flora", "comp:organic", "form:wood", "rarity:legendary"));
        AssertHasProblem(content, "tags", "not a valid");
    }

    [Fact]
    public void Tags_MissingForm_IsFlagged()
    {
        var content = ValidBaseline();
        content.Materials.Add(Tagged("origin:flora", "comp:organic", "rarity:common")); // no form:
        AssertHasProblem(content, "tags", "'form:' tag");
    }

    [Fact]
    public void Tags_TwoRarity_IsFlagged()
    {
        var content = ValidBaseline();
        content.Materials.Add(Tagged("origin:flora", "comp:organic", "form:wood", "rarity:common", "rarity:rare"));
        AssertHasProblem(content, "tags", "'rarity:' tags");
    }

    [Fact]
    public void Tags_ThreeOrigins_IsFlagged()
    {
        var content = ValidBaseline();
        content.Materials.Add(Tagged("origin:flora", "origin:fauna", "origin:fungal", "comp:organic", "form:wood", "rarity:common"));
        AssertHasProblem(content, "tags", "'origin:' tags");
    }

    [Fact]
    public void Tags_TwoOriginsAndTwoForms_AreAllowed()
    {
        var content = ValidBaseline();
        content.Materials.Add(Tagged("origin:mineral", "origin:arcane", "comp:inorganic", "form:metal", "form:ore", "state:raw", "rarity:rare"));
        Assert.Empty(content.Validate());
    }

    [Fact]
    public void Actor_WithUnknownAbility_IsFlagged()
    {
        var content = ValidBaseline();
        content.Actors.Add(new ActorDefinition { Id = "actor.bad", AbilityIds = new[] { "ability.ghost" } });
        AssertHasProblem(content, "actors", "ability.ghost");
    }

    [Fact]
    public void Actor_WithUnknownLoot_IsFlagged()
    {
        var content = ValidBaseline();
        content.Actors.Add(new ActorDefinition
        {
            Id = "actor.bad",
            AbilityIds = new[] { "ability.strike" },
            LootItemId = "material.ghost",
        });
        AssertHasProblem(content, "actors", "material.ghost");
    }

    [Fact]
    public void ProfessionAction_WithUnknownProfession_IsFlagged()
    {
        var content = ValidBaseline();
        content.Actions.Add(new ProfessionActionDefinition { Id = "action.bad", ProfessionId = "prof.ghost" });
        AssertHasProblem(content, "profession_actions", "prof.ghost");
    }

    [Fact]
    public void ProfessionAction_WithUnknownMaterial_IsFlagged()
    {
        var content = ValidBaseline();
        content.Actions.Add(new ProfessionActionDefinition
        {
            Id = "action.bad",
            ProfessionId = "prof.forestry",
            Outputs = new[] { new ItemStack("material.ghost", 1) },
        });
        AssertHasProblem(content, "profession_actions", "material.ghost");
    }

    [Fact]
    public void Interaction_WithUnknownInput_IsFlagged()
    {
        var content = ValidBaseline();
        content.Interactions.Add(new CraftingInteractionDefinition
        {
            Id = "interaction.bad",
            Inputs = new[] { new ItemStack("material.ghost", 1) },
            ResultItemId = "material.oak",
        });
        AssertHasProblem(content, "crafting", "material.ghost");
    }

    [Fact]
    public void Interaction_WithUnknownResult_IsFlagged()
    {
        var content = ValidBaseline();
        content.Interactions.Add(new CraftingInteractionDefinition
        {
            Id = "interaction.bad",
            Inputs = new[] { new ItemStack("material.oak", 1) },
            ResultItemId = "material.ghost",
        });
        AssertHasProblem(content, "crafting", "material.ghost");
    }

    [Fact]
    public void Interaction_ResultMayBeAConsumable()
    {
        var content = ValidBaseline();
        content.Consumables.Add(new ConsumableDefinition { Id = "consumable.salve" });
        content.Interactions.Add(new CraftingInteractionDefinition
        {
            Id = "interaction.ok",
            Inputs = new[] { new ItemStack("material.oak", 1) },
            ResultItemId = "consumable.salve",
        });
        Assert.Empty(content.Validate());
    }

    [Fact]
    public void Interaction_WithUnknownProfessionRequirement_IsFlagged()
    {
        var content = ValidBaseline();
        content.Interactions.Add(new CraftingInteractionDefinition
        {
            Id = "interaction.bad",
            Inputs = new[] { new ItemStack("material.oak", 1) },
            ResultItemId = "material.oak",
            ProfessionRequirements = new[] { new ProfessionRequirement { ProfessionId = "prof.ghost", Level = 1 } },
        });
        AssertHasProblem(content, "crafting", "prof.ghost");
    }

    [Fact]
    public void Realm_WithDanglingConnection_IsFlagged()
    {
        var content = ValidBaseline();
        content.Realms.Add(new RealmDefinition
        {
            Id = "realm.bad",
            Locations = new[]
            {
                new RealmLocationDefinition { Id = "loc.a", Type = RealmLocationType.Entrance, Connections = new[] { "loc.ghost" } },
            },
        });
        AssertHasProblem(content, "realms", "loc.ghost");
    }

    [Fact]
    public void Realm_WithAsymmetricEdge_IsFlagged()
    {
        var content = ValidBaseline();
        content.Realms.Add(new RealmDefinition
        {
            Id = "realm.bad",
            Locations = new[]
            {
                new RealmLocationDefinition { Id = "loc.a", Type = RealmLocationType.Entrance, Connections = new[] { "loc.b" } },
                new RealmLocationDefinition { Id = "loc.b", Type = RealmLocationType.Travel }, // no edge back to loc.a
            },
        });
        AssertHasProblem(content, "realms", "not symmetric");
    }

    [Fact]
    public void Realm_CombatNodeWithUnknownActor_IsFlagged()
    {
        var content = ValidBaseline();
        content.Realms.Add(new RealmDefinition
        {
            Id = "realm.bad",
            Locations = new[]
            {
                new RealmLocationDefinition { Id = "loc.a", Type = RealmLocationType.Combat, ActorId = "actor.ghost" },
            },
        });
        AssertHasProblem(content, "realms", "actor.ghost");
    }

    [Fact]
    public void Realm_GatherNodeWithUnknownAction_IsFlagged()
    {
        var content = ValidBaseline();
        content.Realms.Add(new RealmDefinition
        {
            Id = "realm.bad",
            Locations = new[]
            {
                new RealmLocationDefinition { Id = "loc.a", Type = RealmLocationType.Gather, ProfessionActionId = "action.ghost" },
            },
        });
        AssertHasProblem(content, "realms", "action.ghost");
    }

    [Fact]
    public void Realm_EventNodeWithUnknownReward_IsFlagged()
    {
        var content = ValidBaseline();
        content.Realms.Add(new RealmDefinition
        {
            Id = "realm.bad",
            Locations = new[]
            {
                new RealmLocationDefinition { Id = "loc.a", Type = RealmLocationType.Event, RewardItemId = "material.ghost" },
            },
        });
        AssertHasProblem(content, "realms", "material.ghost");
    }

    [Fact]
    public void Equipment_WeaponWithoutWeaponBlock_IsFlagged()
    {
        var content = ValidBaseline();
        content.Equipment.Add(new EquipmentDefinition { Id = "equip.bad", Slot = EquipmentSlot.Weapon, Weapon = null });
        AssertHasProblem(content, "equipment", "equip.bad");
    }

    [Fact]
    public void Equipment_ArmorWithoutArmorBlock_IsFlagged()
    {
        var content = ValidBaseline();
        content.Equipment.Add(new EquipmentDefinition { Id = "equip.bad", Slot = EquipmentSlot.Armor, Armor = null });
        AssertHasProblem(content, "equipment", "equip.bad");
    }

    // --- Helpers -------------------------------------------------------------

    private static DataStore<T> Load<T>(string subfolder) where T : IDefinition
    {
        var store = new DataStore<T>();
        store.LoadDocuments(Directory.GetFiles(Path.Combine(TestPaths.DataDir, subfolder), "*.json").Select(File.ReadAllText));
        return store;
    }

    private static void AssertHasProblem(TestContent content, string category, string messageFragment)
    {
        var problems = content.Validate();
        Assert.Contains(problems, p => p.Category == category && p.Message.Contains(messageFragment));
    }

    /// <summary>A minimal, internally-consistent content set that validates cleanly.</summary>
    private static TestContent ValidBaseline()
    {
        var content = new TestContent();
        content.Materials.Add(new MaterialDefinition { Id = "material.oak", Tags = ValidTags });
        content.Professions.Add(new ProfessionDefinition { Id = "prof.forestry" });
        content.Abilities.Add(new AbilityDefinition { Id = "ability.strike" });
        return content;
    }

    /// <summary>Mutable bag of stores mirroring <see cref="ContentValidator.Validate"/>'s arguments.</summary>
    private sealed class TestContent
    {
        public DataStore<MaterialDefinition> Materials { get; } = new();
        public DataStore<ProfessionDefinition> Professions { get; } = new();
        public DataStore<ProfessionActionDefinition> Actions { get; } = new();
        public DataStore<CraftingInteractionDefinition> Interactions { get; } = new();
        public DataStore<AbilityDefinition> Abilities { get; } = new();
        public DataStore<ActorDefinition> Actors { get; } = new();
        public DataStore<RealmDefinition> Realms { get; } = new();
        public DataStore<ConsumableDefinition> Consumables { get; } = new();
        public DataStore<EquipmentDefinition> Equipment { get; } = new();

        public IReadOnlyList<ContentProblem> Validate() => ContentValidator.Validate(
            Materials, Professions, Actions, Interactions,
            Abilities, Actors, Realms, Consumables, Equipment);
    }
}
