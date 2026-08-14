using Dungeons.Professions;
using Xunit;

namespace Dungeons.Tests.Professions;

public class ProfessionProgressionTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 100)]
    [InlineData(3, 300)]
    [InlineData(4, 600)]
    public void XpForLevel_MatchesCurve(int level, long expected)
    {
        Assert.Equal(expected, ProfessionLeveling.XpForLevel(level));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(299, 2)]
    [InlineData(300, 3)]
    public void LevelForXp_MatchesCurve(long xp, int expected)
    {
        Assert.Equal(expected, ProfessionLeveling.LevelForXp(xp));
    }

    [Fact]
    public void LevelForXp_CapsAtMax()
    {
        Assert.Equal(ProfessionLeveling.MaxLevel, ProfessionLeveling.LevelForXp(long.MaxValue / 2));
    }

    [Fact]
    public void Progress_TracksXpAndLevel()
    {
        var p = new ProfessionProgress("profession.forestry");
        Assert.Equal(1, p.Level);

        p.AddXp(100);
        Assert.Equal(2, p.Level);
        Assert.Equal(0, p.XpIntoCurrentLevel);       // exactly at level 2
        Assert.Equal(200, p.XpForNextLevel);         // 300 - 100
        Assert.Equal(0.0, p.ProgressToNextLevel);

        p.AddXp(100);
        Assert.Equal(0.5, p.ProgressToNextLevel);    // 100 of 200 into level 2
    }

    [Fact]
    public void Mastery_IsPerAction()
    {
        var p = new ProfessionProgress("profession.forestry");
        p.AddMastery("action.chop_oak", 3);
        Assert.Equal(3, p.GetMastery("action.chop_oak"));
        Assert.Equal(0, p.GetMastery("action.chop_pine"));
    }

    [Fact]
    public void Mastery_ReducesIntervalWithFloor()
    {
        Assert.Equal(100, ProfessionTuning.EffectiveIntervalTicks(100, 0));
        Assert.Equal(90, ProfessionTuning.EffectiveIntervalTicks(100, 20));   // -10%
        Assert.Equal(50, ProfessionTuning.EffectiveIntervalTicks(100, 200));  // capped at -50%
        Assert.Equal(ProfessionTuning.MinimumIntervalTicks, ProfessionTuning.EffectiveIntervalTicks(6, 200));
    }
}
