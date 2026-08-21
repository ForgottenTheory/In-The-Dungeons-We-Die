using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// The forge preview's player voice (Phase 6, D53): the draw table reads as likelihood words
/// derived from the scores it hides, the floor reads as sentences, refusals read plainly —
/// and the Advanced voice keeps the exact scores, so depth is a toggle, never a different truth.
/// </summary>
public class MintReadingTests
{
    private const string Vital = "identity.vital";
    private const string Dense = "identity.dense";

    [Fact]
    public void TheNormalVoiceHidesScoresAndEngineIds()
    {
        var content = Content();
        var composition = LongswordComposition(content, DenseVitalIron(content));
        var effects = new ItemEffectResolver(content).Project(composition);

        var preview = MintReadings.Preview(composition, effects, firstOfItsKind: true, content);

        Assert.Contains("first of its kind", preview);
        Assert.Contains("Guaranteed: While Worn: ", preview);
        Assert.Contains("Will draw", preview);
        Assert.DoesNotContain("→", preview);
        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(preview, "[a-z]_[a-z]"),
            $"an engine id leaked into the preview:\n{preview}");
    }

    [Fact]
    public void LikelihoodIsMeasuredAgainstTheUniformShare()
    {
        // Ten rows totalling 10: a 1.0 row sits exactly at uniform → Possible; a 2.5 row is
        // a front-runner → Likely; a 0.3 row is suppressed → A long shot. A flat table reads
        // all Possible, which is the truth.
        Assert.Equal("Possible", MintReadings.LikelihoodWord(1.0, 10.0, 10));
        Assert.Equal("Likely", MintReadings.LikelihoodWord(2.5, 10.0, 10));
        Assert.Equal("A long shot", MintReadings.LikelihoodWord(0.3, 10.0, 10));
    }

    [Fact]
    public void EveryTableRowCarriesALikelihoodWord()
    {
        var content = Content();
        var composition = LongswordComposition(content, DenseVitalIron(content));
        var effects = new ItemEffectResolver(content).Project(composition);
        Assert.NotEmpty(effects.Candidates);

        var totalScore = effects.Candidates.Sum(c => c.Score);
        foreach (var candidate in effects.Candidates)
        {
            var line = MintReadings.CandidateLine(candidate, totalScore, effects.Candidates.Count, content);
            Assert.True(
                line.StartsWith("Likely — ", StringComparison.Ordinal)
                || line.StartsWith("Possible — ", StringComparison.Ordinal)
                || line.StartsWith("A long shot — ", StringComparison.Ordinal),
                $"row without a likelihood word: '{line}'");
        }
    }

    [Fact]
    public void TheBreachReadsInWords()
    {
        // §9: oak's profile favors barrier from outside the open families — the row must say
        // so in words, not a glyph.
        var content = Content();
        var composition = BucklerComposition(content, OakboundVitalIron(content));
        var effects = new ItemEffectResolver(content).Project(composition);
        Assert.Contains(effects.Candidates, c => c.FromProfileBreach);

        var preview = MintReadings.Preview(composition, effects, firstOfItsKind: false, content);

        Assert.Contains("beyond its families", preview);
        Assert.DoesNotContain("◇", preview);
    }

    [Fact]
    public void TheAdvancedVoiceKeepsTheExactScores()
    {
        var content = Content();
        var composition = LongswordComposition(content, DenseVitalIron(content));
        var effects = new ItemEffectResolver(content).Project(composition);

        var advanced = MintReadings.Advanced(composition, effects, content);

        Assert.Contains("→", advanced);
        Assert.Contains("signature", advanced);
        Assert.Contains(effects.Candidates[0].TriggerId, advanced);
    }

    [Fact]
    public void RefusalsSpeakPlainly()
    {
        foreach (var failure in Enum.GetValues<IdentityCompositionFailure>())
        {
            if (failure == IdentityCompositionFailure.None)
                continue;
            var text = MintReadings.CompositionRefusal(failure);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain(failure.ToString(), text);
        }
    }

    // --- Harness (the resolver tests' fixtures, shared shape) ----------------

    private static ContentBundle Content() => new()
    {
        Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
        Forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms"),
        Identities = TestPaths.LoadStore<IdentityDefinition>("identities"),
        SignatureTriggers = TestPaths.LoadStore<SignatureTriggerDefinition>("signature_triggers"),
        SignatureBehaviors = TestPaths.LoadStore<SignatureBehaviorDefinition>("signature_behaviors"),
        SignatureThemes = TestPaths.LoadStore<SignatureThemeDefinition>("signature_themes"),
        SignaturePayloads = TestPaths.LoadStore<SignaturePayloadDefinition>("signature_payloads"),
        Statuses = TestPaths.LoadStore<StatusDefinition>("statuses"),
        ModifierKeys = TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
        Moves = TestPaths.LoadStore<MoveDefinition>("moves"),
        MoveModifiers = TestPaths.LoadStore<MoveModifierDefinition>("move_modifiers"),
    };

    private static IdentityMaterialState DenseVitalIron(ContentBundle content) =>
        IdentityStateResolver.StateOf(content.Materials.GetById("material.iron_ingot"))! with
        {
            Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 1) },
        };

    private static IdentityMaterialState OakboundVitalIron(ContentBundle content) =>
        DenseVitalIron(content) with
        {
            Roots = new[]
            {
                new ProvenanceRoot("material.iron_ingot", 0.85),
                new ProvenanceRoot("material.oak", 0.15),
            },
        };

    private static IdentityComposition LongswordComposition(
        ContentBundle content, IdentityMaterialState metalState)
    {
        var iron = content.Materials.GetById("material.iron_ingot");
        var leather = content.Materials.GetById("material.leather");
        var composition = IdentityEquipmentComposer.Compose(
            content.Forms.GetById("form.longsword"),
            new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
            {
                ["edge"] = (iron, metalState),
                ["core"] = (iron, metalState),
                ["binding"] = (leather, IdentityStateResolver.StateOf(leather)!),
            },
            content);
        Assert.Equal(IdentityCompositionFailure.None, composition.Failure);
        return composition;
    }

    private static IdentityComposition BucklerComposition(
        ContentBundle content, IdentityMaterialState metalState)
    {
        var iron = content.Materials.GetById("material.iron_ingot");
        var composition = IdentityEquipmentComposer.Compose(
            content.Forms.GetById("form.buckler"),
            new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
            {
                ["face"] = (iron, metalState),
            },
            content);
        Assert.Equal(IdentityCompositionFailure.None, composition.Failure);
        return composition;
    }
}
