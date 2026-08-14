using Dungeons.Realms;
using Xunit;

namespace Dungeons.Tests.Realms;

public class RealmRunTests
{
    private static RealmDefinition Forest() => new()
    {
        Id = "realm.dark_forest",
        Name = "The Dark Forest",
        SupportedTiers = new[] { 1 },
        Locations = new[]
        {
            new RealmLocationDefinition { Id = "entrance", Name = "Camp Entrance", Type = RealmLocationType.Entrance, Depth = 1, Connections = new[] { "path" } },
            new RealmLocationDefinition { Id = "path", Name = "Forest Path", Type = RealmLocationType.Travel, Depth = 1, Connections = new[] { "entrance", "grove", "camp" } },
            new RealmLocationDefinition { Id = "grove", Name = "Old Grove", Type = RealmLocationType.Gather, Depth = 1, Connections = new[] { "path", "descent" }, ProfessionActionId = "action.chop_oak" },
            new RealmLocationDefinition { Id = "camp", Name = "Goblin Camp", Type = RealmLocationType.Combat, Depth = 1, Connections = new[] { "path", "descent" }, ActorId = "actor.goblin_raider" },
            new RealmLocationDefinition { Id = "descent", Name = "Descent", Type = RealmLocationType.Descent, Depth = 1, Connections = new[] { "grove", "camp" } },
            new RealmLocationDefinition { Id = "deep_entrance", Name = "Deep Path", Type = RealmLocationType.Entrance, Depth = 2, Connections = new[] { "warren" } },
            new RealmLocationDefinition { Id = "warren", Name = "Brute Warren", Type = RealmLocationType.Combat, Depth = 2, Connections = new[] { "deep_entrance", "deep_extract" }, ActorId = "actor.goblin_brute" },
            new RealmLocationDefinition { Id = "deep_extract", Name = "Extraction Portal", Type = RealmLocationType.Extraction, Depth = 2, Connections = new[] { "warren" } },
        },
    };

    [Fact]
    public void StartsAtDepthOneEntrance()
    {
        var run = new RealmRun(Forest(), tier: 1);
        Assert.Equal(1, run.CurrentDepth);
        Assert.Equal("entrance", run.CurrentLocationId);
        Assert.Contains("entrance", run.Visited);
        Assert.True(run.Active);
    }

    [Fact]
    public void Travel_OnlyToAdjacentSameDepthNodes()
    {
        var run = new RealmRun(Forest(), 1);
        Assert.False(run.TravelTo("grove"));   // not adjacent to entrance
        Assert.True(run.TravelTo("path"));
        Assert.True(run.TravelTo("camp"));
        Assert.Contains("camp", run.Visited);
    }

    [Fact]
    public void Destinations_ReflectConnections()
    {
        var run = new RealmRun(Forest(), 1);
        run.TravelTo("path");
        var ids = run.Destinations().Select(d => d.Id).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "camp", "entrance", "grove" }, ids);
    }

    [Fact]
    public void ClearingTracksPerLocation()
    {
        var run = new RealmRun(Forest(), 1);
        Assert.False(run.IsCleared("camp"));
        run.MarkCleared("camp");
        Assert.True(run.IsCleared("camp"));
    }

    [Fact]
    public void Descend_OnlyFromDescentNode_AdvancesDepth()
    {
        var run = new RealmRun(Forest(), 1);
        Assert.False(run.CanDescend);          // at entrance
        Assert.False(run.Descend());

        run.TravelTo("path");
        run.TravelTo("camp");
        run.TravelTo("descent");
        Assert.True(run.CanDescend);
        Assert.True(run.Descend());

        Assert.Equal(2, run.CurrentDepth);
        Assert.Equal("deep_entrance", run.CurrentLocationId);
    }

    [Fact]
    public void CannotTravelAcrossDepthsDirectly()
    {
        var run = new RealmRun(Forest(), 1);
        run.TravelTo("path");
        run.TravelTo("camp");
        run.TravelTo("descent");
        // "descent" connects only to same-depth nodes; deep_entrance is reached via Descend().
        Assert.DoesNotContain(run.Destinations(), d => d.Depth == 2);
    }

    [Fact]
    public void ExtractionAvailability_ByNodeType()
    {
        var run = new RealmRun(Forest(), 1);
        Assert.False(run.CanExtract); // entrance
        run.TravelTo("path");
        run.TravelTo("grove");
        run.TravelTo("descent");
        Assert.True(run.CanExtract);  // descent node
    }

    [Fact]
    public void End_DeactivatesRun()
    {
        var run = new RealmRun(Forest(), 1);
        run.End();
        Assert.False(run.Active);
        Assert.False(run.TravelTo("path"));
    }
}
