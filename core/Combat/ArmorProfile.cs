namespace Dungeons.Combat;

/// <summary>
/// A resolved defensive profile — the neutral combat view of equipped armor: flat armour plus
/// per-<b>lane</b> resistances (fractions, keyed by <see cref="DamageLanes"/>).
///
/// <para>The offensive twin (<c>AttackProfile</c>) was deleted in E4: weapons grant
/// <see cref="MoveDefinition"/>s now (D-18, D8 amended with intent preserved). Armour never
/// needed packets or riders, so this half of the seam survives unchanged.</para>
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
