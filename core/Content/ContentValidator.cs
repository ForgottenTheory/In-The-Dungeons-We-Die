using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Realms;

namespace Dungeons.Content;

/// <summary>
/// Validates cross-references and well-formedness across the loaded <see cref="ContentBundle"/>,
/// at load time rather than only in tests. Every "this id points at that store"
/// relationship in the shipped JSON is checked once, up front, so a mistyped or
/// missing reference fails loudly at startup instead of throwing a
/// <see cref="KeyNotFoundException"/> mid-play (see DECISIONS.md D5, ROADMAP Phase 4).
///
/// Character-component <c>abilityIds</c> are validated against a known-unimplemented
/// allowlist (<see cref="KnownUnimplementedAbilities"/>) so real typos fail while designed
/// placeholders pass; component <c>ruleIds</c> are validated by the
/// <see cref="CharacterComposer"/> path (they resolve against code-supplied handlers).
/// </summary>
public static class ContentValidator
{
    /// <summary>Valid property range for material/equipment profiles (docs/itemization.md §2).</summary>
    public const double MinPropertyValue = 0.0;
    public const double MaxPropertyValue = 100.0;

    /// <summary>Valid range for the optional authored potency/integrity overrides
    /// (docs/emergent-item-system.md §6). Integrity 0 means destroyed, so it is not authorable.</summary>
    public const int MinPotency = 1;
    public const int MaxPotency = 100;
    public const int MinIntegrity = 1;
    public const int MaxIntegrity = 100;

    /// <summary>Valid ranges for a <see cref="ProcessDefinition"/> (docs/emergent-item-system.md §7).
    /// A channel rate above 1 would mean a single step overshoots the reagent it is converging
    /// toward, which §8.2 exists to make impossible.</summary>
    public const double MinSeverity = 0.0;
    public const double MaxSeverity = 1.0;
    public const double MaxChannelRate = 1.0;
    public const double RoleWeightTolerance = 0.001;

    /// <summary>Generic tier words the name grammar forbids (docs/emergent-item-system.md §13.1) —
    /// intensity is expressed through vocabulary, not adjectives-of-adjectives.</summary>
    public static readonly IReadOnlySet<string> ForbiddenNameWords =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Greater", "Lesser", "Superior", "Inferior", "Grand", "Minor", "Major",
            "Advanced", "Basic", "Improved", "Enhanced", "Supreme", "Ultimate",
        };

    /// <summary>Ability ids referenced by classes/species but intentionally not yet implemented
    /// (docs/emergent-item-system.md / DECISIONS.md D12). Validation tolerates these; anything
    /// else missing from the ability store is a real typo and fails.</summary>
    public static readonly IReadOnlySet<string> KnownUnimplementedAbilities =
        new HashSet<string>(StringComparer.Ordinal) { "ability.guard", "ability.hex_bolt" };

    /// <summary>
    /// Returns every problem found in the bundle (empty when the content is well-formed).
    /// Never throws for content problems — the caller decides whether to log, warn, or throw
    /// <see cref="ContentValidationException"/>.
    /// </summary>
    public static IReadOnlyList<ContentProblem> Validate(ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // The property registry is the single source of truth for valid property names.
        var knownProperties = new HashSet<string>(
            content.Properties.GetAll().Select(p => p.Id), StringComparer.OrdinalIgnoreCase);

        var problems = new List<ContentProblem>();

        ValidateMaterials(content.Materials, knownProperties, problems);
        ValidateMaterialTags(content.Materials, problems);
        ValidateProcesses(content.Processes, content.Properties, content.Professions, problems);
        ValidateByproducts(content.Byproducts, content.Materials, problems);
        ValidateNameGrammar(content.NameGrammar, content.Properties, problems);
        ValidateActors(content.Actors, content.Abilities, content.Materials, problems);
        ValidateProfessionActions(content.Actions, content.Professions, content.Materials, problems);
        ValidateInteractions(content.Interactions, content.Materials, content.Consumables, content.Professions, problems);
        ValidateRealms(content.Realms, content.Actors, content.Actions, content.Materials, content.Consumables, problems);
        ValidateEquipment(content.Equipment, knownProperties, problems);
        ValidateCharacterAbilities(content, problems);

        return problems;
    }

    private static void ValidateMaterials(
        DataStore<MaterialDefinition> materials,
        IReadOnlySet<string> knownProperties,
        List<ContentProblem> problems)
    {
        foreach (var material in materials.GetAll())
        {
            foreach (var (property, value) in material.Properties)
            {
                if (!knownProperties.Contains(property))
                    problems.Add(new("materials", $"{material.Id} has unknown property '{property}' (typo, or add it to game/data/properties/)."));

                if (value < MinPropertyValue || value > MaxPropertyValue)
                    problems.Add(new("materials", $"{material.Id} property '{property}' = {value:0.##} is outside the {MinPropertyValue:0}–{MaxPropertyValue:0} range."));
            }

            // Optional emergent-system overrides (docs/emergent-item-system.md §6); normally
            // unset, so a value out of range is a typo rather than a deliberate choice.
            if (material.Potency is { } potency && (potency < MinPotency || potency > MaxPotency))
                problems.Add(new("materials", $"{material.Id} potency override {potency} is outside the {MinPotency}–{MaxPotency} range."));

            if (material.Integrity is { } integrity && (integrity < MinIntegrity || integrity > MaxIntegrity))
                problems.Add(new("materials", $"{material.Id} integrity override {integrity} is outside the {MinIntegrity}–{MaxIntegrity} range."));
        }
    }

    /// <summary>
    /// Validates crafting processes (docs/emergent-item-system.md §7). Processes are the only
    /// authored content the reaction engine needs, so a typo here silently changes the physics
    /// of the whole game rather than breaking one recipe — hence the thorough checks.
    /// </summary>
    private static void ValidateProcesses(
        DataStore<ProcessDefinition> processes,
        DataStore<PropertyDefinition> properties,
        DataStore<ProfessionDefinition> professions,
        List<ContentProblem> problems)
    {
        foreach (var process in processes.GetAll())
        {
            if (!process.IsUngated && !professions.Contains(process.Profession))
                problems.Add(new("processes", $"{process.Id} requires unknown profession '{process.Profession}'."));

            if (process.Severity is < MinSeverity or > MaxSeverity)
                problems.Add(new("processes", $"{process.Id} severity {process.Severity:0.##} is outside the {MinSeverity:0}–{MaxSeverity:0} range."));

            if (process.EssenceRate is < 0.0 or > 1.0)
                problems.Add(new("processes", $"{process.Id} essence_rate {process.EssenceRate:0.##} is outside the 0–1 range."));

            ValidateRoleWeights(process, problems);
            ValidateChannel(process, properties, problems);
            ValidateProcessTagEffects(process, problems);

            foreach (var tag in process.Requires.SubstrateTags)
                ValidateProcessTag(process, tag, "requires.substrate_tags", allowWildcard: false, problems);

            if (process.Requires.ProfessionLevel < 0)
                problems.Add(new("processes", $"{process.Id} requires a negative profession level."));

            if (process.IsUngated && process.Requires.ProfessionLevel > 0)
                problems.Add(new("processes", $"{process.Id} is ungated but requires profession level {process.Requires.ProfessionLevel}, which can never be met."));
        }
    }

    /// <summary>
    /// Validates the destruction byproduct table (docs/emergent-item-system.md §6.2c). The
    /// table must be <b>total</b> — every destroyed material has to leave something — and
    /// unambiguous, since a form covered twice would make the outcome depend on load order.
    /// </summary>
    private static void ValidateByproducts(
        DataStore<ByproductDefinition> byproducts,
        DataStore<MaterialDefinition> materials,
        List<ContentProblem> problems)
    {
        if (byproducts.Count == 0)
            return;

        var coveredForms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var byproduct in byproducts.GetAll())
        {
            if (!materials.Contains(byproduct.Material))
                problems.Add(new("byproducts", $"{byproduct.Id} produces unknown material '{byproduct.Material}'."));

            if (byproduct.Forms.Count == 0 && !byproduct.Fallback)
                problems.Add(new("byproducts", $"{byproduct.Id} covers no forms and is not the fallback, so it can never be produced."));

            foreach (var form in byproduct.Forms)
            {
                if (coveredForms.TryGetValue(form, out var owner))
                    problems.Add(new("byproducts", $"{byproduct.Id} and {owner} both cover form '{form}'."));
                else
                    coveredForms[form] = byproduct.Id;
            }
        }

        var fallbacks = byproducts.GetAll().Count(b => b.Fallback);
        if (fallbacks != 1)
            problems.Add(new("byproducts", $"expected exactly one fallback byproduct, found {fallbacks} — destruction must always yield something."));

        // Every form the material library actually uses should map somewhere explicit; the
        // fallback exists for emergent forms, not as cover for an unfinished table.
        var authoredForms = materials.GetAll()
            .SelectMany(m => m.Tags)
            .Where(t => TagFamilies.TryParse(t, out var family, out _) && family == TagFamilies.Form.Name)
            .Select(t => t[(t.IndexOf(':') + 1)..])
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var form in authoredForms.Where(f => !coveredForms.ContainsKey(f)))
            problems.Add(new("byproducts", $"form '{form}' is used by the material library but no byproduct covers it."));
    }

    /// <summary>
    /// Validates the name grammar (docs/emergent-item-system.md §13). The grammar's hard
    /// constraints are what keep generated names from reading like loot-generator spam, so
    /// they are enforced on the vocabulary rather than trusted to whoever authors it.
    /// </summary>
    private static void ValidateNameGrammar(
        DataStore<NameWordDefinition> grammar,
        DataStore<PropertyDefinition> properties,
        List<ContentProblem> problems)
    {
        foreach (var entry in grammar.GetAll())
        {
            if (!entry.HasKnownPrefix)
            {
                problems.Add(new("name_grammar", $"{entry.Id} must start with '{NameWordDefinition.IntensityPrefix}' or '{NameWordDefinition.FormPrefix}'."));
                continue;
            }

            if (entry.Words.Count == 0)
            {
                problems.Add(new("name_grammar", $"{entry.Id} supplies no words."));
                continue;
            }

            if (entry.Kind == NameWordKind.Intensity && !properties.Contains(entry.Key))
                problems.Add(new("name_grammar", $"{entry.Id} is a ladder for unknown property '{entry.Key}'."));

            foreach (var word in entry.Words)
            {
                if (string.IsNullOrWhiteSpace(word))
                    problems.Add(new("name_grammar", $"{entry.Id} contains a blank word."));

                // §13.1: numbers never appear in a name, and intensity is expressed through
                // vocabulary rather than adjectives-of-adjectives.
                if (word.Any(char.IsDigit))
                    problems.Add(new("name_grammar", $"{entry.Id} word '{word}' contains a number."));

                if (ForbiddenNameWords.Contains(word))
                    problems.Add(new("name_grammar", $"{entry.Id} word '{word}' is a tier word; use a stronger vocabulary word instead."));
            }
        }
    }

    private static void ValidateRoleWeights(ProcessDefinition process, List<ContentProblem> problems)
    {
        var weights = process.RoleWeights;

        if (weights.Substrate < 0.0 || weights.Reagent < 0.0 || weights.Catalyst < 0.0)
            problems.Add(new("processes", $"{process.Id} has a negative role weight."));

        // Potency is a weighted mean (§6.1); weights that don't sum to 1 would let a process
        // inflate or deflate potency for free, which is the exploit the mean exists to close.
        if (Math.Abs(weights.Total - 1.0) > RoleWeightTolerance)
            problems.Add(new("processes", $"{process.Id} role_weights sum to {weights.Total:0.###}, not 1.0."));
    }

    private static void ValidateChannel(
        ProcessDefinition process,
        DataStore<PropertyDefinition> properties,
        List<ContentProblem> problems)
    {
        if (process.Channel.Count == 0)
        {
            problems.Add(new("processes", $"{process.Id} opens no channel, so it could never change anything."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in process.Channel)
        {
            if (!seen.Add(entry.Property))
                problems.Add(new("processes", $"{process.Id} lists channel property '{entry.Property}' twice."));

            if (entry.Rate is <= 0.0 or > MaxChannelRate)
                problems.Add(new("processes", $"{process.Id} channel '{entry.Property}' rate {entry.Rate:0.##} is outside the 0–{MaxChannelRate:0} range (exclusive of 0)."));

            if (!properties.TryGetById(entry.Property, out var property))
            {
                problems.Add(new("processes", $"{process.Id} opens unknown property '{entry.Property}' (typo, or add it to game/data/properties/)."));
                continue;
            }

            // §2.3: Response properties are derived outputs and Sourcing describes how hard the
            // material was to obtain — neither may ever be a reaction input.
            if (property.Role is PropertyRole.Response or PropertyRole.Sourcing)
                problems.Add(new("processes", $"{process.Id} opens '{entry.Property}', which is a {property.Role} property and can never be a reaction input."));
        }
    }

    private static void ValidateProcessTagEffects(ProcessDefinition process, List<ContentProblem> problems)
    {
        foreach (var tag in process.TagEffects.Set)
            ValidateProcessTag(process, tag, "tag_effects.set", allowWildcard: false, problems);

        foreach (var tag in process.TagEffects.Clear)
            ValidateProcessTag(process, tag, "tag_effects.clear", allowWildcard: true, problems);

        // Setting and clearing the same exact tag is always an authoring mistake; a family
        // wildcard clear alongside a set in that family is the normal "replace" idiom.
        foreach (var tag in process.TagEffects.Set.Intersect(process.TagEffects.Clear, StringComparer.Ordinal))
            problems.Add(new("processes", $"{process.Id} both sets and clears tag '{tag}'."));
    }

    private static void ValidateProcessTag(
        ProcessDefinition process,
        string tag,
        string field,
        bool allowWildcard,
        List<ContentProblem> problems)
    {
        if (!TagFamilies.TryParse(tag, out var family, out var value))
        {
            problems.Add(new("processes", $"{process.Id} {field} has un-namespaced tag '{tag}' (expected family:value)."));
            return;
        }

        if (!TagFamilies.TryGet(family, out var def))
        {
            problems.Add(new("processes", $"{process.Id} {field} tag '{tag}' uses unknown family '{family}'."));
            return;
        }

        if (value == ProcessTagEffects.ClearFamilyWildcard)
        {
            if (!allowWildcard)
                problems.Add(new("processes", $"{process.Id} {field} may not use the '{family}:*' wildcard."));
            return;
        }

        if (def.ClosedValues is not null && !def.ClosedValues.Contains(value))
            problems.Add(new("processes", $"{process.Id} {field} tag '{tag}' is not a valid '{family}' value."));
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
        DataStore<ConsumableDefinition> consumables,
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
                        // A reward may be a stackable material or a consumable item.
                        if (!string.IsNullOrEmpty(loc.RewardItemId)
                            && !materials.Contains(loc.RewardItemId) && !consumables.Contains(loc.RewardItemId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} rewards unknown item '{loc.RewardItemId}'."));
                        break;
                }
            }
        }
    }

    private static void ValidateEquipment(
        DataStore<EquipmentDefinition> equipment,
        IReadOnlySet<string> knownProperties,
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

            foreach (var property in def.Properties.Keys)
                if (!knownProperties.Contains(property))
                    problems.Add(new("equipment", $"{def.Id} has unknown property '{property}' (typo, or add it to game/data/properties/)."));
        }
    }

    /// <summary>
    /// Validates that ability ids referenced by character components (species/class/prefix/suffix)
    /// exist, tolerating the <see cref="KnownUnimplementedAbilities"/> placeholders.
    /// </summary>
    private static void ValidateCharacterAbilities(ContentBundle content, List<ContentProblem> problems)
    {
        void Check(IEnumerable<CharacterComponentDefinition> components, string kind)
        {
            foreach (var component in components)
                foreach (var abilityId in component.AbilityIds)
                    if (!content.Abilities.Contains(abilityId) && !KnownUnimplementedAbilities.Contains(abilityId))
                        problems.Add(new(kind, $"{component.Id} references unknown ability '{abilityId}'."));
        }

        Check(content.Species.GetAll(), "species");
        Check(content.Classes.GetAll(), "classes");
        Check(content.Prefixes.GetAll(), "prefixes");
        Check(content.Suffixes.GetAll(), "suffixes");
    }
}
