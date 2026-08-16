using Dungeons.Characters;
using Dungeons.Content;

namespace Dungeons.Combat;

/// <summary>An actor after the family → role → actor fold — what combat actually fights.</summary>
public sealed class ResolvedActor
{
    public required string Name { get; init; }
    public required AttributeSet Attributes { get; init; }
    public required ActorResources Resources { get; init; }
    public required IReadOnlyList<MoveGrantSpec> Moves { get; init; }
    public required IReadOnlyList<AiRuleSpec> Ai { get; init; }
    public required double AvoidRepeatWeight { get; init; }
    public required IReadOnlyDictionary<string, double> Resistances { get; init; }
    public required IReadOnlyDictionary<string, double> Vulnerable { get; init; }
    public required double Armor { get; init; }
    public required double Resolve { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public string? LootItemId { get; init; }
}

/// <summary>
/// The enemy framework's one merge fold (M2′c): family baselines, role deltas and defaults,
/// actor overrides — in that order, with one rule set. Attributes and resources are baseline +
/// deltas; resistances, vulnerabilities, armour and Resolve are per-key with the later layer
/// winning; tags union; moves stay the actor's own (identity); AI is the referenced profile's
/// rules plus the actor's inline extras.
///
/// <para>A future variant layer (Elite, Realm, depth) is one more delta through this same fold
/// — never a duplicated definition.</para>
/// </summary>
public static class ActorResolver
{
    public static ResolvedActor Resolve(
        ActorDefinition actor,
        DataStore<EnemyFamilyDefinition> families,
        DataStore<CombatRoleDefinition> roles,
        DataStore<AiProfileDefinition> aiProfiles)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var family = actor.Family is { } familyId ? families.GetById(familyId) : null;
        var role = actor.Role is { } roleId ? roles.GetById(roleId) : null;

        // Baseline + deltas. A layered actor's absolute fields are forbidden by validation, so
        // they contribute nothing here; a standalone actor IS the baseline, exactly as before.
        var attributes = Add(family?.Attributes ?? actor.Attributes,
            Add(role?.AttributeTweaks ?? default, actor.AttributeTweaks));

        var baseResources = family?.Resources ?? actor.Resources;
        var resources = new ActorResources
        {
            Health = baseResources.Health + (role?.ResourceTweaks.Health ?? 0) + actor.ResourceTweaks.Health,
            Mana = baseResources.Mana + (role?.ResourceTweaks.Mana ?? 0) + actor.ResourceTweaks.Mana,
            Stamina = baseResources.Stamina + (role?.ResourceTweaks.Stamina ?? 0) + actor.ResourceTweaks.Stamina,
        };

        var profileId = actor.AiProfile ?? role?.AiProfile;
        var profile = profileId is null ? null : aiProfiles.GetById(profileId);

        return new ResolvedActor
        {
            Name = actor.Name,
            Attributes = attributes,
            Resources = resources,
            Moves = actor.Moves,
            Ai = (profile?.Rules ?? Array.Empty<AiRuleSpec>()).Concat(actor.Ai).ToList(),
            AvoidRepeatWeight = profile?.AvoidRepeatWeight ?? 1.0,
            Resistances = Overlay(family?.Resistances, role?.Resistances, actor.Resistances),
            Vulnerable = Overlay(family?.Vulnerable, role?.Vulnerable, actor.Vulnerable),
            Armor = actor.Armor ?? role?.Armor ?? family?.Armor ?? 0,
            Resolve = actor.Resolve ?? role?.Resolve ?? family?.Resolve ?? 0,
            Tags = (family?.Tags ?? Array.Empty<string>())
                .Concat(role?.Tags ?? Array.Empty<string>())
                .Concat(actor.Tags)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            LootItemId = actor.LootItemId,
        };
    }

    private static AttributeSet Add(AttributeSet a, AttributeSet b) => new()
    {
        Strength = a.Strength + b.Strength,
        Dexterity = a.Dexterity + b.Dexterity,
        Intelligence = a.Intelligence + b.Intelligence,
        Constitution = a.Constitution + b.Constitution,
        Wisdom = a.Wisdom + b.Wisdom,
        Endurance = a.Endurance + b.Endurance,
        Luck = a.Luck + b.Luck,
    };

    /// <summary>Per-key overlay, later layers winning — a role's lane beats the family's, an
    /// actor's beats both. Keys the earlier layers never mention survive untouched.</summary>
    private static IReadOnlyDictionary<string, double> Overlay(params IReadOnlyDictionary<string, double>?[] layers)
    {
        var merged = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in layers)
        {
            if (layer is null)
                continue;
            foreach (var (key, value) in layer)
                merged[key] = value;
        }

        return merged;
    }
}
