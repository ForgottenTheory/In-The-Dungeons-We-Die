using Dungeons.Content;
using Dungeons.Presentation;
using Dungeons.Professions;
using Dungeons.Realms;
using Xunit;
using Xunit.Abstractions;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// The preparation screen's Tools panel: can the party do the <em>work</em> half of a run?
///
/// <para>Worn profession tools are E6 and deliberately absent — a tool slot with nothing reading
/// it would be a surface with no mechanic. What ships is real: the trades a Realm asks for,
/// measured against the levels the player actually has.</para>
/// </summary>
public class RealmFieldworkTests
{
    private readonly ITestOutputHelper _output;

    public RealmFieldworkTests(ITestOutputHelper output) => _output = output;

    private static ContentBundle FieldworkContent() => new()
    {
        Realms = TestPaths.LoadStore<RealmDefinition>("realms"),
        Actions = TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions"),
        Professions = TestPaths.LoadStore<ProfessionDefinition>("professions"),
    };

    private static IReadOnlyList<FieldworkRequirement> DarkForest(
        int knowledge,
        Func<string, int> professionLevel)
    {
        var content = FieldworkContent();
        return RealmFieldwork.Survey(content, content.Realms.GetById("realm.dark_forest"), knowledge, professionLevel);
    }

    private static int Needs(RealmInsight insight) => RealmKnowledgeLevels.Required[insight];

    [Fact]
    public void TheDarkForestAsksForRealTrades()
    {
        var trades = DarkForest(knowledge: 0, professionLevel: _ => 1);

        Assert.NotEmpty(trades);
        Assert.All(trades, trade =>
        {
            Assert.False(string.IsNullOrWhiteSpace(trade.ProfessionName));
            Assert.True(trade.TotalNodeCount > 0);
        });
    }

    /// <summary>
    /// A level-1 character can already work something here — that is the D28 first-session
    /// promise showing up on the preparation screen rather than being discovered by walking in.
    /// </summary>
    [Fact]
    public void AFreshCharacterCanWorkSomethingInTheReferenceRealm()
    {
        var trades = DarkForest(knowledge: 0, professionLevel: _ => 1);

        Assert.Contains(trades, trade => trade.CanWorkAnything);
    }

    [Fact]
    public void AMaxedCharacterCanWorkEveryNode()
    {
        var trades = DarkForest(knowledge: 0, professionLevel: _ => 99);

        Assert.All(trades, trade =>
        {
            Assert.True(trade.CanWorkEverything);
            Assert.Null(trade.NextLevelNeeded);
        });
    }

    /// <summary>Partly-reachable is the interesting case and must not collapse to a single
    /// yes/no — the player needs to know they can work three of five, not "not ready".</summary>
    [Fact]
    public void APartlyReachableTradeReportsBothCountsAndTheNextLevelNeeded()
    {
        var partly = DarkForest(knowledge: 0, professionLevel: _ => 1)
            .Where(trade => !trade.CanWorkEverything)
            .ToList();

        Assert.NotEmpty(partly);
        Assert.All(partly, trade =>
        {
            Assert.NotNull(trade.NextLevelNeeded);
            Assert.True(trade.NextLevelNeeded > trade.PlayerLevel);
            Assert.True(trade.WorkableNodeCount < trade.TotalNodeCount);
        });
    }

    /// <summary>
    /// Node <em>names</em> stay behind RichNodes, but hidden nodes must not even be counted here
    /// — a trade whose node count jumps at 320 knowledge would leak the existence of the hidden
    /// working before the player has earned it.
    /// </summary>
    [Fact]
    public void HiddenWorkingsAreNotCountedUntilTheRoutesAreLearned()
    {
        var before = DarkForest(Needs(RealmInsight.HiddenRoutes) - 1, _ => 99);
        var after = DarkForest(Needs(RealmInsight.HiddenRoutes), _ => 99);

        Assert.True(after.Sum(trade => trade.TotalNodeCount) > before.Sum(trade => trade.TotalNodeCount),
            "The Dark Forest hides a gathering node, so learning the routes should raise the count.");
    }

    /// <summary>164 realms ship and the picker can point at any of them; a realm with no
    /// gathering nodes is a valid answer, not a crash.</summary>
    [Fact]
    public void EveryShippedRealmSurveysWithoutThrowing()
    {
        var content = FieldworkContent();

        foreach (var realm in content.Realms.GetAll())
            RealmFieldwork.Survey(content, realm, knowledge: 0, professionLevel: _ => 1);
    }

    [Fact]
    public void RenderTheDarkForestFieldwork()
    {
        foreach (var trade in DarkForest(knowledge: 0, professionLevel: _ => 1))
            _output.WriteLine($"{trade.ProfessionName} L{trade.PlayerLevel} — "
                + $"{trade.WorkableNodeCount}/{trade.TotalNodeCount} node(s)"
                + (trade.NextLevelNeeded is { } next ? $", next needs L{next}" : string.Empty));
    }
}
