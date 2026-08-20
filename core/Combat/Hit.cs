namespace Dungeons.Combat;

/// <summary>
/// The energy riding on a damage packet (docs/damage-and-defense.md §2.1). Orthogonal to
/// <see cref="DamageType"/>: a flaming sword is Slashing damage with a <c>heat</c> aspect.
///
/// <para>Data-scale rather than a code enum, because aspects come from the reactive material
/// property vocabulary — but validated against this closed set, the same bargain
/// <c>ModifierKeyDefinition</c> and <c>PropertyDefinition</c> strike.</para>
///
/// <para><b>`growth` is deliberately absent.</b> There is no "nature damage" — growth is the
/// recovery property and gates Barrier affixes instead.</para>
/// </summary>
public static class DamageAspects
{
    public const string Heat = "heat";
    public const string Cold = "cold";
    public const string Charge = "charge";
    public const string Toxin = "toxin";
    public const string Corrosion = "corrosion";
    public const string Decay = "decay";

    /// <summary>
    /// Raw force. <b>The one aspect with no resistance lane</b> (D-03a) — unresistable, and
    /// structurally unamplifiable in exchange (§2.5.1). Renamed from <c>arcane</c> (D44):
    /// "arcane" now belongs to the magic-economy identity, and kinetic is what this aspect
    /// always was — the word freed by cutting Kinetic from the identity roster.
    /// </summary>
    public const string Kinetic = "kinetic";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Heat, Cold, Charge, Toxin, Corrosion, Decay, Kinetic,
    };
}

/// <summary>
/// The defensive axis: which resistance a packet is checked against
/// (docs/damage-and-defense.md §4.1).
///
/// <para><b>Eight lanes.</b> Slashing/Crushing/Piercing share one <c>physical</c> lane (D-02) —
/// per-type weakness lives on the enemy as a vulnerability multiplier instead, which keeps the
/// defensive stat count at 8 rather than 10 and makes the counter discoverable.</para>
/// </summary>
public static class DamageLanes
{
    public const string Physical = "physical";
    public const string Magic = "magic";
    public const string Heat = DamageAspects.Heat;
    public const string Cold = DamageAspects.Cold;
    public const string Charge = DamageAspects.Charge;
    public const string Toxin = DamageAspects.Toxin;
    public const string Corrosion = DamageAspects.Corrosion;

    /// <summary>Registered from E1 so nothing needs retrofitting; no content uses it yet (D-03b).</summary>
    public const string Decay = DamageAspects.Decay;

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Physical, Magic, Heat, Cold, Charge, Toxin, Corrosion, Decay,
    };

    /// <summary>The lane an aspectless packet of this type falls into.</summary>
    public static string Of(DamageType type) =>
        DamageTypes.IsPhysical(type) ? Physical : Magic;
}

/// <summary>
/// One divisible unit of damage: exactly one <see cref="DamageType"/> and zero-or-one aspect.
///
/// <para>Packets are why the pipeline can express conversion, added-as-extra, per-lane
/// penetration and aspect-gated ailments at all. The old <c>(DamageType, double)</c> pair could
/// not describe "30% of your slashing converted to heat" without lying about one half of the
/// hit.</para>
/// </summary>
[method: System.Text.Json.Serialization.JsonConstructor]
public sealed record Packet(DamageType Type, string? Aspect, double Amount)
{
    public Packet(DamageType type, double amount) : this(type, null, amount) { }

    /// <summary>
    /// The single resistance this packet answers to — <c>aspect ?? lane-of-type</c>
    /// (docs/damage-and-defense.md §2.2). <b>Null for the arcane aspect</b>, which has no lane
    /// and cannot be resisted, exposed, penetrated or immune-walled.
    /// </summary>
    public string? Lane => Aspect switch
    {
        null => DamageLanes.Of(Type),
        DamageAspects.Kinetic => null,
        var aspect => aspect,
    };

    /// <summary>
    /// Armour answers the <i>delivery</i>, resistance answers the <i>energy</i>. So armour
    /// applies to any physical-typed packet <b>regardless of aspect</b> — which is what stops a
    /// heat aspect being used to slip a sword past armour.
    /// </summary>
    public bool ArmourApplies => DamageTypes.IsPhysical(Type);

    public Packet WithAmount(double amount) => this with { Amount = amount };

    public override string ToString() =>
        Aspect is null ? $"{Type} {Amount:0.##}" : $"{Type}/{Aspect} {Amount:0.##}";
}

/// <summary>
/// One attack in flight: what is being thrown, by whom, at whom, and how it is described.
/// Replaces the <c>(damageType, baseDamage)</c> arguments the old calculator took.
/// </summary>
public sealed class Hit
{
    public required Combatant Source { get; init; }
    public required Combatant Target { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<Packet> Packets { get; init; }

    /// <summary>`attack`, `melee`, `heavy`, the damage type… — matched by conditions and affixes.</summary>
    public IReadOnlySet<string> Tags { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Control buildup toward Stun. Consumed in E2; carried now so the shape is fixed.</summary>
    public double StaggerPower { get; init; }

    /// <summary>True when the move gave no telegraph — the only hits Evade may roll against
    /// (D-07: the passive can never replace reading telegraphs).</summary>
    public bool Untelegraphed { get; init; }

    /// <summary>Damage before any mitigation — the denominator for "how much did I prevent?".</summary>
    public double RawTotal => Packets.Sum(p => p.Amount);

    /// <summary>The type shown in logs and loot messages: whichever packet is largest.</summary>
    public DamageType PrimaryType =>
        Packets.Count == 0 ? DamageType.Slashing : Packets.MaxBy(p => p.Amount)!.Type;

}
