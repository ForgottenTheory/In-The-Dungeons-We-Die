using System.Text.Json.Serialization;

namespace Dungeons.Actions;

/// <summary>
/// Tick timings for one action's lifecycle — the shared half of the Action vocabulary
/// (docs/moves.md §4.2). Combat moves use all four phases; profession actions run on a single
/// interval today and adopt this shape in E6.
///
/// <para>QUEUE → TELEGRAPH → WINDUP → EXECUTION → RECOVERY → READY. Telegraph and windup are
/// separate scheduler states since E2 — that split is what makes "interrupt during windup"
/// expressible, and it is why this type has three numbers rather than one time-to-impact.</para>
/// </summary>
public sealed class ActionTiming
{
    [JsonPropertyName("telegraphTicks")]
    public int TelegraphTicks { get; init; }

    [JsonPropertyName("windupTicks")]
    public int WindupTicks { get; init; }

    [JsonPropertyName("recoveryTicks")]
    public int RecoveryTicks { get; init; }

    /// <summary>Ticks from action start until the effect resolves.</summary>
    public int TimeToImpactTicks => TelegraphTicks + WindupTicks;
}

/// <summary>
/// One resource an action spends — the other shared half (docs/moves.md §4.2).
///
/// <para><see cref="Resource"/> names a pool (<c>health</c> / <c>stamina</c> / <c>mana</c>) or a
/// gauge (<c>Charge</c>, <c>Momentum</c>). Profession actions add item inputs to this same shape
/// in E6. Validated against the real pools and every authored gauge name, so a typo'd cost fails
/// at load rather than making a move free.</para>
/// </summary>
public sealed class ActionCost
{
    public string Resource { get; init; } = string.Empty;
    public double Amount { get; init; }

    public override string ToString() => $"{Resource} {Amount:0.##}";
}
