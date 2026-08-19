using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Content;
using Xunit;
using Xunit.Abstractions;

namespace Dungeons.Tests.Characters;

/// <summary>
/// Character XP and the attribute growth it finally switches on.
///
/// <para><c>AttributeGrowth</c>, the 4.0-point budget rule and <c>ResolvedBuild.GrowthAt</c> were
/// all built long before Phase 8 and never called with a real level — the character was
/// permanently level 1 on a uniform-5 baseline. These tests hold what changed and, more
/// importantly, <b>what must not</b>: the growth budget is still identical across Bases, and
/// nothing outside a Realm feeds this track.</para>
/// </summary>
public class CharacterProgressionTests
{
    private readonly ITestOutputHelper _output;

    public CharacterProgressionTests(ITestOutputHelper output) => _output = output;

    private static BuildResolver ShippedBuilds() => new(new ContentBundle
    {
        Classes = TestPaths.LoadStore<BaseClassDefinition>("classes"),
        Prefixes = TestPaths.LoadStore<PrefixDefinition>("prefixes"),
        Suffixes = TestPaths.LoadStore<SuffixDefinition>("suffixes"),
        NameFormats = TestPaths.LoadStore<NameFormatDefinition>("name_formats"),
        ModifierKeys = TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
    });

    // --- The curve ----------------------------------------------------------

    [Fact]
    public void ANewCharacterIsLevelOneWithNoXp()
    {
        var progress = new CharacterProgress();

        Assert.Equal(0, progress.Xp);
        Assert.Equal(1, progress.Level);
    }

    [Fact]
    public void XpRaisesTheLevelAndReportsTheCrossing()
    {
        var progress = new CharacterProgress();

        Assert.Null(progress.AddXp(CharacterLeveling.XpForLevel(2) - 1));
        Assert.Equal(1, progress.Level);

        var levelUp = progress.AddXp(1);

        Assert.NotNull(levelUp);
        Assert.Equal(1, levelUp!.Value.OldLevel);
        Assert.Equal(2, levelUp.Value.NewLevel);
    }

    [Fact]
    public void TheCurveIsMonotonicAndCapped()
    {
        for (var level = 2; level <= CharacterLeveling.MaxLevel; level++)
            Assert.True(CharacterLeveling.XpForLevel(level) > CharacterLeveling.XpForLevel(level - 1));

        Assert.Equal(CharacterLeveling.MaxLevel, CharacterLeveling.LevelForXp(long.MaxValue / 4));
    }

    // --- What a Realm pays --------------------------------------------------

    /// <summary>A boss is worth a boss. Composed identity means the fold already decided how much
    /// creature is standing there, so XP reads it rather than restating it per actor.</summary>
    [Fact]
    public void ABossIsWorthMoreThanTheSameCreatureWouldBeUnranked()
    {
        var ordinary = CharacterLeveling.XpForDefeating(100, EnemyRank.Normal);
        var elite = CharacterLeveling.XpForDefeating(100, EnemyRank.Elite);
        var boss = CharacterLeveling.XpForDefeating(100, EnemyRank.Boss);

        Assert.True(elite > ordinary);
        Assert.True(boss > elite);
    }

    [Fact]
    public void EvenTheFeeblestThingIsWorthSomething()
    {
        Assert.True(CharacterLeveling.XpForDefeating(1, EnemyRank.Normal) >= 1);
        Assert.True(CharacterLeveling.XpForDefeating(0, EnemyRank.Normal) >= 1);
    }

    // --- Growth -------------------------------------------------------------

    /// <summary>
    /// <b>The rule that must survive every progression pass:</b> every Base distributes the same
    /// total per level, so Base choice stays a trade rather than a menu with bigger options.
    /// Levelling makes that budget matter for the first time, which is exactly when it becomes
    /// worth re-asserting.
    /// </summary>
    [Fact]
    public void EveryBaseStillDistributesTheSameGrowthBudget()
    {
        var classes = TestPaths.LoadStore<BaseClassDefinition>("classes");

        foreach (var baseClass in classes.GetAll())
        {
            var perLevel = AttributeGrowth.PerLevel(baseClass.Growth);
            Assert.Equal(AttributeGrowth.BudgetPerLevel, perLevel.Values.Sum(), 6);
        }
    }

    [Fact]
    public void GrowthAppliesTheBasesOwnShapeAndNothingAtLevelOne()
    {
        var build = ShippedBuilds().Resolve(new CharacterBuild(
            new SpeciesId("species.human"), new BaseClassId("class.wizard"),
            new PrefixId("prefix.galvanic"), new SuffixId("suffix.unreasonable_confidence")));

        Assert.All(build.GrowthAt(1).Values, points => Assert.Equal(0, points));

        var atTwenty = build.GrowthAt(20);
        Assert.True(atTwenty.Values.Sum() > 0);

        // The wizard's own weights decide the shape — a wizard grows Intelligence faster than
        // Strength, and that is the Base doing its job rather than this test asserting a number.
        Assert.True(atTwenty[AttributeType.Intelligence] > atTwenty[AttributeType.Strength]);
    }

    [Fact]
    public void GrowthAccumulatesWithTheLevel()
    {
        var build = ShippedBuilds().Resolve(new CharacterBuild(
            new SpeciesId("species.human"), new BaseClassId("class.wizard"),
            new PrefixId("prefix.galvanic"), new SuffixId("suffix.unreasonable_confidence")));

        var ten = build.GrowthAt(10).Values.Sum();
        var twenty = build.GrowthAt(20).Values.Sum();

        Assert.True(twenty > ten);
    }

    [Fact]
    public void RenderTheCharacterCurve()
    {
        foreach (var level in new[] { 1, 2, 5, 10, 20, 40, 99 })
            _output.WriteLine($"level {level,3}: {CharacterLeveling.XpForLevel(level),9} XP cumulative");

        _output.WriteLine($"one 30 HP raider = {CharacterLeveling.XpForDefeating(30, EnemyRank.Normal)} XP");
        _output.WriteLine($"one 290 HP boss  = {CharacterLeveling.XpForDefeating(290, EnemyRank.Boss)} XP");
        _output.WriteLine($"extracting       = {CharacterLeveling.XpForExtracting} XP");
    }
}
