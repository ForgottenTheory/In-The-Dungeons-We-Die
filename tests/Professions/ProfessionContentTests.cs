using Dungeons.Content;
using Dungeons.Professions;
using Xunit;

namespace Dungeons.Tests.Professions;

/// <summary>
/// The P1–P3 professions pass: the roster reaches the GDD §7.1 slice target, every profession
/// has something to do, and — the reason Mining came first — iron ore has a real source, so
/// GameRoot's startup ore seed could be deleted.
/// </summary>
public class ProfessionContentTests
{
    private static DataStore<ProfessionDefinition> Professions => TestPaths.LoadStore<ProfessionDefinition>("professions");
    private static DataStore<ProfessionActionDefinition> Actions => TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions");

    [Fact]
    public void IronOreHasAProducingAction_SoTheStartupSeedStaysDead()
    {
        Assert.Contains(Actions.GetAll(), a => a.Outputs.Any(o => o.ItemId == "material.iron_ore"));
    }

    [Fact]
    public void TheRosterMeetsTheSliceTarget_AndNoProfessionIsIdle()
    {
        var professions = Professions;
        var actions = Actions.GetAll();

        Assert.True(professions.Count >= 8, $"GDD §7.1 slice target is 8; found {professions.Count}.");

        foreach (var profession in professions.GetAll())
            Assert.Contains(actions, a => a.ProfessionId == profession.Id);
    }

    /// <summary>Interconnection is the point (§7.2): at least some actions must consume what
    /// other professions produce, or the roster is eight isolated faucets.</summary>
    [Fact]
    public void ProfessionsCrossFeed()
    {
        var actions = Actions.GetAll();
        var produced = actions
            .SelectMany(a => a.Outputs.Select(o => (a.ProfessionId, o.ItemId)))
            .ToLookup(p => p.ItemId, p => p.ProfessionId);

        var crossFeeds = actions
            .SelectMany(a => a.Inputs.Select(i => (Consumer: a.ProfessionId, i.ItemId)))
            .Count(pair => produced[pair.ItemId].Any(producer => producer != pair.Consumer));

        Assert.True(crossFeeds >= 4, $"expected several cross-profession chains, found {crossFeeds}.");
    }
}
