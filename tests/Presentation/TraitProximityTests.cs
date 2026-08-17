using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// §15.4's proximity hint, computed from the same authored conditions the resolver births
/// from — "within reach of Resilient; needs more flexibility" — so the hint can never lie.
/// </summary>
public class TraitProximityTests
{
    private static readonly TraitDefinition Resilient = new()
    {
        Id = "trait.resilient",
        Name = "Resilient",
        Condition = new Dictionary<string, PropertyRange>
        {
            ["hardness"] = new() { Min = 70 },
            ["flexibility"] = new() { Min = 60 },
        },
    };

    private static readonly TraitDefinition MergeBornOnly = new()
    {
        Id = "trait.bound_opposition",
        Name = "Bound Opposition",
    };

    private static PropertySet State(double hardness, double flexibility) => new(
        new Dictionary<string, double> { ["hardness"] = hardness, ["flexibility"] = flexibility });

    [Fact]
    public void ATraitOneShortConditionAwayReadsNearbyWithItsNeed()
    {
        var nearby = TraitProximity.Scan(State(75, 45), new[] { Resilient }, Array.Empty<string>());

        var near = Assert.Single(nearby);
        var need = Assert.Single(near.Needs);
        Assert.Equal("flexibility", need.Property);
        Assert.True(need.NeedsMore);
        Assert.Equal(15, need.Deficit, precision: 6);
    }

    [Fact]
    public void ADeficitOutsideTheWindowIsNotNearby() =>
        Assert.Empty(TraitProximity.Scan(State(75, 30), new[] { Resilient }, Array.Empty<string>()));

    [Fact]
    public void ATraitAlreadyCarriedIsNotNearby() =>
        Assert.Empty(TraitProximity.Scan(State(75, 45), new[] { Resilient }, new[] { "trait.resilient" }));

    [Fact]
    public void AMergeBornTraitHasNoStateToBeNear() =>
        Assert.Empty(TraitProximity.Scan(State(99, 99), new[] { MergeBornOnly }, Array.Empty<string>()));

    /// <summary>All conditions met means it is being born, not near — the birth line covers it.</summary>
    [Fact]
    public void AFullySatisfiedConditionIsNotNearby() =>
        Assert.Empty(TraitProximity.Scan(State(75, 65), new[] { Resilient }, Array.Empty<string>()));

    [Fact]
    public void AMaxConditionReadsNeedsLess()
    {
        var placid = new TraitDefinition
        {
            Id = "trait.placid",
            Name = "Placid",
            Condition = new Dictionary<string, PropertyRange> { ["instability"] = new() { Max = 10 } },
        };

        var state = new PropertySet(new Dictionary<string, double> { ["instability"] = 22 });
        var near = Assert.Single(TraitProximity.Scan(state, new[] { placid }, Array.Empty<string>()));
        var need = Assert.Single(near.Needs);

        Assert.False(need.NeedsMore);
        Assert.Equal(12, need.Deficit, precision: 6);
    }
}
