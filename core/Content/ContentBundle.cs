using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Crafting;
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
    public DataStore<ProcessDefinition> Processes { get; init; } = new();
    public DataStore<ByproductDefinition> Byproducts { get; init; } = new();
    public DataStore<NameWordDefinition> NameGrammar { get; init; } = new();
    public DataStore<Modifiers.ModifierKeyDefinition> ModifierKeys { get; init; } = new();
    public DataStore<Dungeons.Characters.Composition.NameFormatDefinition> NameFormats { get; init; } = new();
    public DataStore<ProfessionDefinition> Professions { get; init; } = new();
    public DataStore<ProfessionActionDefinition> Actions { get; init; } = new();
    public DataStore<CraftingInteractionDefinition> Interactions { get; init; } = new();
    public DataStore<AbilityDefinition> Abilities { get; init; } = new();
    public DataStore<ActorDefinition> Actors { get; init; } = new();
    public DataStore<RealmDefinition> Realms { get; init; } = new();
    public DataStore<ConsumableDefinition> Consumables { get; init; } = new();
    public DataStore<EquipmentDefinition> Equipment { get; init; } = new();
    public DataStore<SpeciesDefinition> Species { get; init; } = new();
    public DataStore<BaseClassDefinition> Classes { get; init; } = new();
    public DataStore<PrefixDefinition> Prefixes { get; init; } = new();
    public DataStore<SuffixDefinition> Suffixes { get; init; } = new();
}
