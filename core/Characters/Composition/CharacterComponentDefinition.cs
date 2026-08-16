using System.Text.Json.Serialization;
using Dungeons.Characters.Modifiers;
using Dungeons.Content;

namespace Dungeons.Characters.Composition;

/// <summary>
/// Shared shape of the four data-driven identity components (Species, Base Class,
/// Prefix, Suffix). Each contributes numeric modifiers, descriptive tags, ability
/// ids, and rule-hook ids. Rule-breaking behaviour lives in code keyed by
/// <see cref="RuleIds"/>, not in the JSON (docs/json-schema.md §14–15).
/// </summary>
public abstract class CharacterComponentDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ModifierData> Modifiers { get; init; } = Array.Empty<ModifierData>();
    public IReadOnlyList<string> AbilityIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RuleIds { get; init; } = Array.Empty<string>();
}

/// <summary>Fundamental biological/metaphysical identity (docs/classes.md §2).</summary>
public sealed class SpeciesDefinition : CharacterComponentDefinition
{
}

/// <summary>
/// A mechanical mutation layered over the Base (docs/classes.md §4).
///
/// <para>A Prefix adds <b>one recognizable mechanic</b>, not ten small bonuses — and it is
/// authored <i>once</i>, against events. The hard rule that makes the roster tractable:
/// <b>a Prefix may never reference a Base.</b> Galvanic says "Charge accumulates when you
/// spend a resource"; that a Bastion therefore charges by blocking and a Wizard by releasing
/// a held spell is emergent, not authored. Without that rule the roster would be 15 × 25 = 375
/// hand-written combinations.</para>
/// </summary>
public sealed class PrefixDefinition : CharacterComponentDefinition
{
    /// <summary>One-line statement of the mechanic, for the Character Lab.</summary>
    public string Mechanic { get; init; } = string.Empty;

    /// <summary>A meter the Prefix brings with it, if its mechanic needs one. A build can run
    /// at most one Base gauge and one Prefix gauge — two meters, which stays readable.</summary>
    public GaugeDefinition? Gauge { get; init; }

    // Fully qualified: inside Dungeons.Characters.Composition, a bare `Rules.` would bind to
    // Dungeons.Characters.Rules (the ICharacterRule namespace), not the trigger-rule one.
    /// <summary>The event hooks that are the mechanic.</summary>
    public IReadOnlyList<Dungeons.Rules.TriggerRule> Rules { get; init; } =
        Array.Empty<Dungeons.Rules.TriggerRule>();
}

/// <summary>
/// One way a Suffix expresses itself, selected by channel.
///
/// <para><b><see cref="Channel"/> is the only coupling point between Suffix content and the
/// composition model.</b> Everything else here names events and effects, which are stable. If
/// channels become four, or get picked by the player, or key off attribute thresholds instead,
/// that is one field edited by a script — not fifty suffixes rewritten. This is deliberate:
/// the composition model has already changed several times.</para>
/// </summary>
public sealed class SuffixExpression
{
    public ExpressionChannel Channel { get; init; }

    /// <summary>The hook. Identical in shape to a Prefix rule — suffixes are not a parallel system.</summary>
    public Dungeons.Rules.TriggerRule Rule { get; init; } = new();

    /// <summary>The cost or risk, stated. Every good Suffix has one.</summary>
    public string Drawback { get; init; } = string.Empty;
}

/// <summary>
/// The rule-breaking identity component (docs/classes.md §6).
///
/// <para>Suffixes are where the absurdity lives, and they are explicitly allowed to reach
/// outside combat — harvesting, extraction, crafting, Realm danger. A Suffix should make the
/// player ask "wait, my character can do WHAT?", not read as "+7% damage".</para>
///
/// <para>A fully-expressed Suffix carries <b>one expression per channel</b>, so no build ever
/// looks at it and sees a mechanic meant for somebody else. Roster entries without expressions
/// are named and formatted but not yet designed — deliberate, so the naming system and the
/// roster can ship ahead of 150 authored mechanics.</para>
/// </summary>
public sealed class SuffixDefinition : CharacterComponentDefinition
{
    /// <summary>The core weird idea, in one line.</summary>
    public string Fantasy { get; init; } = string.Empty;

    /// <summary>Name-format style id. <b>Presentation only</b> — never read by mechanics.</summary>
    public string Format { get; init; } = "standard";

    /// <summary>Overrides the format template's suffix slot, for phrasings too good to generalise
    /// ("Against Medical Advice"). Presentation only.</summary>
    [JsonPropertyName("custom_phrase")]
    public string CustomPhrase { get; init; } = string.Empty;

    public IReadOnlyList<SuffixExpression> Expressions { get; init; } = Array.Empty<SuffixExpression>();

    /// <summary>True once every channel has an expression — i.e. every build can use it.</summary>
    public bool IsFullyExpressed =>
        Enum.GetValues<ExpressionChannel>().All(c => Expressions.Any(e => e.Channel == c));

    /// <summary>The expression a build on <paramref name="channel"/> gets, if any.</summary>
    public SuffixExpression? For(ExpressionChannel channel) =>
        Expressions.FirstOrDefault(e => e.Channel == channel);
}

/// <summary>
/// The core combat chassis — what grows, what resource it runs on, and how it fundamentally
/// plays (docs/classes.md §3).
///
/// <para>A Base is distinguished by its <b>engine</b>, not its flavour: how resource flows and
/// how its loop feels against a ticking clock. Two Bases with the same engine and different
/// themes are the same Base.</para>
/// </summary>
public sealed class BaseClassDefinition : CharacterComponentDefinition
{
    public ResourceType PrimaryResource { get; init; } = ResourceType.Stamina;

    /// <summary>
    /// Attribute growth weights by <see cref="AttributeType"/> name. Only the notable ones are
    /// listed; the rest of <see cref="AttributeGrowth.BudgetPerLevel"/> trickles evenly.
    /// </summary>
    public Dictionary<string, double> Growth { get; init; } = new();

    /// <summary>The signature meter, or null for Bases that deliberately run without one.</summary>
    public GaugeDefinition? Gauge { get; init; }

    /// <summary>Which channel this Base expresses Suffix modifiers through by default.</summary>
    [JsonPropertyName("default_channel")]
    public ExpressionChannel DefaultChannel { get; init; } = ExpressionChannel.Strike;

    /// <summary>One-line statement of the loop, for the Character Lab.</summary>
    public string Engine { get; init; } = string.Empty;

    /// <summary>The stated cost of the identity. Every Base has one.</summary>
    public string Weakness { get; init; } = string.Empty;
}
