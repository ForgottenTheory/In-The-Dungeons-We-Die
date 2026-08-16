namespace Dungeons.Combat;

/// <summary>
/// A resolved attack the player (or an actor) can perform — the neutral combat view
/// of an equipped weapon. Combat consumes this; it never sees equipment types, so the
/// material→weapon rules can grow without touching the encounter (docs/itemization.md §3).
/// </summary>
public sealed class AttackProfile
{
    public required string Name { get; init; }
    public required DamageType DamageType { get; init; }
    public required double BaseDamage { get; init; }
    public required int StaminaCost { get; init; }
    public required AbilityTiming Timing { get; init; }

    /// <summary>The fallback attack for an unarmed character — the canonical default used as the
    /// weapon-resolution fallback, kept in Core so combat rules aren't authored in the client.</summary>
    public static readonly AttackProfile Unarmed = new()
    {
        Name = "Bare Fists",
        DamageType = DamageType.Crushing,
        BaseDamage = 3,
        StaminaCost = 3,
        Timing = new AbilityTiming { TelegraphTicks = 2, WindupTicks = 6, RecoveryTicks = 12 },
    };
}

/// <summary>
/// A resolved defensive profile — the neutral combat view of equipped armor: flat armour plus
/// per-<b>lane</b> resistances (fractions, keyed by <see cref="DamageLanes"/>).
///
/// <para><b>Re-keyed in E1 from damage-type name to lane (D-02).</b> Slashing/Crushing/Piercing
/// share one <c>physical</c> lane; per-type weakness moved to the enemy as a vulnerability
/// multiplier. Content authoring `"Slashing": 0.15` becomes `"physical": 0.15`.</para>
/// </summary>
public sealed class ArmorProfile
{
    public static readonly ArmorProfile None = new() { Armor = 0, Resistances = new Dictionary<string, double>() };

    public required double Armor { get; init; }

    /// <summary>Raw, uncapped totals. Capping happens in the pipeline so overcapping can absorb
    /// exposure without the raw value being lost (D-05a).</summary>
    public required IReadOnlyDictionary<string, double> Resistances { get; init; }

    public double ResistanceFor(string lane) => Resistances.TryGetValue(lane, out var v) ? v : 0.0;
}
