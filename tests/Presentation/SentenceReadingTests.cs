using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting.Identity;
using Dungeons.Events;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// The sentence voice (migration Phase 6, D53): every compiled effect sentence must read as
/// one player line, truthful to what <see cref="SentenceAssemblers"/> actually compiles, with
/// no engine vocabulary leaking through. This wording is what makes generated items legible,
/// so it is a rule, not decoration — the <c>CraftFormat</c> precedent a third time.
/// </summary>
public class SentenceReadingTests
{
    // --- Coverage: the voice must span the compilable grammar ----------------

    [Fact]
    public void EveryCompilableBehaviorHasAVoice()
    {
        var content = SyntheticVocabulary();

        foreach (var behaviorId in SentenceAssemblers.CompilableBehaviors)
        {
            var (trigger, payload) = FirstAccepted(behaviorId, content);
            var sentence = new ItemEffectSentence(
                ItemEffectCategory.Generated, trigger.Id, behaviorId, payload.Id, 2.0, 1.0);

            var reading = SentenceReadings.From(sentence, content);

            Assert.NotEqual(SentenceReadings.DormantVocabularyText, reading.Text);
            AssertSpeaksPlayerLanguage(reading.Text, behaviorId);
        }
    }

    [Fact]
    public void EveryShippedFloorSentenceReadsCleanly()
    {
        var content = ShippedContent();
        var floorPayloads = content.SignaturePayloads.GetAll().Where(p => p.Floor is not null).ToList();
        Assert.NotEmpty(floorPayloads);

        foreach (var payload in floorPayloads)
        {
            var sentence = new ItemEffectSentence(
                ItemEffectCategory.Floor, payload.Floor!.Trigger, payload.Floor.Behavior,
                payload.Id, MidRange(payload), payload.Floor.Chance);

            var reading = SentenceReadings.From(sentence, content);

            Assert.NotEqual(SentenceReadings.DormantVocabularyText, reading.Text);
            AssertSpeaksPlayerLanguage(reading.Text, payload.Id);
        }
    }

    // --- Modifier units: derived from the key registry, never a key list -----

    [Fact]
    public void MultiplicativeGrantsReadAsTheSignedDistanceFromOne()
    {
        // Bulwark authors factors (1.08–1.2) because multiplicative contributions multiply
        // raw — the phrase must say what equipping does, not echo the stored number.
        var content = ShippedContent();
        var reading = SentenceReadings.From(new ItemEffectSentence(
            ItemEffectCategory.Generated, "while_worn", "sustain", "bulwark", 1.15, 1.0), content);

        Assert.Contains("+15% Block Strength", reading.Text);
    }

    [Fact]
    public void FractionCappedAdditiveKeysReadAsPercent()
    {
        var content = ShippedContent();
        var reading = SentenceReadings.From(new ItemEffectSentence(
            ItemEffectCategory.Floor, "while_worn", "sustain", "warding", 0.12, 1.0), content);

        Assert.Contains("+12% Magic Resistance", reading.Text);
    }

    [Fact]
    public void UncappedAdditiveKeysReadFlat()
    {
        var content = ShippedContent();
        var reading = SentenceReadings.From(new ItemEffectSentence(
            ItemEffectCategory.Floor, "while_worn", "sustain", "vitality", 8, 1.0), content);

        Assert.Equal("While Worn: +8 Max Health", reading.Text);
    }

    // --- Clause shapes -------------------------------------------------------

    [Fact]
    public void AChanceLeadsTheClause()
    {
        var content = ShippedContent();
        var reading = SentenceReadings.From(new ItemEffectSentence(
            ItemEffectCategory.Generated, "on_hit", "afflict", "kindling", 2.5, 0.3), content);

        Assert.Contains("On Hit: 30% chance to inflict Burn (2.5)", reading.Text);
    }

    [Fact]
    public void BeneficialStatesAreGainedAndAilmentsInflicted()
    {
        var content = ShippedContent();

        var barrier = SentenceReadings.From(new ItemEffectSentence(
            ItemEffectCategory.Generated, "on_block", "afflict", "barrier", 4, 1.0), content);
        var burn = SentenceReadings.From(new ItemEffectSentence(
            ItemEffectCategory.Generated, "on_hit", "afflict", "kindling", 2, 1.0), content);

        Assert.Contains("gain Barrier", barrier.Text);
        Assert.Contains("inflict Burn", burn.Text);
    }

    [Fact]
    public void TheDrawbackReadsAsSuffered()
    {
        // §10.3's price, aimed at the wearer — the reading must never dress the curse up as
        // an effect on the enemy.
        var content = ShippedContent();
        var curse = content.SignaturePayloads.GetAll().First(p =>
            p.Binding.Kind == PayloadBindingKinds.Status
            && content.Statuses.TryGetById(p.Binding.Key, out var status)
            && status.Category == StatusCategory.Ailment);

        var reading = SentenceReadings.From(new ItemEffectSentence(
            ItemEffectCategory.Drawback, "on_hit", "afflict", curse.Id,
            2.0, ItemEffectTuning.DrawbackProcChance, AfflictsWearer: true), content);

        Assert.Contains("suffer", reading.Text);
        Assert.True(reading.AfflictsWearer);
    }

    [Fact]
    public void ExchangeRepeatsTheAssemblerArithmetic()
    {
        // The pact's price and payoff are the assembler's own numbers — paraphrasing the
        // magnitude (2) instead of the computed halves (1 and 3) would misquote the deal.
        var content = SyntheticVocabulary();
        var reading = SentenceReadings.From(new ItemEffectSentence(
            ItemEffectCategory.Generated, "on_hit", "exchange", "test_strike", 2.0, 1.0), content);

        Assert.Contains("pay 1 Health", reading.Text);
        Assert.Contains("deal 3 damage", reading.Text);
    }

    [Fact]
    public void StoreNamesTheGaugeAndItsBand()
    {
        var content = ShippedContent();
        var reading = SentenceReadings.From(new ItemEffectSentence(
            ItemEffectCategory.Generated, "on_block", "store", "bulwark", 1.12, 1.0), content);

        Assert.Contains("Bulwark Store", reading.Text);
        Assert.Contains("50% charge", reading.Text);
        Assert.Contains("+12% Block Strength", reading.Text);
    }

    [Fact]
    public void DrainOfANonHealthResourceTellsBothHalves()
    {
        // No drainResource handler exists: the compile is enemy damage + own mana. A reading
        // that said "drain Mana from the enemy" would promise a transfer that never happens.
        var content = SyntheticVocabulary();
        var reading = SentenceReadings.From(new ItemEffectSentence(
            ItemEffectCategory.Generated, "on_hit", "drain", "test_siphon", 2.0, 1.0), content);

        Assert.Contains("damage", reading.Text);
        Assert.Contains("Mana", reading.Text);
    }

    [Fact]
    public void MissingVocabularyReadsDormantNotWrong()
    {
        // Mirrors CompileAll's skip: a sentence whose vocabulary left the bundle grants
        // nothing, and the reading says so instead of inventing an effect.
        var content = ShippedContent();
        var reading = SentenceReadings.From(new ItemEffectSentence(
            ItemEffectCategory.Generated, "on_hit", "direct", "retired_payload", 2.0, 1.0), content);

        Assert.Equal(SentenceReadings.DormantVocabularyText, reading.Text);
    }

    // --- Harness -------------------------------------------------------------

    /// <summary>No underscores, no dotted ids, no engine arrows — the leak fence every
    /// rendered sentence passes through.</summary>
    private static void AssertSpeaksPlayerLanguage(string text, string context)
    {
        Assert.False(string.IsNullOrWhiteSpace(text), $"{context}: empty reading.");
        Assert.DoesNotContain("_", text);
        Assert.DoesNotContain("→", text);
        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(text, "[a-z]\\.[a-z]"),
            $"{context}: a dotted id leaked into '{text}'.");
    }

    private static (SignatureTriggerDefinition Trigger, SignaturePayloadDefinition Payload) FirstAccepted(
        string behaviorId, ContentBundle content)
    {
        foreach (var payload in content.SignaturePayloads.GetAll().OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            foreach (var trigger in content.SignatureTriggers.GetAll().OrderBy(t => t.Id, StringComparer.Ordinal))
            {
                if (SentenceAssemblers.Accepts(behaviorId, trigger, payload))
                    return (trigger, payload);
            }
        }

        throw new InvalidOperationException(
            $"No (trigger, payload) pair in the fixture satisfies behavior '{behaviorId}'.");
    }

    private static double MidRange(SignaturePayloadDefinition payload) =>
        payload.Range.Count == 2 ? (payload.Range[0] + payload.Range[1]) / 2.0 : 0;

    private static ContentBundle ShippedContent() => new()
    {
        SignatureTriggers = TestPaths.LoadStore<SignatureTriggerDefinition>("signature_triggers"),
        SignatureBehaviors = TestPaths.LoadStore<SignatureBehaviorDefinition>("signature_behaviors"),
        SignaturePayloads = TestPaths.LoadStore<SignaturePayloadDefinition>("signature_payloads"),
        Statuses = TestPaths.LoadStore<StatusDefinition>("statuses"),
        ModifierKeys = TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
        Moves = TestPaths.LoadStore<MoveDefinition>("moves"),
        MoveModifiers = TestPaths.LoadStore<MoveModifierDefinition>("move_modifiers"),
    };

    /// <summary>One payload per binding kind over a tiny vocabulary, so behaviors with no
    /// shipped payload of their food yet (echo, imbue, convert) still prove their voice.</summary>
    private static ContentBundle SyntheticVocabulary()
    {
        var bundle = new ContentBundle();

        bundle.SignatureTriggers.Add(new SignatureTriggerDefinition
        {
            Id = "on_hit", Name = "On Hit", Event = GameEvents.DamageDealt, Description = "x",
        });
        bundle.SignatureTriggers.Add(new SignatureTriggerDefinition
        {
            Id = "while_worn", Name = "While Worn", Standing = true, Description = "x",
        });

        bundle.Statuses.Add(new StatusDefinition { Id = "status.test_burn", Name = "Burn", Category = StatusCategory.Ailment });
        bundle.Moves.Add(new MoveDefinition { Id = "move.test_slam", Name = "Slam" });
        bundle.MoveModifiers.Add(new MoveModifierDefinition { Id = "movemod.test_brand", Name = "Brand" });
        bundle.ModifierKeys.Add(new Dungeons.Modifiers.ModifierKeyDefinition
        {
            Id = "resource.max_health", Name = "Max Health",
        });

        void Payload(string id, string kind, string key = "") => bundle.SignaturePayloads.Add(
            new SignaturePayloadDefinition
            {
                Id = id,
                Name = char.ToUpperInvariant(id[5]) + id[6..],
                Families = new[] { new PayloadFamilyStake { Identity = "identity.test", Rung = 1 } },
                Binding = new PayloadBinding { Kind = kind, Key = key },
                Range = new[] { 1.0, 5.0 },
                Description = "x",
            });

        Payload("test_strike", PayloadBindingKinds.Damage);
        Payload("test_mend", PayloadBindingKinds.Heal);
        Payload("test_siphon", PayloadBindingKinds.Resource, "mana");
        Payload("test_scorch", PayloadBindingKinds.Status, "status.test_burn");
        Payload("test_steady", PayloadBindingKinds.Modifier, "resource.max_health");
        Payload("test_echoing", PayloadBindingKinds.Move, "move.test_slam");
        Payload("test_branding", PayloadBindingKinds.MoveModifier, "movemod.test_brand");

        return bundle;
    }
}
