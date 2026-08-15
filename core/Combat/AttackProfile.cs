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
}

/// <summary>
/// A resolved defensive profile — the neutral combat view of equipped armor: flat
/// armor plus typed resistances (fractions in [0,1], keyed by damage-type/property name).
/// </summary>
public sealed class ArmorProfile
{
    public static readonly ArmorProfile None = new() { Armor = 0, Resistances = new Dictionary<string, double>() };

    public required double Armor { get; init; }
    public required IReadOnlyDictionary<string, double> Resistances { get; init; }

    public double ResistanceFor(string key) => Resistances.TryGetValue(key, out var v) ? v : 0.0;
}
