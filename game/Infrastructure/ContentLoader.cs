using System.Collections.Generic;
using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Realms;
using Godot;

namespace Dungeons.Game.Infrastructure;

/// <summary>
/// The Godot-side bridge that reads JSON content from <c>res://</c> and feeds the
/// raw text into an engine-independent <see cref="DataStore{T}"/>. Core never sees
/// a Godot path — file access lives entirely on this side of the boundary.
/// </summary>
public static class ContentLoader
{
    /// <summary>
    /// Loads every content type from <paramref name="dataRoot"/> into one <see cref="ContentBundle"/>.
    /// This is the single registration point: a new content type is one line here plus a store on
    /// the bundle (convention: folder name == the content type). Content is parsed once, here.
    /// </summary>
    public static ContentBundle LoadAll(string dataRoot) => new()
    {
        Materials = LoadDefinitions<MaterialDefinition>($"{dataRoot}/materials"),
        Properties = LoadDefinitions<PropertyDefinition>($"{dataRoot}/properties"),
        Identities = LoadDefinitions<IdentityDefinition>($"{dataRoot}/identities"),
        SignatureTriggers = LoadDefinitions<SignatureTriggerDefinition>($"{dataRoot}/signature_triggers"),
        SignatureBehaviors = LoadDefinitions<SignatureBehaviorDefinition>($"{dataRoot}/signature_behaviors"),
        SignatureThemes = LoadDefinitions<SignatureThemeDefinition>($"{dataRoot}/signature_themes"),
        VerbActions = LoadDefinitions<VerbActionDefinition>($"{dataRoot}/verb_actions"),
        CraftingActions = LoadDefinitions<CraftingActionDefinition>($"{dataRoot}/processes"),
        Byproducts = LoadDefinitions<ByproductDefinition>($"{dataRoot}/byproducts"),
        Traits = LoadDefinitions<TraitDefinition>($"{dataRoot}/traits"),
        Essences = LoadDefinitions<EssenceDefinition>($"{dataRoot}/essences"),
        Forms = LoadDefinitions<EquipmentBlueprintDefinition>($"{dataRoot}/forms"),
        Affixes = LoadDefinitions<Dungeons.Affixes.AffixDefinition>($"{dataRoot}/affixes"),
        NameGrammar = LoadDefinitions<NameWordDefinition>($"{dataRoot}/name_grammar"),
        ModifierKeys = LoadDefinitions<Dungeons.Modifiers.ModifierKeyDefinition>($"{dataRoot}/modifier_keys"),
        NameFormats = LoadDefinitions<NameFormatDefinition>($"{dataRoot}/name_formats"),
        Professions = LoadDefinitions<ProfessionDefinition>($"{dataRoot}/professions"),
        Actions = LoadDefinitions<ProfessionActionDefinition>($"{dataRoot}/profession_actions"),
        TrainingObstacles = LoadDefinitions<TrainingObstacleDefinition>($"{dataRoot}/training_obstacles"),
        MasteryBenefits = LoadDefinitions<MasteryBenefitDefinition>($"{dataRoot}/mastery"),
        Synergies = LoadDefinitions<ProfessionSynergyDefinition>($"{dataRoot}/synergies"),
        Stations = LoadDefinitions<Dungeons.Hideout.StationDefinition>($"{dataRoot}/stations"),
        Interactions = LoadDefinitions<CraftingInteractionDefinition>($"{dataRoot}/crafting_interactions"),
        Moves = LoadDefinitions<MoveDefinition>($"{dataRoot}/moves"),
        MoveModifiers = LoadDefinitions<MoveModifierDefinition>($"{dataRoot}/move_modifiers"),
        Actors = LoadDefinitions<ActorDefinition>($"{dataRoot}/actors"),
        EnemyFamilies = LoadDefinitions<EnemyFamilyDefinition>($"{dataRoot}/enemy_families"),
        EnemyRoles = LoadDefinitions<CombatRoleDefinition>($"{dataRoot}/enemy_roles"),
        AiProfiles = LoadDefinitions<AiProfileDefinition>($"{dataRoot}/ai_profiles"),
        AutoCombatProfiles = LoadDefinitions<AutoCombatProfileDefinition>($"{dataRoot}/auto_combat"),
        Realms = LoadDefinitions<RealmDefinition>($"{dataRoot}/realms"),
        LootTables = LoadDefinitions<Dungeons.Loot.LootTableDefinition>($"{dataRoot}/loot_tables"),
        Consumables = LoadDefinitions<ConsumableDefinition>($"{dataRoot}/consumables"),
        Techniques = LoadDefinitions<TechniqueDefinition>($"{dataRoot}/techniques"),
        Equipment = LoadDefinitions<EquipmentDefinition>($"{dataRoot}/equipment"),
        Statuses = LoadDefinitions<Dungeons.Combat.StatusDefinition>($"{dataRoot}/statuses"),
        Species = LoadDefinitions<SpeciesDefinition>($"{dataRoot}/species"),
        Classes = LoadDefinitions<BaseClassDefinition>($"{dataRoot}/classes"),
        Prefixes = LoadDefinitions<PrefixDefinition>($"{dataRoot}/prefixes"),
        Suffixes = LoadDefinitions<SuffixDefinition>($"{dataRoot}/suffixes"),
    };

    /// <summary>
    /// Loads every <c>.json</c> definition of type <typeparamref name="T"/> from a directory.
    /// Each file may be a single object or an array of objects (e.g. materials grouped by
    /// category), auto-detected per file.
    /// </summary>
    public static DataStore<T> LoadDefinitions<T>(string directory) where T : IDefinition
    {
        var store = new DataStore<T>();
        store.LoadDocuments(ReadJsonFiles(directory));
        return store;
    }

    /// <summary>Returns the text of every <c>.json</c> file under <paramref name="directory"/>,
    /// recursing into subfolders so a content type can be sharded (e.g. materials/ores.json).</summary>
    public static IReadOnlyList<string> ReadJsonFiles(string directory)
    {
        var results = new List<string>();

        using var dir = DirAccess.Open(directory);
        if (dir is null)
        {
            GD.PushWarning($"[ContentLoader] Directory not found: {directory}");
            return results;
        }

        dir.ListDirBegin();
        for (var name = dir.GetNext(); name != string.Empty; name = dir.GetNext())
        {
            var fullPath = $"{directory.TrimEnd('/')}/{name}";

            if (dir.CurrentIsDir())
            {
                results.AddRange(ReadJsonFiles(fullPath)); // recurse into subfolders
                continue;
            }

            if (!name.EndsWith(".json"))
                continue;

            using var file = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read);
            if (file is null)
            {
                GD.PushWarning($"[ContentLoader] Could not open {fullPath}");
                continue;
            }

            results.Add(file.GetAsText());
        }

        dir.ListDirEnd();
        return results;
    }
}
