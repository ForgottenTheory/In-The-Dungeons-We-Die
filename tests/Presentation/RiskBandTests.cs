using Dungeons.Crafting;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>§3's risk ladder — SAFE · COSTLY · STRAINED · PERILOUS · DESTROYS — mapped from
/// the same <see cref="IntegrityProjection"/> the §6.2c guarantees ride on.</summary>
public class RiskBandTests
{
    private static RiskBand Of(double cost, double spread, int projected, double chance) =>
        Risk.Of(new IntegrityProjection(cost, spread, projected, chance));

    [Fact]
    public void CertainDestructionReadsDestroys() => Assert.Equal(RiskBand.Destroys, Of(40, 0, 0, 1.0));

    [Fact]
    public void AnyRealChanceReadsPerilous() => Assert.Equal(RiskBand.Perilous, Of(18, 10, 2, 0.35));

    [Fact]
    public void LowRemainingBudgetReadsStrained() => Assert.Equal(RiskBand.Strained, Of(6, 0, 20, 0));

    [Fact]
    public void AHeavyCostReadsCostly() => Assert.Equal(RiskBand.Costly, Of(12, 0, 60, 0));

    [Fact]
    public void AGentleStepReadsSafe() => Assert.Equal(RiskBand.Safe, Of(3, 0, 90, 0));

    [Fact]
    public void EveryBandHasAWord()
    {
        foreach (var band in Enum.GetValues<RiskBand>())
            Assert.False(string.IsNullOrWhiteSpace(Risk.Word(band)));
    }
}
