using Dungeons.Items;
using Dungeons.Professions;
using Xunit;
using static Dungeons.Tests.Professions.ProfessionsTestData;

namespace Dungeons.Tests.Professions;

/// <summary>
/// The Agility training course. Its whole design claim is that the configuration is the
/// decision: what you fit is simultaneously your XP rate and your standing utility loadout, so
/// choosing the climbing wall over the endurance run is choosing gathering speed over travel
/// speed and living with it.
/// </summary>
public class TrainingCourseTests
{
    private static TrainingObstacleDefinition Obstacle(
        string id,
        TrainingSlot slot,
        int requiredLevel = 1,
        int intervalTicks = 100,
        long experience = 12,
        params (string Key, double Value)[] bonuses) => new()
    {
        Id = id,
        Name = id,
        Slot = slot,
        RequiredLevel = requiredLevel,
        IntervalTicks = intervalTicks,
        Experience = experience,
        Bonuses = bonuses.ToDictionary(b => b.Key, b => b.Value),
    };

    private static (TrainingCourse Course, ProfessionSystem System) Hideout(params TrainingObstacleDefinition[] obstacles)
    {
        var system = new ProfessionSystem(Store<ProfessionActionDefinition>(), new Inventory(), new FakeRandom());
        return (new TrainingCourse(Store(obstacles), system), system);
    }

    private static readonly TrainingObstacleDefinition BeamWalk =
        Obstacle("obstacle.beam_walk", TrainingSlot.Balance, bonuses: (CourseBonusKeys.RealmTravelSpeed, 0.03));

    private static readonly TrainingObstacleDefinition KnottedRope =
        Obstacle("obstacle.knotted_rope", TrainingSlot.Climbing, bonuses: (CourseBonusKeys.GatheringSpeed, 0.03));

    [Fact]
    public void FittingAnObstacleGrantsItsBonus()
    {
        var (course, _) = Hideout(BeamWalk);

        Assert.Equal(CourseFitFailure.None, course.Fit(TrainingSlot.Balance, BeamWalk.Id));
        Assert.Equal(0.03, course.BonusValue(CourseBonusKeys.RealmTravelSpeed), 6);
    }

    [Fact]
    public void BonusesFromEveryFittedObstacleSum()
    {
        var climbAndTravel = Obstacle("obstacle.rope_bridge", TrainingSlot.Balance, requiredLevel: 1,
            bonuses: (CourseBonusKeys.RealmTravelSpeed, 0.06));

        var (course, _) = Hideout(climbAndTravel, KnottedRope);
        course.Fit(TrainingSlot.Balance, climbAndTravel.Id);
        course.Fit(TrainingSlot.Climbing, KnottedRope.Id);

        Assert.Equal(0.06, course.BonusValue(CourseBonusKeys.RealmTravelSpeed), 6);
        Assert.Equal(0.03, course.BonusValue(CourseBonusKeys.GatheringSpeed), 6);
    }

    /// <summary>One obstacle per slot: fitting a second replaces the first rather than
    /// stacking, which is what makes the choice cost something.</summary>
    [Fact]
    public void ASecondObstacleInTheSameSlotReplacesTheFirst()
    {
        var stronger = Obstacle("obstacle.spinning_log", TrainingSlot.Balance,
            bonuses: (CourseBonusKeys.RealmTravelSpeed, 0.10));

        var (course, _) = Hideout(BeamWalk, stronger);
        course.Fit(TrainingSlot.Balance, BeamWalk.Id);
        course.Fit(TrainingSlot.Balance, stronger.Id);

        Assert.Single(course.Fitted);
        Assert.Equal(0.10, course.BonusValue(CourseBonusKeys.RealmTravelSpeed), 6);
    }

    [Fact]
    public void AnObstacleCannotBeFittedIntoTheWrongSlot()
    {
        var (course, _) = Hideout(BeamWalk);

        Assert.Equal(CourseFitFailure.WrongSlot, course.Fit(TrainingSlot.Climbing, BeamWalk.Id));
        Assert.True(course.IsEmpty);
    }

    [Fact]
    public void AnObstacleAboveTheAgilityLevelIsRefused()
    {
        var advanced = Obstacle("obstacle.blind_drop", TrainingSlot.Advanced, requiredLevel: 65,
            bonuses: (CourseBonusKeys.HazardAvoidance, 0.12));
        var (course, system) = Hideout(advanced);

        Assert.Equal(CourseFitFailure.LevelTooLow, course.Fit(TrainingSlot.Advanced, advanced.Id));

        system.GetProgress(TrainingCourse.AgilityProfessionId).AddXp(ProfessionLeveling.XpForLevel(65));
        Assert.Equal(CourseFitFailure.None, course.Fit(TrainingSlot.Advanced, advanced.Id));
    }

    [Fact]
    public void ALapGrantsTheSumOfTheFittedObstaclesXp()
    {
        var (course, system) = Hideout(BeamWalk, KnottedRope);
        course.Fit(TrainingSlot.Balance, BeamWalk.Id);
        course.Fit(TrainingSlot.Climbing, KnottedRope.Id);

        Assert.Equal(24, course.RunLap());
        Assert.Equal(24, system.GetProgress(TrainingCourse.AgilityProfessionId).Xp);
        Assert.Equal(200, course.LapIntervalTicks());
    }

    [Fact]
    public void AnEmptyCourseGrantsNothing()
    {
        var (course, system) = Hideout(BeamWalk);

        Assert.Equal(0, course.RunLap());
        Assert.Equal(0, system.GetProgress(TrainingCourse.AgilityProfessionId).Xp);
    }

    [Fact]
    public void ClearingASlotRemovesItsBonus()
    {
        var (course, _) = Hideout(BeamWalk);
        course.Fit(TrainingSlot.Balance, BeamWalk.Id);
        course.Clear(TrainingSlot.Balance);

        Assert.True(course.IsEmpty);
        Assert.Equal(0.0, course.BonusValue(CourseBonusKeys.RealmTravelSpeed), 6);
    }

    [Fact]
    public void AvailableObstaclesRespectSlotAndLevel()
    {
        var advanced = Obstacle("obstacle.blind_drop", TrainingSlot.Advanced, requiredLevel: 65,
            bonuses: (CourseBonusKeys.HazardAvoidance, 0.12));
        var (course, _) = Hideout(BeamWalk, KnottedRope, advanced);

        Assert.Equal(new[] { BeamWalk.Id }, course.AvailableFor(TrainingSlot.Balance).Select(o => o.Id));
        Assert.Empty(course.AvailableFor(TrainingSlot.Advanced));
    }

    [Fact]
    public void RestoreDropsObstaclesContentNoLongerHas()
    {
        var (course, _) = Hideout(BeamWalk);

        course.Restore(new[]
        {
            (TrainingSlot.Balance, BeamWalk.Id),
            (TrainingSlot.Climbing, "obstacle.deleted_from_content"),
        });

        Assert.Single(course.Fitted);
        Assert.Equal(BeamWalk.Id, course.Fitted[TrainingSlot.Balance]);
    }
}
