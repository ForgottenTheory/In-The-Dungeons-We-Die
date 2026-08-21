using System.Text.Json;
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
/// Builds a real <see cref="ContentBundle"/> from the workspace's current (unsaved) state by
/// pushing every record through the same <see cref="DataStore{T}"/> parsing the game uses.
/// Anything the game could not load is reported per record — "content that cannot load" is a
/// first-class validation result, not a crash.
/// </summary>
public sealed class GameBundleService
{
    public sealed record BuildResult(ContentBundle Bundle, List<ValidationProblem> LoadProblems);

    public BuildResult Build(ContentWorkspace workspace)
    {
        var problems = new List<ValidationProblem>();

        var bundle = new ContentBundle
        {
            Materials = BuildStore<MaterialDefinition>(workspace, "materials", problems),
            Properties = BuildStore<PropertyDefinition>(workspace, "properties", problems),
            Identities = BuildStore<IdentityDefinition>(workspace, "identities", problems),
            SignatureTriggers = BuildStore<SignatureTriggerDefinition>(workspace, "signature_triggers", problems),
            SignatureBehaviors = BuildStore<SignatureBehaviorDefinition>(workspace, "signature_behaviors", problems),
            SignatureThemes = BuildStore<SignatureThemeDefinition>(workspace, "signature_themes", problems),
            SignaturePayloads = BuildStore<SignaturePayloadDefinition>(workspace, "signature_payloads", problems),
            VerbActions = BuildStore<VerbActionDefinition>(workspace, "verb_actions", problems),
            CraftingActions = BuildStore<CraftingActionDefinition>(workspace, "processes", problems),
            Byproducts = BuildStore<ByproductDefinition>(workspace, "byproducts", problems),
            Traits = BuildStore<TraitDefinition>(workspace, "traits", problems),
            Essences = BuildStore<EssenceDefinition>(workspace, "essences", problems),
            Forms = BuildStore<EquipmentBlueprintDefinition>(workspace, "forms", problems),
            Affixes = BuildStore<AffixDefinition>(workspace, "affixes", problems),
            NameGrammar = BuildStore<NameWordDefinition>(workspace, "name_grammar", problems),
            ModifierKeys = BuildStore<ModifierKeyDefinition>(workspace, "modifier_keys", problems),
            NameFormats = BuildStore<NameFormatDefinition>(workspace, "name_formats", problems),
            Professions = BuildStore<ProfessionDefinition>(workspace, "professions", problems),
            Actions = BuildStore<ProfessionActionDefinition>(workspace, "profession_actions", problems),
            TrainingObstacles = BuildStore<TrainingObstacleDefinition>(workspace, "training_obstacles", problems),
            MasteryBenefits = BuildStore<MasteryBenefitDefinition>(workspace, "mastery", problems),
            Synergies = BuildStore<ProfessionSynergyDefinition>(workspace, "synergies", problems),
            AutoCombatProfiles = BuildStore<AutoCombatProfileDefinition>(workspace, "auto_combat", problems),
            Stations = BuildStore<StationDefinition>(workspace, "stations", problems),
            Interactions = BuildStore<CraftingInteractionDefinition>(workspace, "crafting_interactions", problems),
            Moves = BuildStore<MoveDefinition>(workspace, "moves", problems),
            MoveModifiers = BuildStore<MoveModifierDefinition>(workspace, "move_modifiers", problems),
            Actors = BuildStore<ActorDefinition>(workspace, "actors", problems),
            EnemyFamilies = BuildStore<EnemyFamilyDefinition>(workspace, "enemy_families", problems),
            EnemyRoles = BuildStore<CombatRoleDefinition>(workspace, "enemy_roles", problems),
            AiProfiles = BuildStore<AiProfileDefinition>(workspace, "ai_profiles", problems),
            Realms = BuildStore<RealmDefinition>(workspace, "realms", problems),
            LootTables = BuildStore<LootTableDefinition>(workspace, "loot_tables", problems),
            Consumables = BuildStore<ConsumableDefinition>(workspace, "consumables", problems),
            Techniques = BuildStore<TechniqueDefinition>(workspace, "techniques", problems),
            Equipment = BuildStore<EquipmentDefinition>(workspace, "equipment", problems),
            Statuses = BuildStore<StatusDefinition>(workspace, "statuses", problems),
            Species = BuildStore<SpeciesDefinition>(workspace, "species", problems),
            Classes = BuildStore<BaseClassDefinition>(workspace, "classes", problems),
            Prefixes = BuildStore<PrefixDefinition>(workspace, "prefixes", problems),
            Suffixes = BuildStore<SuffixDefinition>(workspace, "suffixes", problems),
        };

        return new BuildResult(bundle, problems);
    }

    private static DataStore<T> BuildStore<T>(ContentWorkspace workspace, string typeId, List<ValidationProblem> problems)
        where T : IDefinition
    {
        var store = new DataStore<T>();
        foreach (var record in workspace.RecordsOf(typeId))
        {
            try
            {
                store.LoadOne(record.Value.ToJsonString());
            }
            catch (DuplicateDefinitionException)
            {
                problems.Add(new ValidationProblem("error", "load", typeId,
                    $"Duplicate id '{record.Id}' — also defined in another file.", record.Id, typeId, record.File.RelativePath));
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException)
            {
                problems.Add(new ValidationProblem("error", "load", typeId,
                    $"{record.Id}: the game cannot parse this record — {RootMessage(exception)}",
                    record.Id, typeId, record.File.RelativePath));
            }
        }
        return store;
    }

    private static string RootMessage(Exception exception) =>
        exception.InnerException is { } inner ? inner.Message : exception.Message;
}
