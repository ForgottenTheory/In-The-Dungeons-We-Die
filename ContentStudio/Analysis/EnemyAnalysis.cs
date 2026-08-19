using Dungeons.Combat;
using Dungeons.Content;

namespace ContentStudio.Analysis;

/// <summary>
/// Resolved-enemy analytics built on the game's own <see cref="ActorResolver"/>, so the
/// numbers here are exactly what a fight would use — never a re-implementation.
/// </summary>
public static class EnemyAnalysis
{
    /// <summary>Reference packet size used for effective-durability comparisons. EHP depends on
    /// incoming hit size because armour is a ratio of it; 10 is a mid-game single packet.</summary>
    public const double ReferencePacketAmount = 10.0;

    public sealed record EnemyRow(
        string Id, string Name, string? FamilyId, string? RoleId, string? AiProfileId, string Rank,
        IReadOnlyDictionary<string, int> Attributes,
        int Health, int Mana, int Stamina,
        double AuthoredArmor, double EffectiveArmour, double Resolve,
        IReadOnlyDictionary<string, double> Resistances,
        IReadOnlyDictionary<string, double> Vulnerable,
        IReadOnlyList<string> MoveIds, IReadOnlyList<string> LootTableIds,
        IReadOnlyDictionary<string, double> EffectiveHp);

    public static List<EnemyRow> BuildTable(ContentBundle bundle)
    {
        var rows = new List<EnemyRow>();
        foreach (var actor in bundle.Actors.GetAll().OrderBy(actor => actor.Id, StringComparer.Ordinal))
        {
            ResolvedActor resolved;
            try
            {
                resolved = ActorResolver.Resolve(actor, bundle.EnemyFamilies, bundle.EnemyRoles, bundle.AiProfiles);
            }
            catch (KeyNotFoundException)
            {
                continue; // broken family/role reference — validation reports it; the table skips it
            }

            var effectiveArmour = resolved.Attributes.Constitution * CombatTuning.ArmorPerConstitution + resolved.Armor;
            rows.Add(new EnemyRow(
                actor.Id,
                resolved.Name,
                actor.Family,
                actor.Role,
                actor.AiProfile ?? (actor.Role is not null && bundle.EnemyRoles.TryGetById(actor.Role, out var role) ? role.AiProfile : null),
                EnemyRanks.Of(resolved.Tags).ToString(),
                new Dictionary<string, int>
                {
                    ["strength"] = resolved.Attributes.Strength,
                    ["dexterity"] = resolved.Attributes.Dexterity,
                    ["intelligence"] = resolved.Attributes.Intelligence,
                    ["constitution"] = resolved.Attributes.Constitution,
                    ["wisdom"] = resolved.Attributes.Wisdom,
                    ["endurance"] = resolved.Attributes.Endurance,
                    ["luck"] = resolved.Attributes.Luck,
                },
                resolved.Resources.Health, resolved.Resources.Mana, resolved.Resources.Stamina,
                resolved.Armor, effectiveArmour, resolved.Resolve,
                resolved.Resistances.ToDictionary(pair => pair.Key.ToLowerInvariant(), pair => pair.Value),
                resolved.Vulnerable.ToDictionary(pair => pair.Key, pair => pair.Value),
                resolved.Moves.Select(grant => grant.Id).ToList(),
                resolved.LootTableIds,
                EffectiveHpByDamageType(resolved, effectiveArmour)));
        }
        return rows;
    }

    /// <summary>
    /// Health divided by the damage multiplier one reference packet of each type suffers,
    /// mirroring the hit pipeline's mitigation order: armour (physical delivery only), then
    /// capped lane resistance, then clamped type vulnerability.
    /// </summary>
    private static Dictionary<string, double> EffectiveHpByDamageType(ResolvedActor resolved, double effectiveArmour)
    {
        var result = new Dictionary<string, double>();
        foreach (var damageType in Enum.GetValues<DamageType>())
        {
            var multiplier = DamageMultiplierFor(resolved, effectiveArmour, damageType);
            result[damageType.ToString()] = multiplier <= 0 ? double.PositiveInfinity : resolved.Resources.Health / multiplier;
        }
        return result;
    }

    private static double DamageMultiplierFor(ResolvedActor resolved, double effectiveArmour, DamageType damageType)
    {
        var multiplier = 1.0;

        if (DamageTypes.IsPhysical(damageType))
        {
            var armourReduction = effectiveArmour / (effectiveArmour + CombatTuning.ArmourK * ReferencePacketAmount);
            multiplier *= 1.0 - armourReduction;
        }

        var lane = DamageTypes.IsPhysical(damageType) ? "physical" : "magic";
        var laneResistance = resolved.Resistances.TryGetValue(lane, out var resistance) ? resistance : 0.0;
        var cappedResistance = Math.Clamp(laneResistance, CombatTuning.ResistanceFloor, CombatTuning.MaxResistance);
        multiplier *= 1.0 - cappedResistance;

        var vulnerability = resolved.Vulnerable.TryGetValue(damageType.ToString(), out var authored)
            ? Math.Clamp(authored, CombatTuning.MinVulnerability, CombatTuning.MaxVulnerability)
            : 1.0;
        multiplier *= vulnerability;

        return multiplier;
    }

    /// <summary>The AUTHORED → RESOLVED provenance view: which layer contributed what.</summary>
    public static object? ExplainActor(ContentBundle bundle, string actorId)
    {
        if (!bundle.Actors.TryGetById(actorId, out var actor))
            return null;
        var family = actor.Family is not null && bundle.EnemyFamilies.TryGetById(actor.Family, out var familyDefinition) ? familyDefinition : null;
        var role = actor.Role is not null && bundle.EnemyRoles.TryGetById(actor.Role, out var roleDefinition) ? roleDefinition : null;

        ResolvedActor resolved;
        try
        {
            resolved = ActorResolver.Resolve(actor, bundle.EnemyFamilies, bundle.EnemyRoles, bundle.AiProfiles);
        }
        catch (KeyNotFoundException exception)
        {
            return new { error = exception.Message };
        }

        var attributeNames = new[] { "strength", "dexterity", "intelligence", "constitution", "wisdom", "endurance", "luck" };
        var attributeParts = attributeNames.ToDictionary(name => name, name => new
        {
            family = AttributeOf(family?.Attributes ?? (actor.Family is null ? actor.Attributes : default), name),
            role = AttributeOf(role?.AttributeTweaks ?? default, name),
            actor = AttributeOf(actor.AttributeTweaks, name),
            final = AttributeOf(resolved.Attributes, name),
        });

        var resistanceKeys = Keys(family?.Resistances, role?.Resistances, actor.Resistances);
        var vulnerableKeys = Keys(family?.Vulnerable, role?.Vulnerable, actor.Vulnerable);

        return new
        {
            id = actor.Id,
            name = resolved.Name,
            familyId = actor.Family,
            roleId = actor.Role,
            aiProfileId = actor.AiProfile ?? role?.AiProfile,
            aiInlineRuleCount = actor.Ai.Count,
            attributes = attributeParts,
            resources = new
            {
                family = new { health = family?.Resources.Health ?? (actor.Family is null ? actor.Resources.Health : 0), mana = family?.Resources.Mana ?? 0, stamina = family?.Resources.Stamina ?? 0 },
                role = new { health = role?.ResourceTweaks.Health ?? 0, mana = role?.ResourceTweaks.Mana ?? 0, stamina = role?.ResourceTweaks.Stamina ?? 0 },
                actor = new { health = actor.ResourceTweaks.Health, mana = actor.ResourceTweaks.Mana, stamina = actor.ResourceTweaks.Stamina },
                final = new { health = resolved.Resources.Health, mana = resolved.Resources.Mana, stamina = resolved.Resources.Stamina },
            },
            // Armour and Resolve are whole-value overrides: actor beats role beats family.
            armor = new { family = family?.Armor, role = role?.Armor, actor = actor.Armor, final = resolved.Armor, winner = Winner(actor.Armor, role?.Armor, family?.Armor) },
            resolve = new { family = family?.Resolve, role = role?.Resolve, actor = actor.Resolve, final = resolved.Resolve, winner = Winner(actor.Resolve, role?.Resolve, family?.Resolve) },
            resistances = resistanceKeys.ToDictionary(key => key, key => LayeredValue(key, family?.Resistances, role?.Resistances, actor.Resistances)),
            vulnerable = vulnerableKeys.ToDictionary(key => key, key => LayeredValue(key, family?.Vulnerable, role?.Vulnerable, actor.Vulnerable)),
            moves = resolved.Moves.Select(grant => grant.Id).ToList(),
            lootTables = new
            {
                family = family?.LootTableId,
                role = role?.LootTableId,
                actor = actor.LootTableId,
                final = resolved.LootTableIds,
            },
            tags = resolved.Tags,
        };
    }

    private static int AttributeOf(Dungeons.Characters.AttributeSet attributes, string name) => name switch
    {
        "strength" => attributes.Strength,
        "dexterity" => attributes.Dexterity,
        "intelligence" => attributes.Intelligence,
        "constitution" => attributes.Constitution,
        "wisdom" => attributes.Wisdom,
        "endurance" => attributes.Endurance,
        _ => name == "luck" ? attributes.Luck : attributes.Endurance,
    };

    private static string Winner(object? actorValue, object? roleValue, object? familyValue) =>
        actorValue is not null ? "actor" : roleValue is not null ? "role" : familyValue is not null ? "family" : "default";

    private static IReadOnlyList<string> Keys(params IReadOnlyDictionary<string, double>?[] layers) =>
        layers.Where(layer => layer is not null)
            .SelectMany(layer => layer!.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static object LayeredValue(string key, IReadOnlyDictionary<string, double>? family,
        IReadOnlyDictionary<string, double>? role, IReadOnlyDictionary<string, double>? actor)
    {
        double? Of(IReadOnlyDictionary<string, double>? layer) =>
            layer is not null && layer.TryGetValue(key, out var value) ? value : null;
        var familyValue = Of(family);
        var roleValue = Of(role);
        var actorValue = Of(actor);
        return new
        {
            family = familyValue,
            role = roleValue,
            actor = actorValue,
            final = actorValue ?? roleValue ?? familyValue,
            winner = Winner(actorValue, roleValue, familyValue),
        };
    }
}
