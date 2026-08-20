using ContentStudio.Models;
using Dungeons.Affixes;
using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Hideout;
using Dungeons.Items;
using Dungeons.Loot;
using Dungeons.Modifiers;
using Dungeons.Professions;
using Dungeons.Realms;

namespace ContentStudio.Services;

/// <summary>
/// The authoritative list of authored content types, mirroring the game's
/// <c>ContentLoader.LoadAll</c> folder-for-folder. Only folders that exist in the opened
/// project are exposed to the UI — the sidebar never shows systems the game does not have.
/// </summary>
public static class ContentTypeRegistry
{
    public static readonly IReadOnlyList<ContentTypeDescriptor> All = new List<ContentTypeDescriptor>
    {
        // ── Combat ──────────────────────────────────────────────────────────────────────────
        new()
        {
            TypeId = "actors", Folder = "actors", DefinitionType = typeof(ActorDefinition),
            DisplayName = "Enemies", SingularName = "Enemy", NavigationGroup = "Combat", IdPrefix = "actor.",
            ListColumns = new[] { "family", "role", "ai_profile" },
            ValidatorCategories = new[] { "actors" },
            Description = "Actors composed from a family (the body), a role (the archetype delta) and an AI profile (the brain).",
        },
        new()
        {
            TypeId = "enemy_families", Folder = "enemy_families", DefinitionType = typeof(EnemyFamilyDefinition),
            DisplayName = "Enemy Families", SingularName = "Enemy Family", NavigationGroup = "Combat", IdPrefix = "family.",
            ListColumns = new[] { "resolve", "loot_table" },
            ValidatorCategories = new[] { "enemy_families" },
            Description = "Physiology layer: attributes, resources, lane resistances and the anatomy half of the drop.",
        },
        new()
        {
            TypeId = "enemy_roles", Folder = "enemy_roles", DefinitionType = typeof(CombatRoleDefinition),
            DisplayName = "Enemy Roles", SingularName = "Enemy Role", NavigationGroup = "Combat", IdPrefix = "role.",
            ListColumns = new[] { "ai_profile", "armor", "resolve" },
            ValidatorCategories = new[] { "enemy_roles" },
            Description = "Combat-archetype deltas layered over any family; family-agnostic on purpose.",
        },
        new()
        {
            TypeId = "ai_profiles", Folder = "ai_profiles", DefinitionType = typeof(AiProfileDefinition),
            DisplayName = "AI Profiles", SingularName = "AI Profile", NavigationGroup = "Combat", IdPrefix = "ai.",
            ListColumns = new[] { "avoid_repeat_weight" },
            ValidatorCategories = new[] { "ai_profiles" },
            Description = "Reusable brains: weighted rules over the shared condition vocabulary, matching moves by id or tag.",
        },
        new()
        {
            TypeId = "auto_combat", Folder = "auto_combat", DefinitionType = typeof(AutoCombatProfileDefinition),
            DisplayName = "Auto-Combat Brains", SingularName = "Auto-Combat Brain", NavigationGroup = "Combat", IdPrefix = "auto.",
            ListColumns = new[] { "reaction_ticks" },
            ValidatorCategories = new[] { "auto_combat" },
            Description = "The player's automated pilot. Its only handicap is reaction_ticks — never a damage modifier.",
        },
        new()
        {
            TypeId = "moves", Folder = "moves", DefinitionType = typeof(MoveDefinition),
            DisplayName = "Moves", SingularName = "Move", NavigationGroup = "Combat", IdPrefix = "move.",
            ListColumns = new[] { "kind", "stagger_power" },
            ValidatorCategories = new[] { "moves" },
            Description = "One data shape for everything a combatant does, both sides of the fight.",
        },
        new()
        {
            TypeId = "move_modifiers", Folder = "move_modifiers", DefinitionType = typeof(MoveModifierDefinition),
            DisplayName = "Move Modifiers", SingularName = "Move Modifier", NavigationGroup = "Combat", IdPrefix = "movemod.",
            ValidatorCategories = new[] { "move_modifiers" },
            Description = "Match + ops over the closed 11-op vocabulary; how items and components reshape moves.",
        },
        new()
        {
            TypeId = "statuses", Folder = "statuses", DefinitionType = typeof(StatusDefinition),
            DisplayName = "Statuses", SingularName = "Status", NavigationGroup = "Combat", IdPrefix = "status.",
            ListColumns = new[] { "category", "stack_policy", "duration_ticks" },
            ValidatorCategories = new[] { "statuses" },
            Description = "Data-driven ailments, impairments, controls and states — there is no C# class per status.",
        },
        new()
        {
            TypeId = "techniques", Folder = "techniques", DefinitionType = typeof(TechniqueDefinition),
            DisplayName = "Techniques", SingularName = "Technique", NavigationGroup = "Combat", IdPrefix = "technique.",
            ListColumns = new[] { "teaches" },
            ValidatorCategories = new[] { "techniques" },
            Description = "Learnable move-teaching items; the id doubles as the stackable inventory item id.",
        },

        // ── Crafting ────────────────────────────────────────────────────────────────────────
        new()
        {
            TypeId = "materials", Folder = "materials", DefinitionType = typeof(MaterialDefinition),
            DisplayName = "Materials", SingularName = "Material", NavigationGroup = "Crafting", IdPrefix = "material.",
            ValidatorCategories = new[] { "materials", "tags" },
            Description = "The ingredient library the entire crafting engine operates on. Properties are 0–100; absent means zero.",
        },
        new()
        {
            TypeId = "properties", Folder = "properties", DefinitionType = typeof(PropertyDefinition),
            DisplayName = "Properties", SingularName = "Property", NavigationGroup = "Crafting", IdPrefix = "",
            ListColumns = new[] { "role", "family" },
            ValidatorCategories = new[] { "properties" },
            Description = "The property registry — the single source of truth for what a valid property name is.",
        },
        new()
        {
            TypeId = "identities", Folder = "identities", DefinitionType = typeof(IdentityDefinition),
            DisplayName = "Identities", SingularName = "Identity", NavigationGroup = "Crafting", IdPrefix = "identity.",
            ListColumns = new[] { "cluster" },
            ValidatorCategories = new[] { "identity" },
            Description = "The identity roster (D44) — the named doors the crafting redesign moves around. Changing the roster is a design decision; a test pins its membership.",
        },
        new()
        {
            TypeId = "signature_triggers", Folder = "signature_triggers", DefinitionType = typeof(SignatureTriggerDefinition),
            DisplayName = "Signature Triggers", SingularName = "Signature Trigger", NavigationGroup = "Crafting", IdPrefix = "",
            ListColumns = new[] { "event", "standing" },
            ValidatorCategories = new[] { "signature_trigger" },
            Description = "Signature grammar: when a sentence fires. Bare-key ids; each binds to a published game event or is the one standing shape (D30 fence).",
        },
        new()
        {
            TypeId = "signature_behaviors", Folder = "signature_behaviors", DefinitionType = typeof(SignatureBehaviorDefinition),
            DisplayName = "Signature Behaviors", SingularName = "Signature Behavior", NavigationGroup = "Crafting", IdPrefix = "",
            ValidatorCategories = new[] { "signature_behavior" },
            Description = "Signature grammar: how a payload is delivered. Only machinery-backed verbs ship; detonate/spread/bloom wait for their effect kinds.",
        },
        new()
        {
            TypeId = "signature_themes", Folder = "signature_themes", DefinitionType = typeof(SignatureThemeDefinition),
            DisplayName = "Signature Themes", SingularName = "Signature Theme", NavigationGroup = "Crafting", IdPrefix = "",
            ValidatorCategories = new[] { "signature_theme" },
            Description = "Signature grammar: hidden scoring metadata only — themes resonate between sources and are never player-facing.",
        },
        new()
        {
            TypeId = "processes", Folder = "processes", DefinitionType = typeof(CraftingActionDefinition),
            DisplayName = "Crafting Actions", SingularName = "Crafting Action", NavigationGroup = "Crafting", IdPrefix = "process.",
            ListColumns = new[] { "profession", "medium", "severity" },
            ValidatorCategories = new[] { "processes" },
            Description = "The bench verbs (grind, steep, forge-infuse…): channel rates, medium, severity, gates.",
        },
        new()
        {
            TypeId = "traits", Folder = "traits", DefinitionType = typeof(TraitDefinition),
            DisplayName = "Traits", SingularName = "Trait", NavigationGroup = "Crafting", IdPrefix = "trait.",
            ListColumns = new[] { "category" },
            ValidatorCategories = new[] { "traits" },
            Description = "Emergent material qualities: birth conditions, magnitude sources, merges, drawbacks.",
        },
        new()
        {
            TypeId = "essences", Folder = "essences", DefinitionType = typeof(EssenceDefinition),
            DisplayName = "Essences", SingularName = "Essence", NavigationGroup = "Crafting", IdPrefix = "essence.",
            ListColumns = new[] { "anchor" },
            ValidatorCategories = new[] { "essences" },
            Description = "The seven aspects: anchor property, oppositions, capacity and strain.",
        },
        new()
        {
            TypeId = "byproducts", Folder = "byproducts", DefinitionType = typeof(ByproductDefinition),
            DisplayName = "Byproducts", SingularName = "Byproduct", NavigationGroup = "Crafting", IdPrefix = "byproduct.",
            ListColumns = new[] { "material", "fallback" },
            ValidatorCategories = new[] { "byproducts" },
            Description = "What destruction leaves behind, keyed by the destroyed material's form tags.",
        },
        new()
        {
            TypeId = "forms", Folder = "forms", DefinitionType = typeof(EquipmentBlueprintDefinition),
            DisplayName = "Equipment Forms", SingularName = "Equipment Form", NavigationGroup = "Crafting", IdPrefix = "form.",
            ListColumns = new[] { "type", "trait_cap" },
            ValidatorCategories = new[] { "forms" },
            Description = "Fabrication blueprints: slots with tag gates and mass shares, the stat map, granted moves.",
        },
        new()
        {
            TypeId = "affixes", Folder = "affixes", DefinitionType = typeof(AffixDefinition),
            DisplayName = "Item Modifiers", SingularName = "Item Modifier", NavigationGroup = "Crafting", IdPrefix = "affix.",
            ListColumns = new[] { "slot", "family", "class" },
            ValidatorCategories = new[] { "affix" },
            Description = "Prefixes, suffixes and innates rolled from the genome: eligibility, weights, tiers, grants.",
        },
        new()
        {
            TypeId = "equipment", Folder = "equipment", DefinitionType = typeof(EquipmentDefinition),
            DisplayName = "Equipment", SingularName = "Equipment Piece", NavigationGroup = "Crafting", IdPrefix = "equip.",
            ListColumns = new[] { "slot" },
            ValidatorCategories = new[] { "equipment" },
            Description = "The four authored starter pieces. Fabricated equipment is generated at the bench and lives in the save.",
        },
        new()
        {
            TypeId = "consumables", Folder = "consumables", DefinitionType = typeof(ConsumableDefinition),
            DisplayName = "Consumables", SingularName = "Consumable", NavigationGroup = "Crafting", IdPrefix = "consumable.",
            ListColumns = new[] { "healAmount" },
            ValidatorCategories = new[] { "consumables" },
            Description = "Usable items. Currently the Healing Salve shim, until consumable forms land.",
        },
        new()
        {
            TypeId = "crafting_interactions", Folder = "crafting_interactions", DefinitionType = typeof(CraftingInteractionDefinition),
            DisplayName = "Interactions", SingularName = "Interaction", NavigationGroup = "Crafting", IdPrefix = "interaction.",
            ValidatorCategories = new[] { "crafting" },
            Description = "The legacy fixed-recipe path; dies with P5c once consumable forms exist.",
        },
        new()
        {
            TypeId = "name_grammar", Folder = "name_grammar", DefinitionType = typeof(NameWordDefinition),
            DisplayName = "Name Grammar", SingularName = "Name Word", NavigationGroup = "Crafting", IdPrefix = "",
            ValidatorCategories = new[] { "name_grammar" },
            Description = "The word ladders emergent material names are built from.",
        },

        // ── Professions ─────────────────────────────────────────────────────────────────────
        new()
        {
            TypeId = "professions", Folder = "professions", DefinitionType = typeof(ProfessionDefinition),
            DisplayName = "Professions", SingularName = "Profession", NavigationGroup = "Professions", IdPrefix = "profession.",
            ListColumns = new[] { "category" },
            ValidatorCategories = new[] { "professions" },
            Description = "The twenty-profession roster; every one must consume and feed the wider ecosystem.",
        },
        new()
        {
            TypeId = "profession_actions", Folder = "profession_actions", DefinitionType = typeof(ProfessionActionDefinition),
            DisplayName = "Profession Actions", SingularName = "Profession Action", NavigationGroup = "Professions", IdPrefix = "action.",
            ListColumns = new[] { "professionId", "requiredLevel", "baseIntervalTicks", "experience" },
            ValidatorCategories = new[] { "profession_actions" },
            Description = "The ladders: interval, XP, inputs, outputs, bonus rolls, opportunities and drop tables.",
        },
        new()
        {
            TypeId = "mastery", Folder = "mastery", DefinitionType = typeof(MasteryBenefitDefinition),
            DisplayName = "Mastery Ladder", SingularName = "Mastery Rung", NavigationGroup = "Professions", IdPrefix = "mastery.",
            ListColumns = new[] { "kind", "unlock_level", "per_level", "max" },
            ValidatorCategories = new[] { "mastery" },
            Description = "What repeating one action buys — the shared six-rung benefit ladder.",
        },
        new()
        {
            TypeId = "synergies", Folder = "synergies", DefinitionType = typeof(ProfessionSynergyDefinition),
            DisplayName = "Synergies", SingularName = "Synergy", NavigationGroup = "Professions", IdPrefix = "synergy.",
            ListColumns = new[] { "kind", "source", "target" },
            ValidatorCategories = new[] { "synergies" },
            Description = "Cross-profession and global bonuses paying into the same six quantities as mastery.",
        },
        new()
        {
            TypeId = "training_obstacles", Folder = "training_obstacles", DefinitionType = typeof(TrainingObstacleDefinition),
            DisplayName = "Training Obstacles", SingularName = "Training Obstacle", NavigationGroup = "Professions", IdPrefix = "obstacle.",
            ListColumns = new[] { "slot", "requiredLevel" },
            ValidatorCategories = new[] { "training_obstacles" },
            Description = "The Agility course pieces: five slots, one obstacle each.",
        },
        new()
        {
            TypeId = "stations", Folder = "stations", DefinitionType = typeof(StationDefinition),
            DisplayName = "Stations", SingularName = "Station", NavigationGroup = "Professions", IdPrefix = "station.",
            ValidatorCategories = new[] { "stations" },
            Description = "The Hideout routing table: where each profession, crafting action and blueprint lives.",
        },

        // ── World ───────────────────────────────────────────────────────────────────────────
        new()
        {
            TypeId = "realms", Folder = "realms", DefinitionType = typeof(RealmDefinition),
            DisplayName = "Realms", SingularName = "Realm", NavigationGroup = "World", IdPrefix = "realm.",
            ValidatorCategories = new[] { "realms" },
            Description = "The location graphs: nodes, symmetric edges, depths, and what each node offers.",
        },
        new()
        {
            TypeId = "loot_tables", Folder = "loot_tables", DefinitionType = typeof(LootTableDefinition),
            DisplayName = "Loot Tables", SingularName = "Loot Table", NavigationGroup = "World", IdPrefix = "loot.",
            ValidatorCategories = new[] { "loot_tables" },
            Description = "One table shape for every payer: always drops, chance drops, weighted draws, gold.",
        },

        // ── Character ───────────────────────────────────────────────────────────────────────
        new()
        {
            TypeId = "species", Folder = "species", DefinitionType = typeof(SpeciesDefinition),
            DisplayName = "Species", SingularName = "Species", NavigationGroup = "Character", IdPrefix = "species.",
            ValidatorCategories = new[] { "species" },
            Description = "The species component of the four-part class combinator.",
        },
        new()
        {
            TypeId = "classes", Folder = "classes", DefinitionType = typeof(BaseClassDefinition),
            DisplayName = "Base Classes", SingularName = "Base Class", NavigationGroup = "Character", IdPrefix = "class.",
            ListColumns = new[] { "primaryResource", "engine" },
            ValidatorCategories = new[] { "classes" },
            Description = "The Bases: growth budgets, gauges, engines and weaknesses.",
        },
        new()
        {
            TypeId = "prefixes", Folder = "prefixes", DefinitionType = typeof(PrefixDefinition),
            DisplayName = "Class Prefixes", SingularName = "Class Prefix", NavigationGroup = "Character", IdPrefix = "prefix.",
            ValidatorCategories = new[] { "prefixes" },
            Description = "Mechanic components; a prefix may never name a Base.",
        },
        new()
        {
            TypeId = "suffixes", Folder = "suffixes", DefinitionType = typeof(SuffixDefinition),
            DisplayName = "Class Suffixes", SingularName = "Class Suffix", NavigationGroup = "Character", IdPrefix = "suffix.",
            ListColumns = new[] { "format" },
            ValidatorCategories = new[] { "suffixes" },
            Description = "Expression components: one expression per channel, each with a drawback.",
        },
        new()
        {
            TypeId = "name_formats", Folder = "name_formats", DefinitionType = typeof(NameFormatDefinition),
            DisplayName = "Name Formats", SingularName = "Name Format", NavigationGroup = "Character", IdPrefix = "",
            ValidatorCategories = new[] { "name_formats" },
            Description = "The nine grammars class names are rendered through. Presentation only.",
        },

        // ── System ──────────────────────────────────────────────────────────────────────────
        new()
        {
            TypeId = "modifier_keys", Folder = "modifier_keys", DefinitionType = typeof(ModifierKeyDefinition),
            DisplayName = "Modifier Keys", SingularName = "Modifier Key", NavigationGroup = "System", IdPrefix = "",
            ListColumns = new[] { "kind", "family" },
            ValidatorCategories = new[] { "modifier_keys" },
            Description = "The data-defined modification vocabulary: kinds, clamps, scopes and danger flags.",
        },
    };

    private static readonly Dictionary<string, ContentTypeDescriptor> ByTypeId =
        All.ToDictionary(descriptor => descriptor.TypeId, StringComparer.Ordinal);

    public static ContentTypeDescriptor? Find(string typeId) => ByTypeId.GetValueOrDefault(typeId);

    public static ContentTypeDescriptor Require(string typeId) =>
        Find(typeId) ?? throw new KeyNotFoundException($"Unknown content type '{typeId}'.");
}
