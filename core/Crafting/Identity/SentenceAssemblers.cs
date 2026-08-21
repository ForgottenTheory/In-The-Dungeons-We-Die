using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Rules;

namespace Dungeons.Crafting.Identity;

/// <summary>Everything one compiled sentence grants, in the shapes the equip pipeline
/// already consumes: standing stat contributions, trigger rules, gauges, and standing
/// move-modifier attachments. Nothing here is a new runtime concept.</summary>
public sealed record CompiledSentence(
    IReadOnlyList<(string ModifierKey, double Value, string? Scope)> StatGrants,
    IReadOnlyList<TriggerRule> Rules,
    IReadOnlyList<GaugeDefinition> Gauges,
    IReadOnlyList<string> MoveModifierIds)
{
    public static readonly CompiledSentence Nothing = new(
        Array.Empty<(string, double, string?)>(), Array.Empty<TriggerRule>(),
        Array.Empty<GaugeDefinition>(), Array.Empty<string>());
}

/// <summary>
/// The behavior assemblers (§7.3, D43): each shipped behavior verb is one small compiler
/// from <i>trigger + payload + magnitude</i> to grants over machinery that already resolves —
/// the D16 bargain a third time. New sentence combinations are data; a genuinely new verb is
/// one method here, never an engine rewrite.
///
/// <para><b>The D30 fence, applied honestly:</b> <c>drainResource</c> has no registered
/// handler today, so <c>drain</c> compiles as damage-plus-restore (a faithful "take from the
/// enemy") and <c>store</c> compiles as feed-plus-band rather than release-on-full — the
/// release shape joins when a gauge-spend effect kind exists. An assembler that compiled to
/// a silent no-op would be the exact lie this architecture exists to make impossible.</para>
/// </summary>
public static class SentenceAssemblers
{
    /// <summary>Behavior ids this registry can compile — must cover every shipped behavior
    /// registry entry (pinned by test), and nothing else.</summary>
    public static readonly IReadOnlySet<string> CompilableBehaviors = new HashSet<string>(StringComparer.Ordinal)
    {
        "direct", "sustain", "amplify", "afflict", "retaliate", "drain",
        "convert", "echo", "imbue", "exchange", "store",
    };

    /// <summary>
    /// Whether <paramref name="behaviorId"/> can deliver <paramref name="payload"/> through
    /// <paramref name="trigger"/> — the candidate-space gate. Standing triggers only suit
    /// standing deliveries (sustain, convert's move rewrite, store's gauge... no: store FEEDS
    /// on events); event triggers suit everything else.
    /// </summary>
    public static bool Accepts(
        string behaviorId, SignatureTriggerDefinition trigger, SignaturePayloadDefinition payload)
    {
        var binding = payload.Binding.Kind;
        var standing = trigger.Standing;

        return behaviorId switch
        {
            "sustain" => standing && binding == PayloadBindingKinds.Modifier,
            "convert" => standing && binding == PayloadBindingKinds.MoveModifier,
            "direct" => !standing && binding
                is PayloadBindingKinds.Damage or PayloadBindingKinds.Heal or PayloadBindingKinds.Resource,
            "amplify" => !standing && binding == PayloadBindingKinds.Modifier
                && string.IsNullOrEmpty(payload.Binding.Scope), // the timed-grant path carries no scope
            "afflict" => !standing && binding == PayloadBindingKinds.Status,
            "retaliate" => !standing && binding
                is PayloadBindingKinds.Damage or PayloadBindingKinds.Status,
            "drain" => !standing && binding == PayloadBindingKinds.Resource,
            "echo" => !standing && binding == PayloadBindingKinds.Move,
            "imbue" => !standing && binding == PayloadBindingKinds.MoveModifier,
            "exchange" => !standing && binding
                is PayloadBindingKinds.Damage or PayloadBindingKinds.Heal,
            "store" => !standing && binding == PayloadBindingKinds.Modifier
                && string.IsNullOrEmpty(payload.Binding.Scope), // gauge bands are unscoped
            _ => false,
        };
    }

    /// <summary>Compiles one sentence into grants. Deterministic: the same sentence always
    /// compiles to the same grants, which is what lets sentences persist instead of grants.</summary>
    public static CompiledSentence Compile(
        ItemEffectSentence sentence,
        SignatureTriggerDefinition trigger,
        SignaturePayloadDefinition payload,
        ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(content);

        return sentence.BehaviorId switch
        {
            "sustain" => new CompiledSentence(
                new[] { (payload.Binding.Key, sentence.Magnitude, payload.Binding.Scope) },
                Array.Empty<TriggerRule>(), Array.Empty<GaugeDefinition>(), Array.Empty<string>()),

            "convert" => new CompiledSentence(
                Array.Empty<(string, double, string?)>(), Array.Empty<TriggerRule>(),
                Array.Empty<GaugeDefinition>(), new[] { payload.Binding.Key }),

            "direct" => RuleOnly(Rule(sentence, trigger, PayloadEffect(payload, sentence.Magnitude))),

            "amplify" => RuleOnly(Rule(sentence, trigger, new EffectSpec
            {
                Kind = RuleVocabulary.GrantModifier,
                Text = payload.Binding.Key,
                Amount = sentence.Magnitude,
                DurationTicks = ItemEffectTuning.AmplifyDurationTicks,
                Target = EffectTarget.Self,
            })),

            "afflict" => RuleOnly(Rule(sentence, trigger, new EffectSpec
            {
                Kind = RuleVocabulary.ApplyStatus,
                Text = payload.Binding.Key,
                Amount = sentence.Magnitude,
                Target = AfflictTarget(sentence, payload, content),
            })),

            "retaliate" => RuleOnly(Rule(sentence, trigger,
                PayloadEffect(payload, sentence.Magnitude, EffectTarget.TriggerSource))),

            // "Take from the enemy": hurt them, restore yourself — the transfer reading that
            // resolves today (drainResource has no handler; see the class doc).
            "drain" => RuleOnly(Rule(sentence, trigger,
                new EffectSpec { Kind = RuleVocabulary.Damage, Amount = sentence.Magnitude, Target = EffectTarget.TriggerTarget },
                new EffectSpec { Kind = RuleVocabulary.GrantResource, Text = payload.Binding.Key, Amount = sentence.Magnitude, Target = EffectTarget.Self })),

            "echo" => RuleOnly(Rule(sentence, trigger, new EffectSpec
            {
                Kind = RuleVocabulary.TriggerMove,
                Text = payload.Binding.Key,
            })),

            "imbue" => RuleOnly(Rule(sentence, trigger, new EffectSpec
            {
                Kind = RuleVocabulary.ModifyMove,
                Text = payload.Binding.Key,
                DurationTicks = ItemEffectTuning.ImbueDurationTicks,
                Target = EffectTarget.Self,
            })),

            // Health as cost (the pact shape): pay first, then a boosted payload.
            "exchange" => RuleOnly(Rule(sentence, trigger,
                new EffectSpec
                {
                    Kind = RuleVocabulary.Damage,
                    Amount = Math.Round(sentence.Magnitude * ItemEffectTuning.ExchangeHealthCostPerPoint, 2),
                    Target = EffectTarget.Self,
                },
                PayloadEffect(payload, Math.Round(sentence.Magnitude * ItemEffectTuning.ExchangeMagnitudeBoost, 2)))),

            "store" => StoreGauge(sentence, trigger, payload),

            _ => CompiledSentence.Nothing,
        };
    }

    /// <summary>Beneficial statuses (category State — Barrier, Guarded) land on the wearer;
    /// ailments/impairments/controls land on the trigger's target. A drawback sentence
    /// deliberately inverts this and curses the wearer (§10.3).</summary>
    private static EffectTarget AfflictTarget(
        ItemEffectSentence sentence, SignaturePayloadDefinition payload, ContentBundle content)
    {
        if (sentence.AfflictsWearer)
            return EffectTarget.Self;

        return content.Statuses.TryGetById(payload.Binding.Key, out var status)
            && status.Category == StatusCategory.State
                ? EffectTarget.Self
                : EffectTarget.TriggerTarget;
    }

    /// <summary>The store shape that resolves today: the trigger feeds a gauge; while the
    /// gauge sits above the band threshold, the payload's modifier is live. One gauge per
    /// sentence, named for the payload so the meter reads as what it charges.</summary>
    private static CompiledSentence StoreGauge(
        ItemEffectSentence sentence, SignatureTriggerDefinition trigger, SignaturePayloadDefinition payload)
    {
        var gauge = new GaugeDefinition
        {
            Name = $"{payload.Name} Store",
            Behaviour = GaugeBehaviour.BuildSpend,
            Max = ItemEffectTuning.StoreGaugeMax,
            Feeds = new[]
            {
                Rule(sentence, trigger, new EffectSpec
                {
                    Kind = RuleVocabulary.GrantResource,
                    Text = $"{payload.Name} Store",
                    Amount = ItemEffectTuning.StoreFeedPerTrigger,
                }),
            },
            Bands = new[]
            {
                new GaugeBand
                {
                    AtLeast = ItemEffectTuning.StoreBandThreshold,
                    Modifier = payload.Binding.Key,
                    Value = sentence.Magnitude,
                },
            },
        };

        return new CompiledSentence(
            Array.Empty<(string, double, string?)>(), Array.Empty<TriggerRule>(),
            new[] { gauge }, Array.Empty<string>());
    }

    /// <summary>The plain effect a damage/heal/resource/status payload delivers. Heals and
    /// resources default to the wearer (an on-kill heal must not try to heal the corpse);
    /// damage defaults to the rule's own target; <paramref name="targetOverride"/> is
    /// retaliate's aim-at-the-attacker.</summary>
    private static EffectSpec PayloadEffect(
        SignaturePayloadDefinition payload, double magnitude, EffectTarget? targetOverride = null) =>
        payload.Binding.Kind switch
        {
            PayloadBindingKinds.Damage => new EffectSpec
            {
                Kind = RuleVocabulary.Damage, Amount = magnitude, Target = targetOverride,
            },
            PayloadBindingKinds.Heal => new EffectSpec
            {
                Kind = RuleVocabulary.Heal, Amount = magnitude, Target = targetOverride ?? EffectTarget.Self,
            },
            PayloadBindingKinds.Resource => new EffectSpec
            {
                Kind = RuleVocabulary.GrantResource, Text = payload.Binding.Key, Amount = magnitude,
                Target = targetOverride ?? EffectTarget.Self,
            },
            PayloadBindingKinds.Status => new EffectSpec
            {
                Kind = RuleVocabulary.ApplyStatus, Text = payload.Binding.Key, Amount = magnitude,
                Target = targetOverride,
            },
            _ => new EffectSpec(),
        };

    private static TriggerRule Rule(
        ItemEffectSentence sentence, SignatureTriggerDefinition trigger, params EffectSpec[] effects) => new()
    {
        Id = sentence.RuleId,
        Event = trigger.Event ?? string.Empty,
        Chance = sentence.Chance,
        Effect = effects.Length == 1 ? effects[0] : new EffectSpec(),
        Effects = effects.Length > 1 ? effects : Array.Empty<EffectSpec>(),
    };

    private static CompiledSentence RuleOnly(TriggerRule rule) => new(
        Array.Empty<(string, double, string?)>(), new[] { rule },
        Array.Empty<GaugeDefinition>(), Array.Empty<string>());
}
