using Dungeons.Combat;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Realms;

namespace Dungeons.Content;

/// <summary>
/// Validates cross-references and well-formedness across the loaded content stores,
/// at load time rather than only in tests. Every "this id points at that store"
/// relationship in the shipped JSON is checked once, up front, so a mistyped or
/// missing reference fails loudly at startup instead of throwing a
/// <see cref="KeyNotFoundException"/> mid-play (see DECISIONS.md D5, ROADMAP Phase 4).
///
/// This covers pure content→content references. Character-component references
/// (rule ids, class ability ids) are resolved and validated by the
/// <see cref="Characters.Composition.CharacterComposer"/> path instead, because they
/// depend on code-supplied handlers and include intentionally-unimplemented ids.
/// </summary>
public static class ContentValidator
{
    /// <summary>
    /// Returns every problem found in the given stores (empty when the content is
    /// well-formed). Never throws for content problems — the caller decides whether to
    /// log, warn, or throw <see cref="ContentValidationException"/>.
    /// </summary>
    public static IReadOnlyList<ContentProblem> Validate(
        DataStore<MaterialDefinition> materials,
        DataStore<ProfessionDefinition> professions,
        DataStore<ProfessionActionDefinition> actions,
        DataStore<CraftingInteractionDefinition> interactions,
        DataStore<AbilityDefinition> abilities,
        DataStore<ActorDefinition> actors,
        DataStore<RealmDefinition> realms,
        DataStore<ConsumableDefinition> consumables,
        DataStore<EquipmentDefinition> equipment)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(professions);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(interactions);
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(realms);
        ArgumentNullException.ThrowIfNull(consumables);
        ArgumentNullException.ThrowIfNull(equipment);

        var problems = new List<ContentProblem>();

        ValidateMaterials(materials, problems);
        ValidateMaterialTags(materials, problems);
        ValidateActors(actors, abilities, materials, problems);
        ValidateProfessionActions(actions, professions, materials, problems);
        ValidateInteractions(interactions, materials, consumables, professions, problems);
        ValidateRealms(realms, actors, actions, materials, problems);
        ValidateEquipment(equipment, problems);

        return problems;
    }

    /// <summary>Valid property range for material profiles (docs/itemization.md §2).</summary>
    public const double MinPropertyValue = 0.0;
    public const double MaxPropertyValue = 100.0;

    private static readonly IReadOnlySet<string> KnownProperties =
        new HashSet<string>(ItemProperties.All, StringComparer.OrdinalIgnoreCase);

    private static void ValidateMaterials(
        DataStore<MaterialDefinition> materials,
        List<ContentProblem> problems)
    {
        foreach (var material in materials.GetAll())
        {
            foreach (var (property, value) in material.Properties)
            {
                if (!KnownProperties.Contains(property))
                    problems.Add(new("materials", $"{material.Id} has unknown property '{property}' (typo, or add it to ItemProperties)."));

                if (value < MinPropertyValue || value > MaxPropertyValue)
                    problems.Add(new("materials", $"{material.Id} property '{property}' = {value:0.##} is outside the {MinPropertyValue:0}–{MaxPropertyValue:0} range."));
            }
        }
    }

    /// <summary>
    /// Validates the <c>family:value</c> tag namespace on materials (docs/emergent-item-system §4.1):
    /// every tag is namespaced under a known family, closed families use allowed values, and
    /// per-family cardinality holds (comp/state/rarity exactly one, origin one-or-two, form ≥1).
    /// </summary>
    private static void ValidateMaterialTags(
        DataStore<MaterialDefinition> materials,
        List<ContentProblem> problems)
    {
        foreach (var material in materials.GetAll())
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var tag in material.Tags)
            {
                if (!TagFamilies.TryParse(tag, out var family, out var value))
                {
                    problems.Add(new("tags", $"{material.Id} has un-namespaced tag '{tag}' (expected family:value)."));
                    continue;
                }

                if (!TagFamilies.TryGet(family, out var def))
                {
                    problems.Add(new("tags", $"{material.Id} tag '{tag}' uses unknown family '{family}'."));
                    continue;
                }

                if (def.ClosedValues is not null && !def.ClosedValues.Contains(value))
                    problems.Add(new("tags", $"{material.Id} tag '{tag}' is not a valid '{family}' value."));

                counts[family] = counts.GetValueOrDefault(family) + 1;
            }

            foreach (var family in TagFamilies.All)
            {
                var count = counts.GetValueOrDefault(family.Name);
                if (count < family.Min)
                    problems.Add(new("tags", $"{material.Id} needs at least {family.Min} '{family.Name}:' tag (has {count})."));
                else if (count > family.Max)
                    problems.Add(new("tags", $"{material.Id} has {count} '{family.Name}:' tags (max {family.Max})."));
            }
        }
    }

    private static void ValidateActors(
        DataStore<ActorDefinition> actors,
        DataStore<AbilityDefinition> abilities,
        DataStore<MaterialDefinition> materials,
        List<ContentProblem> problems)
    {
        foreach (var actor in actors.GetAll())
        {
            foreach (var abilityId in actor.AbilityIds)
                if (!abilities.Contains(abilityId))
                    problems.Add(new("actors", $"{actor.Id} references unknown ability '{abilityId}'."));

            if (!string.IsNullOrEmpty(actor.LootItemId) && !materials.Contains(actor.LootItemId))
                problems.Add(new("actors", $"{actor.Id} drops unknown material '{actor.LootItemId}'."));
        }
    }

    private static void ValidateProfessionActions(
        DataStore<ProfessionActionDefinition> actions,
        DataStore<ProfessionDefinition> professions,
        DataStore<MaterialDefinition> materials,
        List<ContentProblem> problems)
    {
        foreach (var action in actions.GetAll())
        {
            if (!professions.Contains(action.ProfessionId))
                problems.Add(new("profession_actions", $"{action.Id} references unknown profession '{action.ProfessionId}'."));

            foreach (var io in action.Inputs.Concat(action.Outputs))
                if (!materials.Contains(io.ItemId))
                    problems.Add(new("profession_actions", $"{action.Id} references unknown material '{io.ItemId}'."));

            foreach (var bonus in action.BonusOutputs)
                if (!materials.Contains(bonus.ItemId))
                    problems.Add(new("profession_actions", $"{action.Id} bonus output references unknown material '{bonus.ItemId}'."));
        }
    }

    private static void ValidateInteractions(
        DataStore<CraftingInteractionDefinition> interactions,
        DataStore<MaterialDefinition> materials,
        DataStore<ConsumableDefinition> consumables,
        DataStore<ProfessionDefinition> professions,
        List<ContentProblem> problems)
    {
        foreach (var interaction in interactions.GetAll())
        {
            foreach (var input in interaction.Inputs)
                if (!materials.Contains(input.ItemId))
                    problems.Add(new("crafting", $"{interaction.Id} input references unknown material '{input.ItemId}'."));

            // A result may be a stackable material or a consumable item.
            if (!materials.Contains(interaction.ResultItemId) && !consumables.Contains(interaction.ResultItemId))
                problems.Add(new("crafting", $"{interaction.Id} result '{interaction.ResultItemId}' is neither a known material nor consumable."));

            foreach (var req in interaction.ProfessionRequirements)
                if (!professions.Contains(req.ProfessionId))
                    problems.Add(new("crafting", $"{interaction.Id} requires unknown profession '{req.ProfessionId}'."));
        }
    }

    private static void ValidateRealms(
        DataStore<RealmDefinition> realms,
        DataStore<ActorDefinition> actors,
        DataStore<ProfessionActionDefinition> actions,
        DataStore<MaterialDefinition> materials,
        List<ContentProblem> problems)
    {
        foreach (var realm in realms.GetAll())
        {
            foreach (var loc in realm.Locations)
            {
                foreach (var connection in loc.Connections)
                {
                    if (!realm.HasLocation(connection))
                    {
                        problems.Add(new("realms", $"{realm.Id}/{loc.Id} connects to unknown location '{connection}'."));
                        continue;
                    }

                    // Edges must be symmetric so the party can move back and forth.
                    if (!realm.GetLocation(connection).Connections.Contains(loc.Id))
                        problems.Add(new("realms", $"{realm.Id}: edge {loc.Id} → {connection} is not symmetric."));
                }

                switch (loc.Type)
                {
                    case RealmLocationType.Combat:
                        if (string.IsNullOrEmpty(loc.ActorId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} is a Combat node with no actor."));
                        else if (!actors.Contains(loc.ActorId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} references unknown actor '{loc.ActorId}'."));
                        break;

                    case RealmLocationType.Gather:
                        if (string.IsNullOrEmpty(loc.ProfessionActionId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} is a Gather node with no profession action."));
                        else if (!actions.Contains(loc.ProfessionActionId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} references unknown action '{loc.ProfessionActionId}'."));
                        break;

                    case RealmLocationType.Event:
                        if (!string.IsNullOrEmpty(loc.RewardItemId) && !materials.Contains(loc.RewardItemId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} rewards unknown material '{loc.RewardItemId}'."));
                        break;
                }
            }
        }
    }

    private static void ValidateEquipment(
        DataStore<EquipmentDefinition> equipment,
        List<ContentProblem> problems)
    {
        foreach (var def in equipment.GetAll())
        {
            switch (def.Slot)
            {
                case EquipmentSlot.Weapon when def.Weapon is null:
                    problems.Add(new("equipment", $"{def.Id} is a Weapon but has no weapon stats block."));
                    break;
                case EquipmentSlot.Armor when def.Armor is null:
                    problems.Add(new("equipment", $"{def.Id} is Armor but has no armor stats block."));
                    break;
            }
        }
    }
}
