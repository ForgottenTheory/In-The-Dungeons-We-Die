using ContentStudio.Models;

namespace ContentStudio.Services;

/// <summary>
/// The knowledge reflection cannot recover from the Core types: which string fields are
/// references (and to which content types), which dictionary keys come from which vocabulary,
/// and per-field presentation hints. Keyed by declaring C# type name + JSON field name so a
/// shared shape (ItemStack, EffectSpec…) is described once and applies everywhere it nests.
/// </summary>
public static class SchemaOverrides
{
    private sealed record Rule(
        string? Kind = null,
        string[]? RefTypes = null,
        string? KeySource = null,
        string[]? EnumValues = null,
        string? EnumSource = null,
        double? Min = null,
        double? Max = null,
        double? Step = null,
        string? Label = null,
        string? Help = null);

    /// <summary>Vocabulary names resolved on the client from the /api/vocabulary payload.</summary>
    private static string[] Vocab(string name) => new[] { $"vocab:{name}" };

    private static readonly Dictionary<string, Rule> Rules = new(StringComparer.Ordinal)
    {
        // ── Enemies: actor / family / role / AI ────────────────────────────────────────────
        ["ActorDefinition.family"] = new(Kind: "ref", RefTypes: new[] { "enemy_families" }),
        ["ActorDefinition.role"] = new(Kind: "ref", RefTypes: new[] { "enemy_roles" }),
        ["ActorDefinition.ai_profile"] = new(Kind: "ref", RefTypes: new[] { "ai_profiles" }),
        ["ActorDefinition.loot_table"] = new(Kind: "ref", RefTypes: new[] { "loot_tables" }),
        ["ActorDefinition.resistances"] = new(KeySource: "lanes", Min: -1, Max: 0.75, Step: 0.05,
            Help: "Keyed by damage lane. Negative is a real weakness."),
        ["ActorDefinition.vulnerable"] = new(KeySource: "damageTypes", Min: 0.5, Max: 1.5, Step: 0.05,
            Help: "Keyed by damage TYPE; clamped to [0.50, 1.50] at runtime."),
        ["EnemyFamilyDefinition.loot_table"] = new(Kind: "ref", RefTypes: new[] { "loot_tables" }),
        ["EnemyFamilyDefinition.resistances"] = new(KeySource: "lanes", Min: -1, Max: 0.75, Step: 0.05),
        ["EnemyFamilyDefinition.vulnerable"] = new(KeySource: "damageTypes", Min: 0.5, Max: 1.5, Step: 0.05),
        ["CombatRoleDefinition.ai_profile"] = new(Kind: "ref", RefTypes: new[] { "ai_profiles" }),
        ["CombatRoleDefinition.loot_table"] = new(Kind: "ref", RefTypes: new[] { "loot_tables" }),
        ["CombatRoleDefinition.resistances"] = new(KeySource: "lanes", Min: -1, Max: 0.75, Step: 0.05),
        ["CombatRoleDefinition.vulnerable"] = new(KeySource: "damageTypes", Min: 0.5, Max: 1.5, Step: 0.05),
        ["AiProfileDefinition.avoid_repeat_weight"] = new(Min: 0, Max: 1, Step: 0.05),
        ["AiRuleSpec.move"] = new(Kind: "ref", RefTypes: new[] { "moves" }),
        ["AiRuleSpec.moveTag"] = new(EnumSource: "moveTagsAll", Help: "Set move OR moveTag, never both."),
        ["AutoCombatProfileDefinition.avoid_repeat_weight"] = new(Min: 0, Max: 1, Step: 0.05),
        ["AutoCombatProfileDefinition.reaction_ticks"] = new(Min: 5,
            Help: "The pilot's only handicap (D-07). Must stay ≥ 5 or the brain could parry."),

        // ── Moves ───────────────────────────────────────────────────────────────────────────
        ["MoveDefinition.tags"] = new(EnumSource: "moveTagsAll",
            Help: "Namespaced and closed: action: / delivery: / form: / mech: / essence:"),
        ["MoveDefinition.stagger_power"] = new(Min: 0),
        ["MoveDefinition.cooldown_ticks"] = new(Min: 0),
        ["MoveDefinition.max_targets"] = new(Min: 1),
        ["Packet.aspect"] = new(EnumSource: "damageAspects",
            Help: "Energy riding the delivery; empty = pure physical. 'arcane' has no lane and cannot be resisted."),
        ["ActionCost.resource"] = new(EnumValues: new[] { "stamina", "mana", "health" },
            Help: "A pool name, or an authored gauge name."),
        ["ActionTiming.telegraphTicks"] = new(Min: 0),
        ["ActionTiming.windupTicks"] = new(Min: 0),
        ["ActionTiming.recoveryTicks"] = new(Min: 0),
        ["MoveGrantSpec.id"] = new(Kind: "ref", RefTypes: new[] { "moves" }),
        ["MoveGrantSpec.replaces"] = new(Kind: "ref", RefTypes: new[] { "moves" }),

        // ── Move modifiers ──────────────────────────────────────────────────────────────────
        ["MoveMatch.move_id"] = new(Kind: "ref", RefTypes: new[] { "moves" }),
        ["MoveMatch.tags_all"] = new(EnumSource: "moveTagsAll"),
        ["MoveMatch.tags_any"] = new(EnumSource: "moveTagsAll"),
        ["MoveOpSpec.op"] = new(Kind: "enum", EnumSource: "moveOps"),
        ["MoveOpSpec.field"] = new(EnumSource: "moveOpFields", Help: "Timing field for scaleTiming; flag for setFlag."),
        ["MoveOpSpec.effect"] = new(Help: "Rider added by addEffect."),
        ["MoveOpSpec.from"] = new(EnumSource: "damageLanesAndPhysical"),
        ["MoveOpSpec.to"] = new(EnumSource: "damageAspects"),
        ["MoveOpSpec.fraction"] = new(Min: 0, Max: 1, Step: 0.05),

        // ── Statuses ────────────────────────────────────────────────────────────────────────
        ["StatusDefinition.lane"] = new(Kind: "enum", EnumSource: "damageLanes"),
        ["StatusDefinition.requires_status"] = new(Kind: "ref", RefTypes: new[] { "statuses" }),
        ["StatusDefinition.duration_ticks"] = new(Min: 0),
        ["StatusDefinition.tick_interval"] = new(Min: 0),
        ["StatusDefinition.max_stacks"] = new(Min: 1),
        ["StatusDefinition.control_buildup"] = new(Min: 0),
        ["StatusModifier.key"] = new(Kind: "ref", RefTypes: new[] { "modifier_keys" }),
        ["StatusModifier.value"] = new(Step: 0.05),

        // ── Rules / effects / conditions (shared everywhere) ────────────────────────────────
        ["ConditionSpec.kind"] = new(Kind: "enum", EnumSource: "ruleConditions"),
        ["ConditionSpec.text"] = new(Kind: "ref",
            RefTypes: new[] { "statuses", "moves", "modifier_keys" },
            Help: "Meaning depends on kind: a status id, tag, lane, resource or gauge name."),
        ["EffectSpec.kind"] = new(Kind: "enum", EnumSource: "ruleEffects"),
        ["EffectSpec.text"] = new(Kind: "ref",
            RefTypes: new[] { "statuses", "modifier_keys", "moves", "move_modifiers", "materials" },
            Help: "Meaning depends on kind: status for applyStatus, modifier key for grantModifier, move for grantMove/triggerMove…"),
        ["EffectSpec.chance"] = new(Min: 0, Max: 1, Step: 0.05),
        ["EffectSpec.duration_ticks"] = new(Min: 0),
        ["TriggerRule.event"] = new(Kind: "enum", EnumSource: "gameEvents"),
        ["TriggerRule.chance"] = new(Min: 0, Max: 1, Step: 0.05),
        ["TriggerRule.cooldown_ticks"] = new(Min: 0),

        // ── Materials & properties ──────────────────────────────────────────────────────────
        ["MaterialDefinition.properties"] = new(KeySource: "properties", Min: 0, Max: 100, Step: 1),
        ["MaterialDefinition.essence"] = new(KeySource: "essences", Min: 0, Max: 100, Step: 1),
        ["MaterialDefinition.materialStrength"] = new(Min: 1, Max: 100),
        ["MaterialDefinition.workability"] = new(Min: 1, Max: 100),
        ["PropertyDefinition.opposes"] = new(Kind: "ref", RefTypes: new[] { "properties" }),
        ["PropertyDefinition.floor"] = new(Min: 0, Max: 100),
        ["ResistContributor.property"] = new(Kind: "ref", RefTypes: new[] { "properties" }),
        ["ResistContributor.weight"] = new(Step: 0.05),
        ["TagGrant.tag"] = new(Help: "family:value material tag granted at or above min."),

        // ── Crafting actions (processes) ────────────────────────────────────────────────────
        ["CraftingActionDefinition.profession"] = new(Kind: "ref", RefTypes: new[] { "professions" },
            Help: "Empty string = ungated."),
        ["CraftingActionDefinition.severity"] = new(Min: 0, Max: 1, Step: 0.05),
        ["CraftingActionDefinition.essence_rate"] = new(Min: 0, Max: 1, Step: 0.05),
        ["AffectedQuality.property"] = new(Kind: "ref", RefTypes: new[] { "properties" }),
        ["AffectedQuality.rate"] = new(Min: 0, Max: 1, Step: 0.05),
        ["RoleWeights.substrate"] = new(Min: 0, Max: 1, Step: 0.05),
        ["RoleWeights.reagent"] = new(Min: 0, Max: 1, Step: 0.05),
        ["RoleWeights.catalyst"] = new(Min: 0, Max: 1, Step: 0.05),
        ["CraftingActionRequirements.profession_level"] = new(Min: 0),

        // ── Traits / essences / byproducts ──────────────────────────────────────────────────
        ["TraitDefinition.category"] = new(Kind: "enum", EnumSource: "traitCategories"),
        ["TraitDefinition.condition"] = new(Kind: "json", Help: "Property → {min, max} ranges."),
        ["TraitDefinition.magnitude_of"] = new(Kind: "refList", RefTypes: new[] { "properties" }),
        ["TraitDefinition.consumes"] = new(KeySource: "properties", Min: 0),
        ["TraitMerge.with"] = new(Kind: "ref", RefTypes: new[] { "traits" }),
        ["TraitMerge.into"] = new(Kind: "ref", RefTypes: new[] { "traits" }),
        ["EssenceDefinition.anchor"] = new(Kind: "ref", RefTypes: new[] { "properties" }),
        ["EssenceDefinition.opposes"] = new(EnumSource: "essenceKeys", Help: "Bare essence keys (fire, frost…)."),
        ["ByproductDefinition.material"] = new(Kind: "ref", RefTypes: new[] { "materials" }),
        ["ByproductDefinition.forms"] = new(Help: "Bare form-tag values (metal, wood…), not namespaced."),

        // ── Forms (equipment blueprints) ────────────────────────────────────────────────────
        ["BlueprintSlot.requires_tags"] = new(Help: "Any-of material tags gating this slot."),
        ["BlueprintSlot.mass_share"] = new(Min: 0, Max: 1, Step: 0.05, Help: "All slots must sum to 1.0."),
        ["BlueprintSlot.trait_expression"] = new(KeySource: "traitCategories", Min: 0, Max: 1, Step: 0.05),
        ["StatContribution.slot"] = new(KeySource: "ownSlots", Help: "A slot name, or * for all slots mass-weighted."),
        ["StatContribution.property"] = new(Kind: "ref", RefTypes: new[] { "properties" }),
        ["EquipmentBlueprintDefinition.trait_cap"] = new(Min: 1),

        // ── Affixes (item modifiers) ────────────────────────────────────────────────────────
        ["AffixDefinition.slot"] = new(Kind: "enum", EnumValues: new[] { "prefix", "suffix", "innate" }),
        ["AffixDefinition.class"] = new(Kind: "enum",
            EnumValues: new[] { "standard", "trigger", "innate", "exotic", "signature", "anomalous" }),
        ["ModifierAvailability.forms_any"] = new(Help: "Form ids or form tags (weapon, armor, shield…)."),
        ["ModifierAvailability.requires_any_essence"] = new(EnumSource: "essenceKeys"),
        ["MaterialInfluenceRequirement.property"] = new(Kind: "ref", RefTypes: new[] { "properties" }),
        ["MaterialInfluenceRequirement.min"] = new(Min: 0, Max: 100),
        ["ChanceWeightScale.property"] = new(Kind: "ref", RefTypes: new[] { "properties" }),
        ["ChanceWeightScale.essence"] = new(EnumSource: "essenceKeys"),
        ["AffixTier.requires"] = new(KeySource: "properties",
            Help: "Property (or essence.X) minimums the genome must meet for this tier."),
        ["AffixTier.range"] = new(Help: "Exactly [low, high]."),
        ["AffixGrant.type"] = new(Kind: "enum", EnumValues: new[] { "stat", "rule", "moveModifier" }),
        ["AffixGrant.key"] = new(Kind: "ref", RefTypes: new[] { "modifier_keys", "move_modifiers" }),
        ["AffixGrant.value"] = new(Help: "\"$roll\" or a literal number, as a string."),
        ["AffixGrant.scope"] = new(Help: "dimension:value, e.g. lane:heat or move_tag:heavy."),

        // ── Equipment ───────────────────────────────────────────────────────────────────────
        ["EquipmentDefinition.move_modifiers"] = new(Kind: "refList", RefTypes: new[] { "move_modifiers" }),
        ["EquipmentDefinition.properties"] = new(KeySource: "properties", Min: 0, Max: 100, Step: 1),
        ["EquipmentDefinition.essence"] = new(KeySource: "essences", Min: 0, Max: 100),
        ["ArmorStats.resistances"] = new(KeySource: "lanes", Min: -1, Max: 0.75, Step: 0.05),

        // ── Loot ────────────────────────────────────────────────────────────────────────────
        ["LootEntryDefinition.itemId"] = new(Kind: "ref", RefTypes: new[] { "materials", "consumables", "techniques" }),
        ["LootEntryDefinition.tableId"] = new(Kind: "ref", RefTypes: new[] { "loot_tables" }),
        ["LootEntryDefinition.chance"] = new(Min: 0, Max: 1, Step: 0.01),
        ["LootEntryDefinition.weight"] = new(Min: 0),
        ["LootEntryDefinition.minQuantity"] = new(Min: 0),
        ["LootEntryDefinition.maxQuantity"] = new(Min: 0),
        ["LootDrawDefinition.picks"] = new(Min: 0, Max: 32),
        ["GoldDropDefinition.chance"] = new(Min: 0, Max: 1, Step: 0.05),
        ["LootCondition.requiresTags"] = new(Help: "Context tags: active, passive, in_realm, elite, boss, realm ids, source:*…"),
        ["LootCondition.excludesTags"] = new(Help: "Context tags that must be absent."),
        ["LootCondition.minDepth"] = new(Min: 0),

        // ── Professions ─────────────────────────────────────────────────────────────────────
        ["ProfessionActionDefinition.professionId"] = new(Kind: "ref", RefTypes: new[] { "professions" }),
        ["ProfessionActionDefinition.loot_table"] = new(Kind: "ref", RefTypes: new[] { "loot_tables" }),
        ["ProfessionActionDefinition.requiredLevel"] = new(Min: 1, Max: 99),
        ["ProfessionActionDefinition.baseIntervalTicks"] = new(Min: 1, Help: "20 ticks = 1 second."),
        ["ProfessionActionDefinition.successChance"] = new(Min: 0, Max: 1, Step: 0.05,
            Help: "Below 1.0 only for Hunting and Thieving."),
        ["ProfessionOpportunityDefinition.discoveryChance"] = new(Min: 0, Max: 1, Step: 0.01),
        ["ProfessionOpportunityDefinition.riskWeight"] = new(Min: 0, Max: 1, Step: 0.05),
        ["ProfessionOpportunityDefinition.required_mastery"] = new(Min: 0, Max: 99),
        ["ProfessionOpportunityDefinition.extraIntervalTicks"] = new(Min: 1),
        ["ItemStack.itemId"] = new(Kind: "ref", RefTypes: new[] { "materials" }),
        ["ItemStack.quantity"] = new(Min: 1),
        ["ItemChance.itemId"] = new(Kind: "ref", RefTypes: new[] { "materials" }),
        ["ItemChance.chance"] = new(Min: 0, Max: 1, Step: 0.01),
        ["ItemChance.quantity"] = new(Min: 1),
        ["RealmKnowledgeGain.realmId"] = new(Kind: "ref", RefTypes: new[] { "realms" }),
        ["MasteryBenefitDefinition.profession"] = new(Kind: "ref", RefTypes: new[] { "professions" },
            Help: "Empty = applies to every profession."),
        ["MasteryBenefitDefinition.unlock_level"] = new(Min: 1, Max: 99),
        ["MasteryBenefitDefinition.per_level"] = new(Step: 0.001),
        ["ProfessionSynergyDefinition.source"] = new(Kind: "ref", RefTypes: new[] { "professions" },
            Help: "Empty = reads the player's TOTAL profession level."),
        ["ProfessionSynergyDefinition.target"] = new(Kind: "ref", RefTypes: new[] { "professions" },
            Help: "Empty = pays every profession."),
        ["ProfessionSynergyDefinition.unlock_level"] = new(Min: 1),
        ["ProfessionSynergyDefinition.per_level"] = new(Step: 0.001),
        ["TrainingObstacleDefinition.bonuses"] = new(KeySource: "courseBonuses", Min: 0, Step: 0.01),
        ["TrainingObstacleDefinition.intervalTicks"] = new(Min: 1),
        ["TrainingObstacleDefinition.requiredLevel"] = new(Min: 1, Max: 99),

        // ── Stations ────────────────────────────────────────────────────────────────────────
        ["StationDefinition.professions"] = new(Kind: "refList", RefTypes: new[] { "professions" }),
        ["StationDefinition.crafting_actions"] = new(Kind: "refList", RefTypes: new[] { "processes" }),
        ["StationDefinition.blueprints"] = new(Kind: "refList", RefTypes: new[] { "forms" }),

        // ── Realms ──────────────────────────────────────────────────────────────────────────
        ["RealmLocationDefinition.connections"] = new(Help: "Sibling location ids. Edges must be symmetric."),
        ["RealmLocationDefinition.actorId"] = new(Kind: "ref", RefTypes: new[] { "actors" }),
        ["RealmLocationDefinition.professionActionId"] = new(Kind: "ref", RefTypes: new[] { "profession_actions" }),
        ["RealmLocationDefinition.loot_table"] = new(Kind: "ref", RefTypes: new[] { "loot_tables" }),
        ["RealmLocationDefinition.depth"] = new(Min: 1),
        ["RealmLocationDefinition.restore_fraction"] = new(Min: 0, Max: 1, Step: 0.05),

        // ── Character components ────────────────────────────────────────────────────────────
        ["CharacterComponentDefinition.move_modifiers"] = new(Kind: "refList", RefTypes: new[] { "move_modifiers" }),
        ["SpeciesDefinition.move_modifiers"] = new(Kind: "refList", RefTypes: new[] { "move_modifiers" }),
        ["BaseClassDefinition.move_modifiers"] = new(Kind: "refList", RefTypes: new[] { "move_modifiers" }),
        ["PrefixDefinition.move_modifiers"] = new(Kind: "refList", RefTypes: new[] { "move_modifiers" }),
        ["SuffixDefinition.move_modifiers"] = new(Kind: "refList", RefTypes: new[] { "move_modifiers" }),
        ["BaseClassDefinition.growth"] = new(KeySource: "attributes", Min: 0, Step: 0.1,
            Help: "Total must not exceed the 4.0 per-level budget."),
        ["SuffixDefinition.format"] = new(Kind: "enum", EnumSource: "nameFormats"),
        ["GaugeBand.modifier"] = new(Kind: "ref", RefTypes: new[] { "modifier_keys" }),
        ["TechniqueDefinition.teaches"] = new(Kind: "ref", RefTypes: new[] { "moves" }),

        // ── Modifier keys ───────────────────────────────────────────────────────────────────
        ["ModifierKeyDefinition.scoped_by"] = new(Kind: "enum", EnumSource: "scopeDimensions"),

        // ── Crafting interactions ───────────────────────────────────────────────────────────
        ["CraftingInteractionDefinition.resultItemId"] = new(Kind: "ref", RefTypes: new[] { "materials", "consumables" }),
        ["ProfessionRequirement.professionId"] = new(Kind: "ref", RefTypes: new[] { "professions" }),
    };

    public static FieldSchema Apply(Type declaringType, FieldSchema generated)
    {
        if (!Rules.TryGetValue($"{declaringType.Name}.{generated.Name}", out var rule))
            return generated;

        return generated with
        {
            Kind = rule.Kind ?? generated.Kind,
            RefTypes = rule.RefTypes ?? generated.RefTypes,
            KeySource = rule.KeySource ?? generated.KeySource,
            EnumValues = rule.EnumValues ?? generated.EnumValues,
            // EnumSource names a vocabulary list the client resolves from /api/vocabulary.
            Help = rule.Help ?? generated.Help,
            Label = rule.Label ?? generated.Label,
            Min = rule.Min ?? generated.Min,
            Max = rule.Max ?? generated.Max,
            Step = rule.Step ?? generated.Step,
            EnumSourceName = rule.EnumSource ?? generated.EnumSourceName,
        };
    }
}
