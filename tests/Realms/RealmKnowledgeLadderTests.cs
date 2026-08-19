using Dungeons.Content;
using Dungeons.Realms;
using Xunit;

namespace Dungeons.Tests.Realms;

/// <summary>
/// The two rungs Phase 8 added to the Realm Knowledge ladder, and the promise that it did not
/// move the five D38 already tuned.
///
/// <para>GDD §11.4 lists seven things Knowledge should buy. Six of them were information and all
/// six shipped by Phase 7. The seventh — <em>"unlock portal targeting"</em> — is the only one
/// that hands the player an <b>option</b>, and it is the last rung because you cannot aim at a
/// door you have not found.</para>
/// </summary>
public class RealmKnowledgeLadderTests
{
    private static RealmDefinition DarkForest() =>
        TestPaths.LoadStore<RealmDefinition>("realms").GetById("realm.dark_forest");

    private static int Needs(RealmInsight insight) => RealmKnowledgeLevels.Required[insight];

    // --- The ladder was bracketed, not rescaled -----------------------------

    /// <summary>
    /// <b>The five middle thresholds are exactly where D38 left them.</b> That balance pass was
    /// made against a measured per-run yield, and quietly rescaling it while adding rungs would
    /// have thrown the measurement away.
    /// </summary>
    [Fact]
    public void PhaseEightDidNotMoveTheThresholdsD38Tuned()
    {
        Assert.Equal(30, Needs(RealmInsight.EnemyWeaknesses));
        Assert.Equal(75, Needs(RealmInsight.Hazards));
        Assert.Equal(160, Needs(RealmInsight.RichNodes));
        Assert.Equal(320, Needs(RealmInsight.HiddenRoutes));
        Assert.Equal(560, Needs(RealmInsight.ExtractionRoutes));
    }

    [Fact]
    public void TheNewRungsBracketTheOldOnes()
    {
        Assert.True(Needs(RealmInsight.CommonResources) < Needs(RealmInsight.EnemyWeaknesses));
        Assert.True(Needs(RealmInsight.DeepEntry) > Needs(RealmInsight.ExtractionRoutes));
    }

    /// <summary>A first expedition must teach the party something, or the cheapest rung is not
    /// doing the job it was added for.</summary>
    [Fact]
    public void TheFirstRungIsReachableWellInsideASingleRun()
    {
        var realm = DarkForest();
        var oneThoroughRun =
            RealmTuning.KnowledgePerEnter
            + realm.Locations.Count * RealmTuning.KnowledgePerTravel
            + RealmTuning.KnowledgePerExtract;

        Assert.True(Needs(RealmInsight.CommonResources) < oneThoroughRun / 2,
            "the cheapest insight should land partway through a first expedition, not at the end of it.");
    }

    // --- Deep entry ----------------------------------------------------------

    [Fact]
    public void WithoutTheInsightEveryRunStartsAtTheEdge()
    {
        var realm = DarkForest();

        Assert.Equal(1, RealmRun.DeepestReachableEntry(realm, Needs(RealmInsight.DeepEntry) - 1));
        Assert.Equal(1, RealmRun.DeepestReachableEntry(realm, 0));
    }

    [Fact]
    public void WithTheInsightEveryAuthoredEntranceIsReachable()
    {
        var realm = DarkForest();

        Assert.Equal(realm.MaxDepth, RealmRun.DeepestReachableEntry(realm, Needs(RealmInsight.DeepEntry)));
    }

    /// <summary>A stale depth choice starts a shallower run rather than no run at all — refusing
    /// to open the door is how a preparation screen manufactures a stuck state (GDD §13.1).</summary>
    [Fact]
    public void AskingToStartTooDeepIsClampedRatherThanRefused()
    {
        var realm = DarkForest();
        var run = new RealmRun(realm, tier: 1, knowledge: 0, startingDepth: 3);

        Assert.Equal(1, run.CurrentDepth);
        Assert.True(run.Active);
    }

    [Fact]
    public void StartingDeepBeginsAtThatDepthsEntrance()
    {
        var realm = DarkForest();
        var run = new RealmRun(realm, tier: 1, knowledge: Needs(RealmInsight.DeepEntry), startingDepth: 2);

        Assert.Equal(2, run.CurrentDepth);
        Assert.Equal(realm.EntranceForDepth(2)!.Id, run.CurrentLocationId);
        Assert.Equal(RealmLocationType.Entrance, run.CurrentLocation.Type);
    }

    /// <summary>
    /// The price of the shortcut. Starting at depth 2 skips depth 1 entirely — its fights, its
    /// loot and the knowledge they would have paid. The insight is a route, not a reward.
    /// </summary>
    [Fact]
    public void StartingDeepSkipsTheShallowGroundEntirely()
    {
        var realm = DarkForest();
        var deep = new RealmRun(realm, tier: 1, knowledge: Needs(RealmInsight.DeepEntry), startingDepth: 2);

        Assert.DoesNotContain(deep.Destinations(), location => location.Depth == 1);
        Assert.All(deep.KnownAtCurrentDepth(), location => Assert.Equal(2, location.Depth));
    }

    /// <summary>Every shipped realm has to survive being started at its deepest door, or the
    /// option breaks the picker for 163 destinations.</summary>
    [Fact]
    public void EveryShippedRealmCanBeEnteredAtEveryDepthItAllows()
    {
        var realms = TestPaths.LoadStore<RealmDefinition>("realms");
        var fullKnowledge = Needs(RealmInsight.DeepEntry);

        foreach (var realm in realms.GetAll())
        {
            var deepest = RealmRun.DeepestReachableEntry(realm, fullKnowledge);
            for (var depth = 1; depth <= deepest; depth++)
            {
                var run = new RealmRun(realm, tier: 1, knowledge: fullKnowledge, startingDepth: depth);
                Assert.Equal(depth, run.CurrentDepth);
            }
        }
    }
}
