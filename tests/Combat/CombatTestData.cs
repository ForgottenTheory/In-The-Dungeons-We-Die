using Dungeons.Actions;
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

    /// <summary>A single-packet attack move — the E4 shape of what tests used to author as an
    /// ability. Same numbers in, same numbers out.</summary>
    public static MoveDefinition Move(
        string id, DamageType type, double baseValue, int telegraph, int windup, int recovery,
        int stamina = 0, double stagger = 0) => new()
        {
            Id = id,
            Name = id,
            Kind = MoveKind.Attack,
            Tags = new[] { "action:attack", "delivery:melee" },
            Timing = new ActionTiming { TelegraphTicks = telegraph, WindupTicks = windup, RecoveryTicks = recovery },
            Costs = stamina > 0
                ? new[] { new ActionCost { Resource = "stamina", Amount = stamina } }
                : Array.Empty<ActionCost>(),
            Packets = new[] { new Packet(type, baseValue) },
            StaggerPower = stagger,
        };

    /// <summary>Resolves a definition with no modifiers — what most tests want in a moveset.</summary>
    public static ResolvedMove Resolved(MoveDefinition move) =>
        MovesetBuilder.Apply(move, Array.Empty<MoveOpSpec>(), new[] { "test" });

    public static IReadOnlyList<ResolvedMove> Set(params MoveDefinition[] moves) =>
        moves.Select(Resolved).ToList();

    public static Combatant Player(
        int hp = 100, AttributeSet? attrs = null, int stamina = 100,
        IReadOnlyList<ResolvedMove>? moveset = null, ArmorProfile? armor = null) => new(
        "Hero", CombatTeam.Player,
        new ResourcePool(ResourceType.Health, hp),
        new ResourcePool(ResourceType.Stamina, stamina),
        new ResourcePool(ResourceType.Mana, 0),
        moveset ?? Set(Move("move.strike", DamageType.Slashing, 8, 2, 8, 15, stamina: 5)),
        () => attrs ?? Attrs(),
        lootTableIds: null,
        armorProfile: armor);

    public static Combatant Enemy(
        string name, int hp, AttributeSet attrs, MoveDefinition move,
        IReadOnlyList<string>? lootTables = null,
        ArmorProfile? armor = null,
        IReadOnlyDictionary<string, double>? vulnerable = null,
        IReadOnlyList<AiRuleSpec>? ai = null) => new(
        name, CombatTeam.Enemy,
        new ResourcePool(ResourceType.Health, hp),
        new ResourcePool(ResourceType.Stamina, 100),
        new ResourcePool(ResourceType.Mana, 0),
        Set(move),
        () => attrs,
        lootTables,
        armorProfile: armor,
        vulnerability: vulnerable,
        ai: ai);

    public static DataStore<MoveDefinition> Moves(params MoveDefinition[] moves)
    {
        var store = new DataStore<MoveDefinition>();
        foreach (var move in moves)
            store.Add(move);
        return store;
    }
}
