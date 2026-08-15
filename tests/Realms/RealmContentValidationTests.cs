using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Professions;
using Dungeons.Realms;
using Dungeons.Tests;
using Xunit;

namespace Dungeons.Tests.Realms;

/// <summary>
/// Validates the shipped Dark Forest map: connections are symmetric and resolve,
/// content references (actors, gather actions, reward materials) exist, and the
/// depth structure supports the extract-or-go-deeper loop.
/// </summary>
public class RealmContentValidationTests
{
    private static DataStore<T> Load<T>(string subfolder) where T : IDefinition
    {
        return TestPaths.LoadStore<T>(subfolder);
    }

    [Fact]
    public void DarkForest_IsWellFormed()
    {
        var realms = Load<RealmDefinition>("realms");
        var actors = Load<ActorDefinition>("actors");
        var actions = Load<ProfessionActionDefinition>("profession_actions");
        var materials = Load<MaterialDefinition>("materials");

        var forest = realms.GetById("realm.dark_forest");

        Assert.NotNull(forest.EntranceForDepth(1));
        Assert.NotNull(forest.EntranceForDepth(2));
        Assert.Contains(forest.Locations, l => l.Type == RealmLocationType.Descent);
        Assert.Contains(forest.Locations, l => l.Type == RealmLocationType.Extraction);

        foreach (var loc in forest.Locations)
        {
            foreach (var connection in loc.Connections)
            {
                Assert.True(forest.HasLocation(connection), $"{loc.Id} → unknown {connection}");
                // Symmetric edges so the party can move back and forth.
                Assert.True(forest.GetLocation(connection).Connections.Contains(loc.Id),
                    $"edge {loc.Id} → {connection} is not symmetric");
            }

            switch (loc.Type)
            {
                case RealmLocationType.Combat:
                    Assert.True(actors.Contains(loc.ActorId!), $"{loc.Id} → unknown actor {loc.ActorId}");
                    break;
                case RealmLocationType.Gather:
                    Assert.True(actions.Contains(loc.ProfessionActionId!), $"{loc.Id} → unknown action {loc.ProfessionActionId}");
                    break;
                case RealmLocationType.Event when !string.IsNullOrEmpty(loc.RewardItemId):
                    Assert.True(materials.Contains(loc.RewardItemId!), $"{loc.Id} → unknown reward {loc.RewardItemId}");
                    break;
            }
        }
    }

    [Fact]
    public void CanTraverse_Entrance_To_Descent_To_Depth2_To_Extraction()
    {
        var realms = Load<RealmDefinition>("realms");
        var run = new RealmRun(realms.GetById("realm.dark_forest"), tier: 1);

        // Depth 1: walk to the descent.
        Assert.True(run.TravelTo("loc.forest_path"));
        Assert.True(run.TravelTo("loc.goblin_camp"));
        Assert.True(run.TravelTo("loc.ruins"));
        Assert.True(run.TravelTo("loc.descent"));

        Assert.True(run.CanExtract);
        Assert.True(run.Descend());
        Assert.Equal(2, run.CurrentDepth);

        // Depth 2: reach the extraction portal.
        Assert.True(run.TravelTo("loc.brute_warren"));
        Assert.True(run.TravelTo("loc.deep_extraction"));
        Assert.Equal(RealmLocationType.Extraction, run.CurrentLocation.Type);
        Assert.True(run.CanExtract);
    }
}
