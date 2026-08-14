using Dungeons.Content;

namespace Dungeons.Combat;

/// <summary>
/// Tick timings for one action's lifecycle (docs/combat-spec.md §4, docs/json-schema.md §16).
/// Time-to-impact is telegraph + windup; recovery follows execution.
/// </summary>
public sealed class AbilityTiming
{
    public int TelegraphTicks { get; init; }
    public int WindupTicks { get; init; }
    public int RecoveryTicks { get; init; }

    /// <summary>Ticks from action start until the effect resolves.</summary>
    public int TimeToImpactTicks => TelegraphTicks + WindupTicks;
}

/// <summary>
/// Data-driven combat ability. Milestone 5 supports single-target damage abilities;
/// the effect shape widens (status, healing, area) in later work.
/// </summary>
public sealed class AbilityDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DamageType DamageType { get; init; } = DamageType.Slashing;
    public double BaseValue { get; init; }
    public int StaminaCost { get; init; }
    public AbilityTiming Timing { get; init; } = new();
}
