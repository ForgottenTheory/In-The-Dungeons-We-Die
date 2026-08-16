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
        ArmorProfile? armorProfile = null,
        IReadOnlyDictionary<string, double>? vulnerability = null)
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
        Vulnerability = vulnerability ?? EmptyVulnerability;
    }

    private static readonly IReadOnlyDictionary<string, double> EmptyVulnerability =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>Per-damage-type multipliers, keyed by <see cref="DamageType"/> name (D-02).</summary>
    public IReadOnlyDictionary<string, double> Vulnerability { get; }

    /// <summary>
    /// Control threshold (D-08). Buildup from Stun, Freeze, Fear and Silence all measure
    /// against this one pool, so a build cannot Stun-lock <b>and</b> Freeze-lock. 0 falls back to
    /// <see cref="CombatTuning.DefaultResolve"/>.
    /// </summary>
    public double Resolve { get; init; }

    /// <summary>
    /// Per-lane chance to inflict that lane's signature ailment on hit. Empty until affixes
    /// grant it in E5; the plumbing exists so the magnitude rule is pinned now.
    /// </summary>
    public IReadOnlyDictionary<string, double> AilmentChance { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    public double AilmentChanceFor(string lane) => AilmentChance.GetValueOrDefault(lane);

    public AttributeSet Attributes => _attributes();
    public bool IsAlive => !Health.IsDepleted;

    /// <summary>Tick at which this combatant finishes recovery and can act again.</summary>
    public long ReadyTick { get; set; }

    /// <summary>Ticks up to which a block / dodge stance is active (-1 = inactive).</summary>
    public long BlockUntilTick { get; set; } = -1;
    public long DodgeUntilTick { get; set; } = -1;

    /// <summary>Tick the block stance was raised — the reference for the Perfect Block window.</summary>
    public long BlockStartTick { get; set; } = -1;

    public bool IsBlocking(long tick) => tick <= BlockUntilTick;
    public bool IsDodging(long tick) => tick <= DodgeUntilTick;
    public bool IsReady(long tick) => tick >= ReadyTick;

    /// <summary>
    /// True inside the tight window at the start of a block stance, where the block negates the
    /// hit outright rather than reducing it (D-06). Rewards timing over holding guard, which is
    /// what GDD §5.5 requires ("holding block forever must never be optimal").
    /// </summary>
    public bool IsPerfectBlocking(long tick) =>
        IsBlocking(tick) && BlockStartTick >= 0 && tick - BlockStartTick < CombatTuning.PerfectBlockWindowTicks;

    /// <summary>Flat armour: Constitution plus whatever is worn.</summary>
    public double Armour =>
        (Attributes.Constitution * CombatTuning.ArmorPerConstitution) + ArmorProfile.Armor;

    /// <summary>
    /// Resistance in one lane after capping and flooring (docs/damage-and-defense.md §4.2).
    /// Exposure, inversion and penetration slot in here in E3/E5; the ordering they need is
    /// already the reason this is a method rather than a dictionary read.
    /// </summary>
    public double EffectiveResistance(string lane)
    {
        var total = ArmorProfile.ResistanceFor(lane);
        var capped = Math.Min(total, CombatTuning.MaxResistance);
        return Math.Max(capped, CombatTuning.ResistanceFloor);
    }

    /// <summary>
    /// Per-damage-type multiplier (D-02). Two-way and clamped: a skeleton can take 25% more
    /// crushing and 20% less piercing. This is where "the right weapon for the enemy" lives now
    /// that the three physical resistances collapsed into one lane.
    /// </summary>
    public double VulnerabilityTo(DamageType type) =>
        Vulnerability.TryGetValue(type.ToString(), out var v)
            ? Math.Clamp(v, CombatTuning.MinVulnerability, CombatTuning.MaxVulnerability)
            : 1.0;

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
            actor.LootItemId,
            attack: null,
            armorProfile: actor.Resistances.Count == 0
                ? null
                : new ArmorProfile { Armor = 0, Resistances = actor.Resistances },
            vulnerability: actor.Vulnerable)
        {
            Resolve = actor.Resolve,
        };
    }
}
