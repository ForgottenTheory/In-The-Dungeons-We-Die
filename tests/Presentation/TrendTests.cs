using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// §2D — trends derive from the algebra's typed <see cref="PropertyChangeKind"/> records,
/// never re-inferred from numbers, and the fold's precedence rules are pinned here.
/// </summary>
public class TrendTests
{
    private static TransformationStepResult Step(params PropertyChange[] changes) => new(
        new PropertySet(new Dictionary<string, double>()),
        new TransferCoefficients(1, 1, 1, 1, 1),
        StressReleased: 0,
        Changes: changes);

    private static PropertyMovement One(params TransformationStepResult[] steps) =>
        Assert.Single(Trends.Aggregate(steps));

    [Fact]
    public void AChannelGainReadsRising()
    {
        var movement = One(Step(new PropertyChange("heat", 20, 45, PropertyChangeKind.OnChannelTransfer)));

        Assert.Equal(Trend.Rising, movement.Trend);
        Assert.True(movement.CrossesTier); // Low → Moderate
    }

    [Fact]
    public void AChannelLossReadsFalling() =>
        Assert.Equal(Trend.Falling, One(Step(new PropertyChange("hardness", 65, 50, PropertyChangeKind.OnChannelTransfer))).Trend);

    [Fact]
    public void StartingFromNothingReadsEmerging() =>
        Assert.Equal(Trend.Emerging, One(Step(new PropertyChange("heat", 0, 36, PropertyChangeKind.OnChannelTransfer))).Trend);

    [Fact]
    public void EndingAtNothingReadsVanishing() =>
        Assert.Equal(Trend.Vanishing, One(Step(new PropertyChange("toxicity", 8, 0, PropertyChangeKind.Pruned))).Trend);

    [Fact]
    public void AnnihilationWinsOverEverythingElse()
    {
        var movement = One(Step(
            new PropertyChange("heat", 30, 40, PropertyChangeKind.OnChannelTransfer),
            new PropertyChange("heat", 40, 22, PropertyChangeKind.Annihilation)));

        Assert.Equal(Trend.Conflicting, movement.Trend);
    }

    [Fact]
    public void OffChannelDilutionReadsFading() =>
        Assert.Equal(Trend.Fading, One(Step(new PropertyChange("solubility", 40, 28, PropertyChangeKind.Dilution))).Trend);

    [Fact]
    public void TinyNetMovementReadsSteady() =>
        Assert.Equal(Trend.Steady, One(Step(new PropertyChange("mass", 50, 51, PropertyChangeKind.OnChannelTransfer))).Trend);

    [Fact]
    public void StructuralBlendsReadDrifting() =>
        Assert.Equal(Trend.Drifting, One(Step(new PropertyChange("conductivity", 55, 51, PropertyChangeKind.StructuralBlend))).Trend);

    /// <summary>Two steps fold into one movement: first Before, last After.</summary>
    [Fact]
    public void StepsFoldFirstToLast()
    {
        var movement = One(
            Step(new PropertyChange("heat", 10, 30, PropertyChangeKind.OnChannelTransfer)),
            Step(new PropertyChange("heat", 30, 55, PropertyChangeKind.OnChannelTransfer)));

        Assert.Equal(10, movement.Initial);
        Assert.Equal(55, movement.Final);
        Assert.Equal(Trend.Rising, movement.Trend);
        Assert.Equal(PropertyTier.Trace, movement.TierBefore);
        Assert.Equal(PropertyTier.Moderate, movement.TierAfter);
    }

    /// <summary>Derived-resistance drops are §2.2 bookkeeping, not something the player caused —
    /// they never appear as movements.</summary>
    [Fact]
    public void DerivedResistanceDropsAreExcluded() =>
        Assert.Empty(Trends.Aggregate(new[]
        {
            Step(new PropertyChange("heat_resistance", 60, 0, PropertyChangeKind.DerivedResistance)),
        }));

    [Fact]
    public void MovementsOrderLargestFirstThenById()
    {
        var movements = Trends.Aggregate(new[]
        {
            Step(
                new PropertyChange("mass", 50, 55, PropertyChangeKind.OnChannelTransfer),
                new PropertyChange("heat", 0, 40, PropertyChangeKind.OnChannelTransfer),
                new PropertyChange("cold", 40, 0, PropertyChangeKind.Pruned)),
        });

        Assert.Equal(new[] { "cold", "heat", "mass" }, movements.Select(m => m.Property));
    }
}
