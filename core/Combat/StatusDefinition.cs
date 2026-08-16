using System.Text.Json.Serialization;
using Dungeons.Content;
using Dungeons.Modifiers;
using Dungeons.Rules;

namespace Dungeons.Combat;

/// <summary>
/// What kind of thing a status is. <b>The category is not flavour</b> — it decides the stacking
/// rule, the removal rule and what resists it (docs/statuses.md §2).
/// </summary>
public enum StatusCategory
{
    /// <summary>Damage over time. Resisted by the lane resistance at application.</summary>
    Ailment,

    /// <summary>Debuff with no damage. Resisted by status duration/effect modifiers.</summary>
    Impairment,

    /// <summary>Prevents or redirects action. Gated by Resolve, never applied directly.</summary>
    Control,

    /// <summary>Tactical marker, often self-applied.</summary>
    State,
}

/// <summary>How repeated applications combine.</summary>
public enum StackPolicy
{
    /// <summary>Independent instances up to <see cref="StatusDefinition.MaxStacks"/>. Poison.</summary>
    Stack,

    /// <summary>A stronger application replaces a weaker one; a weaker one refreshes duration. Burn.</summary>
    RefreshHighest,

    /// <summary>Magnitude is kept, duration resets. Most markers.</summary>
    RefreshDuration,

    /// <summary>One instance, later applications ignored entirely.</summary>
    Unique,
}

/// <summary>Where an ailment's magnitude comes from.</summary>
public enum MagnitudeBasis
{
    /// <summary>A flat authored number.</summary>
    Flat,

    /// <summary>
    /// A coefficient on the damage that landed in this status's lane. Because that figure is
    /// already post-mitigation, the target's lane resistance reduces the ailment too — one
    /// number, no second calculation, and "why is my Burn weak against this enemy?" answers
    /// itself.
    /// </summary>
    LaneDamage,
}

/// <summary>The magnitude rule for one status.</summary>
public sealed class StatusMagnitude
{
    public MagnitudeBasis Basis { get; init; } = MagnitudeBasis.Flat;
    public double Coefficient { get; init; } = 1.0;
}

/// <summary>A modifier a status contributes while it is active.</summary>
public sealed class StatusModifier
{
    public string Key { get; init; } = string.Empty;
    public double Value { get; init; }

    /// <summary>Multiplies by the instance's stack count. Corroded strips armour per stack.</summary>
    [JsonPropertyName("per_stack")]
    public bool PerStack { get; init; }
}

/// <summary>
/// A data-driven status (docs/statuses.md §6).
///
/// <para><b>There is deliberately no C# class per ailment.</b> Bleed, Poison, Burn, Chill and the
/// thirteen markers shipped prefixes reference are all rows in <c>game/data/statuses/</c>. The
/// controller manages only <i>lifetime</i> — apply, stack, tick, expire, cleanse — because
/// everything a status <i>does</i> is already expressible: <see cref="WhileActive"/> is a list of
/// modifier contributions, and the effect hooks are the same <see cref="EffectSpec"/> vocabulary
/// moves and affixes use.</para>
/// </summary>
public sealed class StatusDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public StatusCategory Category { get; init; } = StatusCategory.State;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>Resistance family, for ailments. Null for laneless statuses.</summary>
    public string? Lane { get; init; }

    [JsonPropertyName("duration_ticks")]
    public int DurationTicks { get; init; } = 60;

    /// <summary>0 means the status does nothing periodically — most impairments and states.</summary>
    [JsonPropertyName("tick_interval")]
    public int TickInterval { get; init; }

    [JsonPropertyName("stack_policy")]
    public StackPolicy StackPolicy { get; init; } = StackPolicy.RefreshDuration;

    [JsonPropertyName("max_stacks")]
    public int MaxStacks { get; init; } = 1;

    public StatusMagnitude Magnitude { get; init; } = new();

    /// <summary>Modifier contributions applied for as long as the status is on the target.</summary>
    [JsonPropertyName("while_active")]
    public IReadOnlyList<StatusModifier> WhileActive { get; init; } = Array.Empty<StatusModifier>();

    [JsonPropertyName("on_apply")]
    public IReadOnlyList<EffectSpec> OnApply { get; init; } = Array.Empty<EffectSpec>();

    [JsonPropertyName("per_tick")]
    public IReadOnlyList<EffectSpec> PerTick { get; init; } = Array.Empty<EffectSpec>();

    [JsonPropertyName("on_expire")]
    public IReadOnlyList<EffectSpec> OnExpire { get; init; } = Array.Empty<EffectSpec>();

    /// <summary>Which cleanse targets it. Defaults to the category name.</summary>
    [JsonPropertyName("cleanse_group")]
    public string CleanseGroup { get; init; } = string.Empty;

    /// <summary>Control buildup one application contributes toward Resolve. Controls only.</summary>
    [JsonPropertyName("control_buildup")]
    public double ControlBuildup { get; init; }

    /// <summary>
    /// A status that must already be present for this one to accumulate at all. Freeze requires
    /// Chill, which is what makes cold a two-step aspect rather than a third burst aspect.
    /// </summary>
    [JsonPropertyName("requires_status")]
    public string? RequiresStatus { get; init; }

    public string Group => string.IsNullOrEmpty(CleanseGroup)
        ? Category.ToString().ToLowerInvariant()
        : CleanseGroup;

    public bool IsControl => Category == StatusCategory.Control;
}

/// <summary>A status actually on a combatant right now.</summary>
public sealed class StatusInstance
{
    public required StatusDefinition Definition { get; init; }
    public required string SourceId { get; init; }
    public required long AppliedTick { get; init; }

    public double Magnitude { get; set; }
    public int Stacks { get; set; } = 1;
    public long ExpiresTick { get; set; }
    public long NextTickAt { get; set; }

    public string Id => Definition.Id;

    /// <summary>Total contribution of one <see cref="StatusModifier"/>, stacks included.</summary>
    public double Contribution(StatusModifier modifier) =>
        modifier.PerStack ? modifier.Value * Stacks : modifier.Value;

    public override string ToString() =>
        Stacks > 1 ? $"{Definition.Name} ×{Stacks} ({Magnitude:0.#})" : $"{Definition.Name} ({Magnitude:0.#})";
}
