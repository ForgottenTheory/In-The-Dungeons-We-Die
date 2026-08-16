using Dungeons.Characters;
using Dungeons.Combat;
using Dungeons.Content;

namespace Dungeons.Tests.Combat;

internal static class CombatTestData
{
    public static AttributeSet Attrs(int str = 5, int con = 5, int intel = 5, int luck = 0) => new()
    {
        Strength = str,
        Dexterity = 5,
        Intelligence = intel,
        Constitution = con,
        Wisdom = 5,
        Endurance = 5,
        Luck = luck,
    };

    public static Combatant Player(int hp = 100, AttributeSet? attrs = null, int stamina = 100, AttackProfile? attack = null, ArmorProfile? armor = null) => new(
        "Hero", CombatTeam.Player,
        new ResourcePool(ResourceType.Health, hp),
        new ResourcePool(ResourceType.Stamina, stamina),
        new ResourcePool(ResourceType.Mana, 0),
        new[] { "ability.strike" },
        () => attrs ?? Attrs(),
        lootItemId: null,
        attack: attack,
        armorProfile: armor);

    public static Combatant Enemy(
        string name, int hp, AttributeSet attrs, string abilityId,
        string? loot = "material.goblin_scrap",
        ArmorProfile? armor = null,
        IReadOnlyDictionary<string, double>? vulnerable = null) => new(
        name, CombatTeam.Enemy,
        new ResourcePool(ResourceType.Health, hp),
        new ResourcePool(ResourceType.Stamina, 100),
        new ResourcePool(ResourceType.Mana, 0),
        new[] { abilityId },
        () => attrs,
        loot,
        attack: null,
        armorProfile: armor,
        vulnerability: vulnerable);

    public static AbilityDefinition Ability(string id, DamageType type, double baseValue, int telegraph, int windup, int recovery, int stamina = 0) => new()
    {
        Id = id,
        Name = id,
        DamageType = type,
        BaseValue = baseValue,
        StaminaCost = stamina,
        Timing = new AbilityTiming { TelegraphTicks = telegraph, WindupTicks = windup, RecoveryTicks = recovery },
    };

    public static DataStore<AbilityDefinition> Abilities(params AbilityDefinition[] abilities)
    {
        var store = new DataStore<AbilityDefinition>();
        foreach (var ability in abilities)
            store.Add(ability);
        return store;
    }
}
