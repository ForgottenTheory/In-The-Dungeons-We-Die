using Dungeons.Characters;

namespace Dungeons.Combat;

public enum CombatTeam
{
    Player,
    Enemy,
}

/// <summary>
/// A runtime combat participant. The player combatant shares the underlying
/// <see cref="Character"/>'s real resource pools so Realm attrition persists, and
/// reads its <em>effective</em> attributes so health-conditional rules (e.g. the
/// wounded-bonus suffixes) influence the fight. Enemy combatants use static actor stats.
/// </summary>
public sealed class Combatant
{
    private readonly Func<AttributeSet> _attributes;

    public Combatant(
        string name,
        CombatTeam team,
        ResourcePool health,
        ResourcePool stamina,
        ResourcePool mana,
        IReadOnlyList<string> abilityIds,
        Func<AttributeSet> attributes,
        string? lootItemId = null,
        AttackProfile? attack = null,
        ArmorProfile? armorProfile = null)
    {
        Name = name;
        Team = team;
        Health = health;
        Stamina = stamina;
        Mana = mana;
        AbilityIds = abilityIds;
        _attributes = attributes;
        LootItemId = lootItemId;
        Attack = attack;
        ArmorProfile = armorProfile ?? ArmorProfile.None;
    }

    public string Name { get; }
    public CombatTeam Team { get; }
    public ResourcePool Health { get; }
    public ResourcePool Stamina { get; }
    public ResourcePool Mana { get; }
    public IReadOnlyList<string> AbilityIds { get; }
    public string? LootItemId { get; }

    /// <summary>The equipped-weapon attack this combatant uses for its basic strike (player). Null falls back to the encounter default.</summary>
    public AttackProfile? Attack { get; }

    /// <summary>Equipped-armor mitigation applied when this combatant is hit.</summary>
    public ArmorProfile ArmorProfile { get; }

    public AttributeSet Attributes => _attributes();
    public bool IsAlive => !Health.IsDepleted;

    /// <summary>Tick at which this combatant finishes recovery and can act again.</summary>
    public long ReadyTick { get; set; }

    /// <summary>Ticks up to which a block / dodge stance is active (-1 = inactive).</summary>
    public long BlockUntilTick { get; set; } = -1;
    public long DodgeUntilTick { get; set; } = -1;

    public bool IsBlocking(long tick) => tick <= BlockUntilTick;
    public bool IsDodging(long tick) => tick <= DodgeUntilTick;
    public bool IsReady(long tick) => tick >= ReadyTick;

    public static Combatant FromCharacter(Character character, AttackProfile? attack = null, ArmorProfile? armorProfile = null) => new(
        character.DisplayName,
        CombatTeam.Player,
        character.Health,
        character.Stamina,
        character.Mana,
        character.Blueprint.AbilityIds,
        () => character.EffectiveAttributes,
        lootItemId: null,
        attack: attack,
        armorProfile: armorProfile);

    public static Combatant FromActor(ActorDefinition actor)
    {
        var attributes = actor.Attributes;
        return new Combatant(
            actor.Name,
            CombatTeam.Enemy,
            new ResourcePool(ResourceType.Health, actor.Resources.Health),
            new ResourcePool(ResourceType.Stamina, actor.Resources.Stamina),
            new ResourcePool(ResourceType.Mana, actor.Resources.Mana),
            actor.AbilityIds,
            () => attributes,
            actor.LootItemId);
    }
}
