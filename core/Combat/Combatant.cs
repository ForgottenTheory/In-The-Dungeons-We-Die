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
        IReadOnlyList<ResolvedMove> moveset,
        Func<AttributeSet> attributes,
        string? lootItemId = null,
        ArmorProfile? armorProfile = null,
        IReadOnlyDictionary<string, double>? vulnerability = null,
        IReadOnlyList<AiRuleSpec>? ai = null)
    {
        Name = name;
        Team = team;
        Health = health;
        Stamina = stamina;
        Mana = mana;
        Moveset = moveset;
        _attributes = attributes;
        LootItemId = lootItemId;
        ArmorProfile = armorProfile ?? ArmorProfile.None;
        Vulnerability = vulnerability ?? EmptyVulnerability;
        Ai = ai ?? Array.Empty<AiRuleSpec>();
    }

    private static readonly IReadOnlyDictionary<string, double> EmptyVulnerability =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    public string Name { get; }
    public CombatTeam Team { get; }
    public ResourcePool Health { get; }
    public ResourcePool Stamina { get; }
    public ResourcePool Mana { get; }

    /// <summary>
    /// Everything this combatant can do, fully resolved (E4). Settable because the moveset is a
    /// function of things that change mid-session — equipment, build, granted moves — and the
    /// encounter holds one combatant reference across those changes.
    /// </summary>
    public IReadOnlyList<ResolvedMove> Moveset { get; set; }

    /// <summary>Weighted move selection for enemies and (later) auto-combat. Empty = uniform.</summary>
    public IReadOnlyList<AiRuleSpec> Ai { get; }

    /// <summary>Weight multiplier on whichever move this combatant used last, in [0, 1] (M2′c).
    /// 1 = indifferent to repeats; 0 = never the same move twice running.</summary>
    public double AvoidRepeatWeight { get; init; } = 1.0;

    public string? LootItemId { get; }

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

    /// <summary>Tick up to which a parry attempt is live (R4c-2; gear-granted, 3-tick window).</summary>
    public long ParryUntilTick { get; set; } = -1;

    public bool IsParrying(long tick) => tick <= ParryUntilTick;

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
    /// Resistance in one lane after capping and flooring (docs/damage-and-defense.md §4.2),
    /// from the armour profile alone. The pipeline resolves `combat.resist.<lane>`
    /// contributions on top of this base (R4a) and caps via <see cref="CapResistance"/>;
    /// exposure, inversion and penetration slot into that ordering in R4c.
    /// </summary>
    public double EffectiveResistance(string lane) =>
        CapResistance(ArmorProfile.ResistanceFor(lane));

    /// <summary>The §4.2 cap and floor, shared by every resistance read.</summary>
    public static double CapResistance(double total) =>
        Math.Max(Math.Min(total, CombatTuning.MaxResistance), CombatTuning.ResistanceFloor);

    /// <summary>
    /// Per-damage-type multiplier (D-02). Two-way and clamped: a skeleton can take 25% more
    /// crushing and 20% less piercing. This is where "the right weapon for the enemy" lives now
    /// that the three physical resistances collapsed into one lane.
    /// </summary>
    public double VulnerabilityTo(DamageType type) =>
        Vulnerability.TryGetValue(type.ToString(), out var v)
            ? Math.Clamp(v, CombatTuning.MinVulnerability, CombatTuning.MaxVulnerability)
            : 1.0;

    public static Combatant FromCharacter(
        Character character, IReadOnlyList<ResolvedMove> moveset, ArmorProfile? armorProfile = null) => new(
        character.DisplayName,
        CombatTeam.Player,
        character.Health,
        character.Stamina,
        character.Mana,
        moveset,
        () => character.EffectiveAttributes,
        lootItemId: null,
        armorProfile: armorProfile);

    /// <summary>Builds the runtime combatant from a <see cref="ResolvedActor"/> — the output of
    /// <see cref="ActorResolver"/>'s family → role → actor fold (M2′c). Armour is real here:
    /// the pre-framework path hardcoded <c>Armor = 0</c>, so enemy armour was unusable.</summary>
    public static Combatant FromActor(ResolvedActor actor, IReadOnlyList<ResolvedMove> moveset)
    {
        var attributes = actor.Attributes;
        return new Combatant(
            actor.Name,
            CombatTeam.Enemy,
            new ResourcePool(ResourceType.Health, actor.Resources.Health),
            new ResourcePool(ResourceType.Stamina, actor.Resources.Stamina),
            new ResourcePool(ResourceType.Mana, actor.Resources.Mana),
            moveset,
            () => attributes,
            actor.LootItemId,
            armorProfile: actor.Armor <= 0 && actor.Resistances.Count == 0
                ? null
                : new ArmorProfile { Armor = actor.Armor, Resistances = new Dictionary<string, double>(actor.Resistances) },
            vulnerability: actor.Vulnerable,
            ai: actor.Ai)
        {
            Resolve = actor.Resolve,
            AvoidRepeatWeight = actor.AvoidRepeatWeight,
        };
    }
}
