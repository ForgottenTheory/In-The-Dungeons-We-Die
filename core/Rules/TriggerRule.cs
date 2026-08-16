using System.Text.Json.Serialization;

namespace Dungeons.Rules;

/// <summary>
/// One test a trigger applies before firing. Authored as
/// <c>{ "kind": "hasTag", "text": "heavy" }</c> or <c>{ "kind": "amountAtLeast", "value": 10 }</c>.
///
/// <para>Both <see cref="Value"/> and <see cref="Text"/> exist on every condition rather than
/// each kind having its own shape — one schema keeps the JSON uniform and the validator simple,
/// at the cost of a couple of unused fields per entry. Worth it.</para>
/// </summary>
public sealed class ConditionSpec
{
    public string Kind { get; init; } = string.Empty;
    public double Value { get; init; }
    public string Text { get; init; } = string.Empty;

    /// <summary>Inverts the result, so one kind covers both "has" and "lacks".</summary>
    public bool Negate { get; init; }
}

/// <summary>
/// What a trigger does when it fires.
///
/// <para>Effects are <b>declared here and executed by handlers registered elsewhere</b>. That
/// split is what lets suffixes reference statuses, summons and Realm mechanics that do not
/// exist yet: the content is authorable and validated today, and the behaviour arrives when
/// the owning system does. An effect with no registered handler is an explicit no-op, not a
/// crash and not a silent success.</para>
/// </summary>
public sealed class EffectSpec
{
    public string Kind { get; init; } = string.Empty;

    /// <summary>The effect's headline number — damage, healing, modifier magnitude.</summary>
    public double Amount { get; init; }

    /// <summary>
    /// Names an event field that <see cref="Amount"/> multiplies, so an effect can be "40% of
    /// the damage that triggered it" without needing an expression parser. Empty means flat.
    /// </summary>
    [JsonPropertyName("scales_with")]
    public string ScalesWith { get; init; } = string.Empty;

    /// <summary>Free parameter — a modifier key, status id, item id, or shape hint.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>How long the effect lasts, where that means anything.</summary>
    [JsonPropertyName("duration_ticks")]
    public int DurationTicks { get; init; }

    /// <summary>Resolved magnitude for <paramref name="triggeringEvent"/>.</summary>
    public double Magnitude(Events.GameEvent triggeringEvent)
    {
        ArgumentNullException.ThrowIfNull(triggeringEvent);

        if (string.IsNullOrEmpty(ScalesWith))
            return Amount;

        var basis = string.Equals(ScalesWith, "amount", StringComparison.OrdinalIgnoreCase)
            ? triggeringEvent.Amount
            : triggeringEvent.Value(ScalesWith);

        return Amount * basis;
    }
}

/// <summary>
/// An event hook: when <see cref="Event"/> happens and every condition passes, do
/// <see cref="Effect"/>.
///
/// <para>This is the single shape Prefixes and Suffixes are authored in. A prefix is a list of
/// these; a suffix expression is one of these plus a channel selector. Neither ever names a
/// Base — which is what stops the roster becoming 15 × 25 hand-authored combinations.</para>
/// </summary>
public sealed class TriggerRule
{
    /// <summary>Stable id, used for cooldown bookkeeping and log attribution.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The <see cref="Events.GameEvents"/> kind this listens for.</summary>
    public string Event { get; init; } = string.Empty;

    /// <summary>All must pass. Empty means "always".</summary>
    public IReadOnlyList<ConditionSpec> When { get; init; } = Array.Empty<ConditionSpec>();

    public EffectSpec Effect { get; init; } = new();

    /// <summary>Ticks before this rule may fire again. 0 means every time.</summary>
    [JsonPropertyName("cooldown_ticks")]
    public int CooldownTicks { get; init; }

    /// <summary>Firing probability, 0–1. Rolled through the seeded source, so still reproducible.</summary>
    public double Chance { get; init; } = 1.0;

    /// <summary>Player-facing explanation, for the Character Lab and tooltips.</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>The closed vocabularies content may use. The validator checks against these, so a
/// typo in a prefix fails at load rather than silently never firing.</summary>
public static class RuleVocabulary
{
    // --- Conditions -------------------------------------------------------------------------
    public const string HasTag = "hasTag";
    public const string AmountAtLeast = "amountAtLeast";
    public const string AmountAtMost = "amountAtMost";
    public const string ValueAtLeast = "valueAtLeast";     // Text names the field
    public const string ValueAtMost = "valueAtMost";
    public const string SourceIsSelf = "sourceIsSelf";
    public const string TargetIsSelf = "targetIsSelf";
    public const string SelfHealthBelow = "selfHealthBelow";   // fraction, from event Values
    public const string SelfHealthAbove = "selfHealthAbove";
    public const string GaugeAtLeast = "gaugeAtLeast";         // fraction of capacity
    public const string FirstInEncounter = "firstInEncounter";

    public static readonly IReadOnlySet<string> Conditions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        HasTag, AmountAtLeast, AmountAtMost, ValueAtLeast, ValueAtMost,
        SourceIsSelf, TargetIsSelf, SelfHealthBelow, SelfHealthAbove, GaugeAtLeast, FirstInEncounter,
    };

    // --- Effects ------------------------------------------------------------------------------
    public const string Damage = "damage";
    public const string AreaDamage = "areaDamage";
    public const string Heal = "heal";
    public const string ApplyStatus = "applyStatus";          // Text = status id
    public const string GrantModifier = "grantModifier";      // Text = modifier key
    public const string GrantResource = "grantResource";      // Text = resource/gauge name
    public const string DrainResource = "drainResource";
    public const string SpawnEntity = "spawnEntity";          // Text = entity id
    public const string GrantItem = "grantItem";              // Text = item id
    public const string RevealInfo = "revealInfo";
    public const string Reposition = "reposition";
    public const string Interrupt = "interrupt";

    public static readonly IReadOnlySet<string> Effects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Damage, AreaDamage, Heal, ApplyStatus, GrantModifier, GrantResource, DrainResource,
        SpawnEntity, GrantItem, RevealInfo, Reposition, Interrupt,
    };

    /// <summary>Effects whose <see cref="EffectSpec.Text"/> must be a registered modifier key.</summary>
    public static readonly IReadOnlySet<string> ModifierKeyed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        GrantModifier,
    };
}
