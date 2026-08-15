using Dungeons.Professions;
using Xunit;

namespace Dungeons.Tests.Professions;

public class ProfessionTuningTests
{
    [Theory]
    [InlineData(0.5, 1.0)]   // dead centre = perfect
    [InlineData(0.0, 0.0)]   // edges = zero
    [InlineData(1.0, 0.0)]
    [InlineData(0.25, 0.5)]  // halfway to the edge
    [InlineData(0.75, 0.5)]
    public void TimingPerformance_PeaksAtCentre_AndFallsToEdges(double position, double expected)
    {
        Assert.Equal(expected, ProfessionTuning.TimingPerformance(position), 3);
    }
}
