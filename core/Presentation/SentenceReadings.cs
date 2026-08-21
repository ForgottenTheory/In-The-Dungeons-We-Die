using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting.Identity;
using Dungeons.Modifiers;

namespace Dungeons.Presentation;

/// <summary>One item-effect sentence in player language. <see cref="Category"/> keeps D50's
/// taxonomy readable on every surface — the identity's promise, the roll, the Signature and
/// the drawback must stay distinguishable wherever the text lands.</summary>
public sealed record SentenceReading(ItemEffectCategory Category, string Text, bool AfflictsWearer);

/// <summary>
/// The player voice of the sentence grammar (migration Phase 6, D53): one
/// <see cref="ItemEffectSentence"/> becomes one gameplay-language line — "On Block: 30%
/// chance to gain Barrier (4)", "While Worn: +8 Max Health" — replacing the engine spelling
/// (<c>on_block → store → bulwark 0.15</c>) the panels showed during the migration.
///
/// <para><b>Wording is bound to what <see cref="SentenceAssemblers"/> actually compiles.</b>
/// Drain reads as damage-plus-recovery because that is what resolves (no drainResource
/// handler exists); store reads as charge-plus-band because release-on-full does not exist
/// yet; exchange repeats the assembler's own cost/boost arithmetic. A reading that promised
/// the designed shape instead of the compiled one would be exactly the lie D30 exists to
/// prevent. <c>SentenceReadingTests.EveryCompilableBehaviorHasAVoice</c> pins the coverage.</para>
/// </summary>
public static class SentenceReadings
{
    /// <summary>What a sentence reads as when its vocabulary is no longer in the bundle —
    /// the same condition under which <see cref="ItemEffectResolver.CompileAll"/> grants
    /// nothing, said out loud instead of guessed around.</summary>
    public const string DormantVocabularyText = "a dormant effect — the game no longer speaks its vocabulary";

    public static IReadOnlyList<SentenceReading> From(
        IEnumerable<ItemEffectSentence> sentences, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(sentences);
        return sentences.Select(sentence => From(sentence, content)).ToList();
    }

    public static SentenceReading From(ItemEffectSentence sentence, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(sentence);
        ArgumentNullException.ThrowIfNull(content);

        if (!content.SignatureTriggers.TryGetById(sentence.TriggerId, out var trigger)
            || !content.SignaturePayloads.TryGetById(sentence.PayloadId, out var payload))
        {
            return new SentenceReading(sentence.Category, DormantVocabularyText, sentence.AfflictsWearer);
        }

        var clause = Clause(sentence, payload, content);

        // Standing triggers compile to unconditional grants, so a chance would be a lie there;
        // everywhere else a sub-certain chance is part of the promise and leads the clause.
        var text = trigger.Standing || sentence.Chance >= 1.0
            ? $"{trigger.Name}: {clause}"
            : $"{trigger.Name}: {Percent(sentence.Chance)} chance to {clause}";

        return new SentenceReading(sentence.Category, text, sentence.AfflictsWearer);
    }

    /// <summary>The behavior's delivery, verb-first so a leading "X% chance to" folds onto
    /// every clause grammatically. One case per compilable behavior, in the assemblers'
    /// order.</summary>
    private static string Clause(
        ItemEffectSentence sentence, SignaturePayloadDefinition payload, ContentBundle content)
    {
        var magnitude = sentence.Magnitude;

        return sentence.BehaviorId switch
        {
            "sustain" => ModifierPhrase(payload.Binding.Key, magnitude, content)
                + ScopeSuffix(payload.Binding.Scope, content),

            "convert" => $"your moves carry {MoveModifierName(payload.Binding.Key, content)}",

            "direct" => DeliveryClause(payload, magnitude, content),

            "amplify" => $"gain {ModifierPhrase(payload.Binding.Key, magnitude, content)} "
                + $"for {ItemEffectTuning.AmplifyDurationTicks}t",

            "afflict" => AfflictClause(sentence, payload, magnitude, content),

            "retaliate" => payload.Binding.Kind == PayloadBindingKinds.Status
                ? $"strike back with {StatusName(payload.Binding.Key, content)} ({Number(magnitude)})"
                : $"strike back for {Number(magnitude)} damage",

            // The honest transfer reading: the enemy takes plain damage, the wearer recovers
            // the resource. For health both halves collapse into one word; for mana/stamina
            // they do not, and pretending otherwise would misname the damage half.
            "drain" => string.Equals(payload.Binding.Key, "health", StringComparison.OrdinalIgnoreCase)
                ? $"drain {Number(magnitude)} Health from the enemy"
                : $"deal {Number(magnitude)} damage and recover {Number(magnitude)} {ResourceName(payload.Binding.Key)}",

            "echo" => $"unleash {MoveName(payload.Binding.Key, content)}",

            "imbue" => $"imbue your moves with {MoveModifierName(payload.Binding.Key, content)} "
                + $"for {ItemEffectTuning.ImbueDurationTicks}t",

            // Mirrors the assembler's arithmetic exactly — the pact's price and payoff are
            // computed, not paraphrased.
            "exchange" => ExchangeClause(payload, magnitude, content),

            "store" => $"charge the {payload.Name} Store; at {Percent(ItemEffectTuning.StoreBandThreshold)} charge: "
                + ModifierPhrase(payload.Binding.Key, magnitude, content),

            _ => DormantVocabularyText,
        };
    }

    /// <summary>direct's payload delivery — also exchange's second half.</summary>
    private static string DeliveryClause(SignaturePayloadDefinition payload, double magnitude, ContentBundle content) =>
        payload.Binding.Kind switch
        {
            PayloadBindingKinds.Damage => $"deal {Number(magnitude)} damage",
            PayloadBindingKinds.Heal => $"restore {Number(magnitude)} Health",
            PayloadBindingKinds.Resource => $"recover {Number(magnitude)} {ResourceName(payload.Binding.Key)}",
            _ => DormantVocabularyText,
        };

    /// <summary>Mirrors <c>SentenceAssemblers.AfflictTarget</c>: beneficial states land on the
    /// wearer ("gain"), ailments on the enemy ("inflict"), and a drawback curses its own
    /// wearer ("suffer") — the reading names the same target the compiled rule aims at.</summary>
    private static string AfflictClause(
        ItemEffectSentence sentence, SignaturePayloadDefinition payload, double magnitude, ContentBundle content)
    {
        var status = StatusName(payload.Binding.Key, content);
        if (sentence.AfflictsWearer)
            return $"suffer {status} ({Number(magnitude)})";

        var beneficial = content.Statuses.TryGetById(payload.Binding.Key, out var definition)
            && definition.Category == StatusCategory.State;
        return beneficial
            ? $"gain {status} ({Number(magnitude)})"
            : $"inflict {status} ({Number(magnitude)})";
    }

    private static string ExchangeClause(SignaturePayloadDefinition payload, double magnitude, ContentBundle content)
    {
        var healthCost = Math.Round(magnitude * ItemEffectTuning.ExchangeHealthCostPerPoint, 2);
        var boosted = Math.Round(magnitude * ItemEffectTuning.ExchangeMagnitudeBoost, 2);
        return $"pay {Number(healthCost)} Health to {DeliveryClause(payload, boosted, content)}";
    }

    // ---- Modifier grants in player units -----------------------------------------------------

    /// <summary>
    /// A modifier grant as the player reads it: "+8 Max Health", "+12% Magic Resistance",
    /// "+15% Block Strength". Whether a value is a fraction-of-one is derived from the key
    /// registry, never from a hardcoded key list: diminishing keys are chances by design;
    /// additive keys capped at ≤ 1 hold fractions (resistances, crit); multiplicative keys
    /// hold factors and read as the signed distance from ×1. Flat otherwise.
    /// <c>ShippedModifierKeysFormatAsExpected</c> pins the derivation against the registry so
    /// a key that breaks the rule is caught, not misread.
    /// </summary>
    public static string ModifierPhrase(string modifierKey, double value, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.ModifierKeys.TryGetById(modifierKey, out var key))
            return $"{SignedNumber(value)} {modifierKey}";

        if (key.Kind == ModifierKind.Multiplicative)
            return $"{SignedPercent(value - 1.0)} {key.Name}";

        var readsAsFraction = key.Kind == ModifierKind.Diminishing || key.Max is <= 1.0;
        return readsAsFraction
            ? $"{SignedPercent(value)} {key.Name}"
            : $"{SignedNumber(value)} {key.Name}";
    }

    /// <summary>A sustain grant's optional scope — "status:status.burn" reads as the scoped
    /// thing's own name. No shipped payload authors one yet; the shape exists so the first
    /// that does reads correctly instead of leaking the pair.</summary>
    private static string ScopeSuffix(string? scope, ContentBundle content)
    {
        if (string.IsNullOrEmpty(scope))
            return string.Empty;

        var separator = scope.IndexOf(':');
        var scopedValue = separator >= 0 && separator < scope.Length - 1 ? scope[(separator + 1)..] : scope;

        if (content.Statuses.TryGetById(scopedValue, out var status))
            return $" — {status.Name} only";
        if (content.Moves.TryGetById(scopedValue, out var move))
            return $" — {move.Name} only";
        return $" — {scopedValue} only";
    }

    // ---- Vocabulary names, falling back to the id only when the bundle lacks the entry -------

    private static string StatusName(string statusId, ContentBundle content) =>
        content.Statuses.TryGetById(statusId, out var status) ? status.Name : statusId;

    private static string MoveName(string moveId, ContentBundle content) =>
        content.Moves.TryGetById(moveId, out var move) ? move.Name : moveId;

    private static string MoveModifierName(string moveModifierId, ContentBundle content) =>
        content.MoveModifiers.TryGetById(moveModifierId, out var moveModifier) ? moveModifier.Name : moveModifierId;

    private static string ResourceName(string resourceKey) =>
        resourceKey.Length > 1
            ? char.ToUpperInvariant(resourceKey[0]) + resourceKey[1..]
            : resourceKey.ToUpperInvariant();

    // ---- Numbers ------------------------------------------------------------------------------

    private static string Number(double value) => value.ToString("0.##");

    private static string SignedNumber(double value) =>
        value >= 0 ? $"+{Number(value)}" : $"-{Number(-value)}";

    private static string SignedPercent(double fraction) =>
        fraction >= 0 ? $"+{Percent(fraction)}" : $"-{Percent(-fraction)}";

    /// <summary>"30%" — the house percent (no space), matching risk and odds lines.</summary>
    private static string Percent(double fraction) => $"{Math.Round(fraction * 100, 1):0.#}%";
}
