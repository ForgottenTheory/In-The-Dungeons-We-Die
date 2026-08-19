using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Crafting;
using Dungeons.Hideout;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Realms;

namespace Dungeons.Content;

/// <summary>
/// All loaded content definition stores, in one carrier. This is the single place a new
/// content type is registered: add a store here, load it in <c>ContentLoader.LoadAll</c>,
/// and (if it has cross-references) validate it in <see cref="ContentValidator"/> — instead
/// of threading another positional argument through every call site. It holds only loaded
/// definition stores; runtime state lives elsewhere.
/// </summary>
public sealed class ContentBundle
{
    public DataStore<MaterialDefinition> Materials { get; init; } = new();
    public DataStore<PropertyDefinition> Properties { get; init; } = new();
    public DataStore<CraftingActionDefinition> CraftingActions { get; init; } = new();
    public DataStore<ByproductDefinition> Byproducts { get; init; } = new();
    public DataStore<TraitDefinition> Traits { get; init; } = new();
    public DataStore<EssenceDefinition> Essences { get; init; } = new();
    public DataStore<EquipmentBlueprintDefinition> Forms { get; init; } = new();
    public DataStore<Affixes.AffixDefinition> Affixes { get; init; } = new();
    public DataStore<NameWordDefinition> NameGrammar { get; init; } = new();
    public DataStore<Modifiers.ModifierKeyDefinition> ModifierKeys { get; init; } = new();
    public DataStore<Dungeons.Characters.Composition.NameFormatDefinition> NameFormats { get; init; } = new();
    public DataStore<ProfessionDefinition> Professions { get; init; } = new();
    public DataStore<ProfessionActionDefinition> Actions { get; init; } = new();
    public DataStore<TrainingObstacleDefinition> TrainingObstacles { get; init; } = new();

    /// <summary>The shared mastery ladder — what repeating one action buys (Phase 8).</summary>
    public DataStore<MasteryBenefitDefinition> MasteryBenefits { get; init; } = new();

    /// <summary>Hideout stations — the player-facing entry points into the professions,
    /// the crafting bench and equipment assembly. Routing only; they own no rules.</summary>
    public DataStore<StationDefinition> Stations { get; init; } = new();

    public DataStore<CraftingInteractionDefinition> Interactions { get; init; } = new();
    public DataStore<MoveDefinition> Moves { get; init; } = new();
    public DataStore<MoveModifierDefinition> MoveModifiers { get; init; } = new();
    public DataStore<ActorDefinition> Actors { get; init; } = new();
    public DataStore<EnemyFamilyDefinition> EnemyFamilies { get; init; } = new();
    public DataStore<CombatRoleDefinition> EnemyRoles { get; init; } = new();
    public DataStore<AiProfileDefinition> AiProfiles { get; init; } = new();
    public DataStore<RealmDefinition> Realms { get; init; } = new();

    /// <summary>Drop tables (M6). One shape for every loot source in the game — enemies,
    /// gathering nodes, chests, profession actions — composed by nesting rather than copied.</summary>
    public DataStore<Dungeons.Loot.LootTableDefinition> LootTables { get; init; } = new();
    public DataStore<ConsumableDefinition> Consumables { get; init; } = new();
    public DataStore<TechniqueDefinition> Techniques { get; init; } = new();
    public DataStore<EquipmentDefinition> Equipment { get; init; } = new();

    /// <summary>Statuses (E2). Data-driven — there is no C# class per ailment.</summary>
    public DataStore<Dungeons.Combat.StatusDefinition> Statuses { get; init; } = new();
    public DataStore<SpeciesDefinition> Species { get; init; } = new();
    public DataStore<BaseClassDefinition> Classes { get; init; } = new();
    public DataStore<PrefixDefinition> Prefixes { get; init; } = new();
    public DataStore<SuffixDefinition> Suffixes { get; init; } = new();
}
