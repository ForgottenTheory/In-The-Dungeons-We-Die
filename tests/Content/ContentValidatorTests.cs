using Dungeons.Characters.Composition;
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
        var problems = ContentValidator.Validate(LoadShippedBundle());

        Assert.True(problems.Count == 0,
            "Shipped content should validate cleanly, but found:" + Environment.NewLine +
            string.Join(Environment.NewLine, problems));
    }

    private static ContentBundle LoadShippedBundle() => new()
    {
        Materials = Load<MaterialDefinition>("materials"),
        Identities = Load<IdentityDefinition>("identities"),
        SignatureTriggers = Load<SignatureTriggerDefinition>("signature_triggers"),
        SignatureBehaviors = Load<SignatureBehaviorDefinition>("signature_behaviors"),
        SignatureThemes = Load<SignatureThemeDefinition>("signature_themes"),
        SignaturePayloads = Load<SignaturePayloadDefinition>("signature_payloads"),
        VerbActions = Load<VerbActionDefinition>("verb_actions"),
        Byproducts = Load<ByproductDefinition>("byproducts"),
        ModifierKeys = Load<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
        Statuses = Load<StatusDefinition>("statuses"),
        NameFormats = Load<NameFormatDefinition>("name_formats"),
        Professions = Load<ProfessionDefinition>("professions"),
        Actions = Load<ProfessionActionDefinition>("profession_actions"),
        TrainingObstacles = Load<TrainingObstacleDefinition>("training_obstacles"),
        MasteryBenefits = Load<MasteryBenefitDefinition>("mastery"),
        Synergies = Load<ProfessionSynergyDefinition>("synergies"),
        Stations = Load<Dungeons.Hideout.StationDefinition>("stations"),
        Forms = Load<EquipmentBlueprintDefinition>("forms"),
        Interactions = Load<CraftingInteractionDefinition>("crafting_interactions"),
        Moves = Load<MoveDefinition>("moves"),
        MoveModifiers = Load<MoveModifierDefinition>("move_modifiers"),
        Actors = Load<ActorDefinition>("actors"),
        EnemyFamilies = Load<EnemyFamilyDefinition>("enemy_families"),
        EnemyRoles = Load<CombatRoleDefinition>("enemy_roles"),
        AiProfiles = Load<AiProfileDefinition>("ai_profiles"),
        AutoCombatProfiles = Load<AutoCombatProfileDefinition>("auto_combat"),
        Realms = Load<RealmDefinition>("realms"),
        LootTables = Load<Dungeons.Loot.LootTableDefinition>("loot_tables"),
        Consumables = Load<ConsumableDefinition>("consumables"),
        Techniques = Load<TechniqueDefinition>("techniques"),
        Equipment = Load<EquipmentDefinition>("equipment"),
        Species = Load<SpeciesDefinition>("species"),
        Classes = Load<BaseClassDefinition>("classes"),
        Prefixes = Load<PrefixDefinition>("prefixes"),
        Suffixes = Load<SuffixDefinition>("suffixes"),
    };

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

        // Spot-check that the identity model expresses intent where properties used to.
        Assert.Contains(materials.GetById("material.storm_core").Identities, grant => grant.Id == "identity.storm");
        Assert.Contains(materials.GetById("material.frost_core").Identities, grant => grant.Id == "identity.frost");
        Assert.Contains("identity.venomous", materials.GetById("material.scorpion_venom").Latent);
    }

    // --- Each rule catches a broken reference --------------------------------

    [Fact]
    public void ValidBaseline_ProducesNoProblems()
    {
        var content = ValidBaseline();
        Assert.Empty(content.Validate());
    }





    [Fact]
    public void Equipment_WithUnknownProperty_IsFlagged()
    {
        var content = ValidBaseline();
        content.Equipment.Add(new EquipmentDefinition
        {
            Id = "equip.bad",
            Slot = EquipmentSlot.Weapon,
            Moves = new[] { new MoveGrantSpec { Id = "move.strike" } },
            Properties = new Dictionary<string, double> { ["sparkliness"] = 3 },
        });
        AssertHasProblem(content, "equipment", "sparkliness");
    }

    [Fact]
    public void CharacterComponent_WithUnknownMove_IsFlagged()
    {
        var content = ValidBaseline();
        content.Species.Add(new SpeciesDefinition { Id = "species.bad", Moves = new[] { new MoveGrantSpec { Id = "move.ghost" } } });
        AssertHasProblem(content, "species", "move.ghost");
    }

    /// <summary>The E4 rule: there is no allowlist any more — a granted move either exists or fails.</summary>
    [Fact]
    public void CharacterComponent_WithKnownMove_IsAccepted()
    {
        var content = ValidBaseline();
        content.Species.Add(new SpeciesDefinition { Id = "species.ok", Moves = new[] { new MoveGrantSpec { Id = "move.strike" } } });
        Assert.Empty(content.Validate());
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
    public void Actor_WithUnknownMove_IsFlagged()
    {
        var content = ValidBaseline();
        content.Actors.Add(new ActorDefinition { Id = "actor.bad", Moves = new[] { new MoveGrantSpec { Id = "move.ghost" } } });
        AssertHasProblem(content, "actors", "move.ghost");
    }

    [Fact]
    public void Actor_WithUnknownLootTable_IsFlagged()
    {
        var content = ValidBaseline();
        content.Actors.Add(new ActorDefinition
        {
            Id = "actor.bad",
            Moves = new[] { new MoveGrantSpec { Id = "move.strike" } },
            LootTableId = "loot.ghost",
        });
        AssertHasProblem(content, "actors", "loot.ghost");
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
    public void Realm_NodeWithUnknownLootTable_IsFlagged()
    {
        var content = ValidBaseline();
        content.Realms.Add(new RealmDefinition
        {
            Id = "realm.bad",
            Locations = new[]
            {
                new RealmLocationDefinition
                {
                    Id = "loc.a", Type = RealmLocationType.Event,
                    EventText = "A chest.", LootTableId = "loot.ghost",
                },
            },
        });
        AssertHasProblem(content, "realms", "loot.ghost");
    }

    [Fact]
    public void Realm_EventNodeThatDoesNothing_IsFlagged()
    {
        var content = ValidBaseline();
        content.Realms.Add(new RealmDefinition
        {
            Id = "realm.bad",
            Locations = new[] { new RealmLocationDefinition { Id = "loc.a", Type = RealmLocationType.Event } },
        });
        AssertHasProblem(content, "realms", "no text and no loot");
    }

    [Fact]
    public void Equipment_WeaponGrantingNoMoves_IsFlagged()
    {
        var content = ValidBaseline();
        content.Equipment.Add(new EquipmentDefinition { Id = "equip.bad", Slot = EquipmentSlot.Weapon });
        AssertHasProblem(content, "equipment", "equip.bad");
    }

    [Fact]
    public void Equipment_ArmorWithoutArmorBlock_IsFlagged()
    {
        var content = ValidBaseline();
        content.Equipment.Add(new EquipmentDefinition { Id = "equip.bad", Slot = EquipmentSlot.Body, Armor = null });
        AssertHasProblem(content, "equipment", "equip.bad");
    }

    // --- CraftingActions (docs/emergent-item-system.md §7) --------------------------















    // --- Byproducts (docs/emergent-item-system.md §6.2c) ----------------------

    [Fact]
    public void Byproduct_ProducingAnUnknownMaterial_IsReported()
    {
        var content = ValidBaseline();
        content.Byproducts.Add(new ByproductDefinition
        {
            Id = "byproduct.bad", Material = "material.nope", Forms = new[] { "wood" }, Fallback = true,
        });
        AssertHasProblem(content, "byproducts", "unknown material");
    }

    /// <summary>A form covered twice would make the outcome depend on load order.</summary>
    [Fact]
    public void Byproduct_CoveringAFormTwice_IsReported()
    {
        var content = ValidBaseline();
        content.Byproducts.Add(new ByproductDefinition
        {
            Id = "byproduct.a", Material = "material.oak", Forms = new[] { "wood" }, Fallback = true,
        });
        content.Byproducts.Add(new ByproductDefinition
        {
            Id = "byproduct.b", Material = "material.oak", Forms = new[] { "wood" },
        });
        AssertHasProblem(content, "byproducts", "both cover form 'wood'");
    }

    /// <summary>Without a fallback, destroying an emergent form would return nothing at all.</summary>
    [Fact]
    public void ByproductTable_WithNoFallback_IsReported()
    {
        var content = ValidBaseline();
        content.Byproducts.Add(new ByproductDefinition
        {
            Id = "byproduct.only", Material = "material.oak", Forms = new[] { "wood" },
        });
        AssertHasProblem(content, "byproducts", "exactly one fallback");
    }

    /// <summary>The fallback covers emergent forms, not gaps in an unfinished table.</summary>
    [Fact]
    public void ByproductTable_MissingAnAuthoredForm_IsReported()
    {
        var content = ValidBaseline(); // the baseline material carries form:wood
        content.Byproducts.Add(new ByproductDefinition
        {
            Id = "byproduct.fallback", Material = "material.oak", Forms = new[] { "liquid" }, Fallback = true,
        });
        AssertHasProblem(content, "byproducts", "form 'wood' is used by the material library");
    }

    // --- The modifier registry (D-12) ----------------------------------------

    /// <summary>A scope dimension nothing supplies is a key nothing can ever resolve, so the
    /// dimension vocabulary is closed and checked at load like every other one.</summary>
    [Fact]
    public void ModifierKey_ScopedByAnUnknownDimension_IsReported()
    {
        var content = ValidBaseline();
        content.ModifierKeys.Add(new Dungeons.Modifiers.ModifierKeyDefinition
        {
            Id = "combat.damage.weather", Name = "Weather Damage", Family = "offence", ScopedBy = "weather",
        });
        AssertHasProblem(content, "modifier_keys", "unknown dimension 'weather'");
    }

    /// <summary>The cap is the entire point of marking a family dangerous. Leaving <c>max</c> off
    /// is how a family reaches certainty and stops being balanceable after the fact.</summary>
    [Fact]
    public void ModifierKey_MarkedDangerousWithNoCeiling_IsReported()
    {
        var content = ValidBaseline();
        content.ModifierKeys.Add(new Dungeons.Modifiers.ModifierKeyDefinition
        {
            Id = "combat.avoid.everything", Name = "Avoidance", Family = "defence",
            Kind = Dungeons.Modifiers.ModifierKind.Diminishing, Danger = true,
        });
        AssertHasProblem(content, "modifier_keys", "no max");
    }

    // --- The enemy framework (M2′c) ------------------------------------------

    private static ActorDefinition LayeredActor(
        string? family = "family.test", string? role = null, string? aiProfile = null,
        MoveGrantSpec[]? moves = null, AiRuleSpec[]? ai = null) => new()
        {
            Id = "actor.layered", Name = "Layered",
            Family = family, Role = role, AiProfile = aiProfile,
            Moves = moves ?? new[] { new MoveGrantSpec { Id = "move.strike" } },
            Ai = ai ?? Array.Empty<AiRuleSpec>(),
        };

    private static TestContent FrameworkBaseline()
    {
        var content = ValidBaseline();
        content.EnemyFamilies.Add(new EnemyFamilyDefinition { Id = "family.test", Name = "Test" });
        return content;
    }

    [Fact]
    public void Actor_ReferencingUnknownFamily_IsReported()
    {
        var content = ValidBaseline();
        content.Actors.Add(LayeredActor(family: "family.ghost"));
        AssertHasProblem(content, "actors", "unknown family");
    }

    [Fact]
    public void Actor_AuthoringAbsoluteAttributesAndAFamily_IsReported()
    {
        var content = FrameworkBaseline();
        content.Actors.Add(new ActorDefinition
        {
            Id = "actor.both", Name = "Both", Family = "family.test",
            Attributes = Dungeons.Characters.AttributeSet.Uniform(5),
            Moves = new[] { new MoveGrantSpec { Id = "move.strike" } },
        });
        AssertHasProblem(content, "actors", "use attribute_tweaks");
    }

    [Fact]
    public void AiRule_MatchingATagNoGrantedMoveCarries_IsReported()
    {
        var content = FrameworkBaseline();
        content.Actors.Add(LayeredActor(ai: new[] { new AiRuleSpec { MoveTag = "mech:stagger", Weight = 1 } }));
        AssertHasProblem(content, "actors", "none of its moves carry");
    }

    [Fact]
    public void AiRule_SettingBothMoveAndMoveTag_IsReported()
    {
        var content = FrameworkBaseline();
        content.Actors.Add(LayeredActor(ai: new[] { new AiRuleSpec { Move = "move.strike", MoveTag = "action:attack", Weight = 1 } }));
        AssertHasProblem(content, "actors", "exactly one of move/moveTag");
    }

    [Fact]
    public void Actor_GrantedAMoveRequiringEquipment_IsReported()
    {
        var content = FrameworkBaseline();
        content.Moves.Add(new MoveDefinition
        {
            Id = "move.armed", Name = "Armed Strike",
            Tags = new[] { "action:attack", "delivery:melee" },
            Requires = new[] { new Dungeons.Rules.ConditionSpec { Kind = "equippedTag", Text = "sword" } },
            Packets = new[] { new Packet(DamageType.Slashing, 8) },
        });
        content.Species.Add(new SpeciesDefinition { Id = "species.armed", Moves = new[] { new MoveGrantSpec { Id = "move.armed" } } });
        content.Actors.Add(LayeredActor(moves: new[] { new MoveGrantSpec { Id = "move.armed" } }));
        AssertHasProblem(content, "actors", "cannot satisfy equippedTag");
    }

    [Fact]
    public void AiProfile_AvoidRepeatOutsideZeroToOne_IsReported()
    {
        var content = FrameworkBaseline();
        content.AiProfiles.Add(new AiProfileDefinition { Id = "ai.jittery", Name = "Jittery", AvoidRepeatWeight = 1.5 });
        AssertHasProblem(content, "ai_profiles", "outside [0, 1]");
    }

    // --- Traits (C1a) ---------------------------------------------------------




    // --- Essences (C1b) -------------------------------------------------------



    // --- Techniques (M2′ acquisition) ----------------------------------------

    /// <summary>A technique that teaches a missing move is a dead item the player can learn
    /// nothing from — it must fail at load, not on the Learn click.</summary>
    [Fact]
    public void Technique_TeachingAnUnknownMove_IsReported()
    {
        var content = ValidBaseline();
        content.Techniques.Add(new Dungeons.Combat.TechniqueDefinition
        {
            Id = "technique.bad", Name = "Bad Manual", Teaches = "move.ghost",
        });
        AssertHasProblem(content, "techniques", "unknown move");
    }

    [Fact]
    public void Technique_TeachingNothing_IsReported()
    {
        var content = ValidBaseline();
        content.Techniques.Add(new Dungeons.Combat.TechniqueDefinition
        {
            Id = "technique.empty", Name = "Blank Pages",
        });
        AssertHasProblem(content, "techniques", "teaches nothing");
    }

    /// <summary>Techniques are a real granting source: a move only a technique teaches is
    /// reachable, not orphan content.</summary>
    [Fact]
    public void Technique_MakesItsMoveReachable()
    {
        var content = ValidBaseline();
        content.Moves.Add(new MoveDefinition
        {
            Id = "move.arcana", Name = "Arcana",
            Tags = new[] { "action:attack", "delivery:projectile" },
            Packets = new[] { new Packet(DamageType.Magic, 10) },
        });
        content.Techniques.Add(new Dungeons.Combat.TechniqueDefinition
        {
            Id = "technique.arcana", Name = "Grimoire: Arcana", Teaches = "move.arcana",
        });
        Assert.DoesNotContain(content.Validate(), p => p.Message.Contains("move.arcana"));
    }

    // --- Hideout stations ----------------------------------------------------
    //
    // A station is routing, so every rule is about reachability. The two that matter are the
    // pair: a profession with no station cannot be trained, and a profession with two stations
    // gets its ladder drawn twice — both are silent until a player goes looking.

    [Fact]
    public void Station_RoutingToRealContent_IsAccepted()
    {
        var content = ValidBaseline();
        content.Forms.Add(new EquipmentBlueprintDefinition
        {
            Id = "form.plank",
            Name = "Plank",
            Type = EquipmentSlot.Body,
            Tags = new[] { "armor" },
            Slots = new() { ["shell"] = new BlueprintSlot { RequiresTags = new[] { "form:wood" } } },
        });
        content.Stations.Add(Station(hasAssembly: true));

        Assert.Empty(content.Validate());
    }

    [Fact]
    public void Station_HostingUnknownProfession_IsFlagged()
    {
        var content = ValidBaseline();
        content.Stations.Add(Station(professions: new[] { "prof.forestry", "prof.nonesuch" }));

        AssertHasProblem(content, "stations", "unknown profession 'prof.nonesuch'");
    }



    [Fact]
    public void Station_HostingNoProfession_IsFlagged()
    {
        var content = ValidBaseline();
        content.Stations.Add(Station());
        content.Stations.Add(Station(id: "station.empty", professions: Array.Empty<string>()));

        AssertHasProblem(content, "stations", "hosts no profession");
    }

    [Fact]
    public void Profession_ReachableFromNoStation_IsFlagged()
    {
        var content = ValidBaseline();
        content.Professions.Add(new ProfessionDefinition { Id = "prof.orphan" });
        content.Stations.Add(Station());

        AssertHasProblem(content, "stations", "prof.orphan has no station");
    }



    [Fact]
    public void Profession_HostedByTwoStations_IsFlagged()
    {
        var content = ValidBaseline();
        content.Stations.Add(Station());
        content.Stations.Add(Station(id: "station.second"));

        AssertHasProblem(content, "stations", "already belongs to station.test");
    }

    /// <summary>A well-formed station; each argument left unset keeps its valid default, so a
    /// test breaks exactly one rule.</summary>
    private static Dungeons.Hideout.StationDefinition Station(
        string id = "station.test",
        IReadOnlyList<string>? professions = null,
        bool hasAssembly = false) => new()
        {
            Id = id,
            Name = "Test Station",
            Description = "Where the test happens.",
            Professions = professions ?? new[] { "prof.forestry" },
            HasAssembly = hasAssembly,
        };

    // --- Helpers -------------------------------------------------------------

    private static DataStore<T> Load<T>(string subfolder) where T : IDefinition
    {
        return TestPaths.LoadStore<T>(subfolder);
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
        content.Moves.Add(new MoveDefinition { Id = "move.strike", Name = "Strike", Tags = new[] { "action:attack", "delivery:melee" }, Packets = new[] { new Packet(DamageType.Slashing, 8) } });
        content.Species.Add(new SpeciesDefinition { Id = "species.baseline", Moves = new[] { new MoveGrantSpec { Id = "move.strike" } } });
        return content;
    }

    /// <summary>A <see cref="ContentBundle"/> under construction, with the real property registry
    /// loaded so material/equipment property validation has its source of truth.</summary>
    private sealed class TestContent
    {
        private readonly ContentBundle _bundle = new();

        public DataStore<MaterialDefinition> Materials => _bundle.Materials;
        public DataStore<ByproductDefinition> Byproducts => _bundle.Byproducts;
        public DataStore<ProfessionDefinition> Professions => _bundle.Professions;
        public DataStore<ProfessionActionDefinition> Actions => _bundle.Actions;
        public DataStore<CraftingInteractionDefinition> Interactions => _bundle.Interactions;
        public DataStore<Dungeons.Combat.MoveDefinition> Moves => _bundle.Moves;
        public DataStore<Dungeons.Combat.MoveModifierDefinition> MoveModifiers => _bundle.MoveModifiers;
        public DataStore<ActorDefinition> Actors => _bundle.Actors;
        public DataStore<EnemyFamilyDefinition> EnemyFamilies => _bundle.EnemyFamilies;
        public DataStore<CombatRoleDefinition> EnemyRoles => _bundle.EnemyRoles;
        public DataStore<AiProfileDefinition> AiProfiles => _bundle.AiProfiles;
        public DataStore<RealmDefinition> Realms => _bundle.Realms;
        public DataStore<ConsumableDefinition> Consumables => _bundle.Consumables;
        public DataStore<Dungeons.Combat.TechniqueDefinition> Techniques => _bundle.Techniques;
        public DataStore<EquipmentDefinition> Equipment => _bundle.Equipment;
        public DataStore<EquipmentBlueprintDefinition> Forms => _bundle.Forms;
        public DataStore<Dungeons.Hideout.StationDefinition> Stations => _bundle.Stations;
        public DataStore<Dungeons.Characters.Composition.SpeciesDefinition> Species => _bundle.Species;
        public DataStore<Dungeons.Modifiers.ModifierKeyDefinition> ModifierKeys => _bundle.ModifierKeys;

        public IReadOnlyList<ContentProblem> Validate() => ContentValidator.Validate(_bundle);
    }
}
