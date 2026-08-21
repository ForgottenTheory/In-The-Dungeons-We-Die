using Dungeons.Content;
using Dungeons.Crafting.Identity;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// The bench's player voice (migration Phase 6): refusals in words, change lines diffed from
/// the states the engine produced, ranks as rung words, and the §4 fairness odds intact.
/// </summary>
public class VerbReadingTests
{
    private const string Vital = "identity.vital";
    private const string Dense = "identity.dense";

    [Fact]
    public void EveryRefusalSpeaksPlainly()
    {
        foreach (var reason in Enum.GetValues<VerbFailureReason>())
        {
            var text = VerbReadings.Refusal(reason);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain(reason.ToString(), text);
        }
    }

    [Fact]
    public void ARevealReadsAsAwakening()
    {
        var before = new IdentityMaterialState { Latent = new[] { Vital }, Capacity = 2 };
        var after = before with
        {
            Identities = new[] { new IdentityStake(Vital, 1) },
            Latent = Array.Empty<string>(),
        };

        var lines = Lines(CraftVerb.Reveal, before, after);

        Assert.Contains("Vital awakens.", lines);
    }

    [Fact]
    public void ATransferredIdentitySettlesInAtItsRungWord()
    {
        var before = new IdentityMaterialState { Capacity = 2 };
        var after = before with { Identities = new[] { new IdentityStake(Dense, 2) } };

        var lines = Lines(CraftVerb.Transfer, before, after);

        Assert.Contains("Dense (improved) settles in.", lines);
        Assert.DoesNotContain(lines, line => line.Contains("rank 2"));
    }

    [Fact]
    public void ADevelopReadsAsDeepening()
    {
        var before = new IdentityMaterialState
        {
            Identities = new[] { new IdentityStake(Vital, 1) }, Capacity = 2,
        };
        var after = before with { Identities = new[] { new IdentityStake(Vital, 2) } };

        var lines = Lines(CraftVerb.Develop, before, after);

        Assert.Contains("Vital deepens — now improved.", lines);
    }

    [Fact]
    public void ADisplaceNamesBothHalvesOfTheSwap()
    {
        var before = new IdentityMaterialState
        {
            Identities = new[] { new IdentityStake(Dense, 1) }, Capacity = 1,
        };
        var after = before with { Identities = new[] { new IdentityStake(Vital, 1) } };

        var lines = Lines(CraftVerb.Displace, before, after);

        Assert.Contains("Dense is ejected — no refund.", lines);
        Assert.Contains("Vital settles in.", lines);
    }

    [Fact]
    public void ConditionStepsAndRiskOddsStayOnScreen()
    {
        var before = new IdentityMaterialState
        {
            Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 1) },
            Capacity = 1,
            Condition = Condition.Worked,
        };
        var after = before with { Condition = Condition.Strained };
        var projection = new VerbProjection(
            null, after, Array.Empty<IdentityMaterialState>(),
            new VerbRisks(FractureChance: 0.15, DestructionChance: 0),
            Array.Empty<VerbStep>());

        var lines = VerbReadings.ProjectionLines(CraftVerb.Develop, before, projection, null, Content());

        Assert.Contains("Condition: Worked → Strained.", lines);
        Assert.Contains(lines, line => line.Contains("chance the newest identity fractures away"));
    }

    [Fact]
    public void ReachingFragileWarnsOfTheGamble()
    {
        var before = new IdentityMaterialState { Condition = Condition.Strained };
        var after = before with { Condition = Condition.Fragile };

        var lines = Lines(CraftVerb.Transfer, before, after);

        Assert.Contains(lines, line => line.Contains("Deeper work now gambles destruction"));
    }

    [Fact]
    public void WorkmanshipSpeaksWordsNotNumbers()
    {
        var before = new IdentityMaterialState { Quality = 40 };

        Assert.Contains("Workmanship: decent → fine.", Lines(CraftVerb.Refine, before, before with { Quality = 55 }));
        Assert.Contains("Workmanship improves a little — still decent.",
            Lines(CraftVerb.Refine, before with { Quality = 30 }, before with { Quality = 45 }));
    }

    [Fact]
    public void AFractureOutcomeNamesTheBrokenIdentity()
    {
        var before = new IdentityMaterialState
        {
            Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 1) },
            Capacity = 1,
        };
        var outcome = new VerbOutcome(
            VerbResultKind.Fractured, null, before, Array.Empty<IdentityMaterialState>(),
            null, Vital, VerbRisks.None, Array.Empty<VerbStep>());

        var lines = VerbReadings.OutcomeLines(CraftVerb.Develop, before, outcome, null, Content());

        Assert.Contains(lines, line => line.Contains("Vital breaks away"));
    }

    [Fact]
    public void TheOutcomeVoiceMatchesThePreviewVoiceOnSuccess()
    {
        // Preview parity, presentation edition: a clean success must read exactly as its
        // preview did, or the panel promised one thing and reported another.
        var content = Content();
        var before = new IdentityMaterialState { Latent = new[] { Vital }, Capacity = 2 };
        var after = before with
        {
            Identities = new[] { new IdentityStake(Vital, 1) },
            Latent = Array.Empty<string>(),
        };

        var preview = VerbReadings.ProjectionLines(
            CraftVerb.Reveal, before,
            new VerbProjection(null, after, Array.Empty<IdentityMaterialState>(), VerbRisks.None, Array.Empty<VerbStep>()),
            null, content);
        var outcome = VerbReadings.OutcomeLines(
            CraftVerb.Reveal, before,
            new VerbOutcome(VerbResultKind.Succeeded, null, after, Array.Empty<IdentityMaterialState>(),
                null, null, VerbRisks.None, Array.Empty<VerbStep>()),
            null, content);

        Assert.Equal(preview, outcome);
    }

    [Fact]
    public void AnExtractNamesWhatItDrawsOut()
    {
        var before = new IdentityMaterialState
        {
            Identities = new[] { new IdentityStake(Vital, 2) }, Capacity = 1,
        };
        var carrier = new IdentityMaterialState
        {
            Identities = new[] { new IdentityStake(Vital, 2) }, Capacity = 1, IsCarrier = true,
        };
        var projection = new VerbProjection(
            null, null, new[] { carrier }, VerbRisks.None, Array.Empty<VerbStep>());

        var lines = VerbReadings.ProjectionLines(CraftVerb.Extract, before, projection, null, Content());

        Assert.Contains(lines, line => line.Contains("Draws Vital (improved) out onto a fresh carrier"));
    }

    // --- Harness -------------------------------------------------------------

    private static IReadOnlyList<string> Lines(
        CraftVerb verb, IdentityMaterialState before, IdentityMaterialState after) =>
        VerbReadings.ProjectionLines(
            verb, before,
            new VerbProjection(null, after, Array.Empty<IdentityMaterialState>(), VerbRisks.None, Array.Empty<VerbStep>()),
            null, Content());

    private static ContentBundle Content() => new()
    {
        Identities = TestPaths.LoadStore<IdentityDefinition>("identities"),
    };
}
