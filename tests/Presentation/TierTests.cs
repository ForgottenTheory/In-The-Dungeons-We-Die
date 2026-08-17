using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>The one shared tier function (docs/presentation-architecture.md §2B) — every
/// surface reads through it, so its boundaries are rules.</summary>
public class TierTests
{
    [Theory]
    [InlineData(0, PropertyTier.None)]
    [InlineData(-3, PropertyTier.None)]
    [InlineData(0.5, PropertyTier.Trace)]
    [InlineData(15, PropertyTier.Trace)]
    [InlineData(15.5, PropertyTier.Low)]
    [InlineData(35, PropertyTier.Low)]
    [InlineData(36, PropertyTier.Moderate)]
    [InlineData(60, PropertyTier.Moderate)]
    [InlineData(61, PropertyTier.Strong)]
    [InlineData(85, PropertyTier.Strong)]
    [InlineData(86, PropertyTier.Extreme)]
    [InlineData(100, PropertyTier.Extreme)]
    public void TierBoundariesAreExact(double value, PropertyTier expected) =>
        Assert.Equal(expected, Tiers.Of(value));

    [Fact]
    public void PipsCountTheTierOrdinalOutOfFive()
    {
        Assert.Equal("○○○○○", Tiers.Pips(PropertyTier.None));
        Assert.Equal("●○○○○", Tiers.Pips(PropertyTier.Trace));
        Assert.Equal("●●●○○", Tiers.Pips(PropertyTier.Moderate));
        Assert.Equal("●●●●●", Tiers.Pips(PropertyTier.Extreme));
    }

    [Theory]
    [InlineData(100, "Fresh")]
    [InlineData(90, "Fresh")]
    [InlineData(89, "Sturdy")]
    [InlineData(60, "Sturdy")]
    [InlineData(59, "Worn")]
    [InlineData(30, "Worn")]
    [InlineData(29, "Fragile")]
    [InlineData(1, "Fragile")]
    [InlineData(0, "Destroyed")]
    public void IntegrityReadsAsWearWords(int integrity, string expected) =>
        Assert.Equal(expected, Tiers.WearWord(integrity));
}
