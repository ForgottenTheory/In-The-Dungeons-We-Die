using Dungeons.Content;

namespace Dungeons.Professions;

/// <summary>
/// The five stations of a Hideout training course. Fixed vocabulary, so it is an enum and
/// not content: adding a slot changes what a course <em>is</em>, and every obstacle in the
/// game would have to be re-sorted (docs/code-map.md §5).
/// </summary>
public enum TrainingSlot
{
    Balance,
    Climbing,
    Endurance,
    Recovery,
    Advanced,
}

/// <summary>
/// One obstacle that can be fitted into a course slot. Running the course grants Agility XP;
/// the obstacles the player chose to fit also grant their <see cref="Bonuses"/> for as long
/// as they stay fitted — which is what makes the configuration a decision rather than a
/// cosmetic layout.
/// </summary>
public sealed class TrainingObstacleDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public TrainingSlot Slot { get; init; } = TrainingSlot.Balance;
    public int RequiredLevel { get; init; } = 1;

    /// <summary>Ticks this obstacle adds to one lap of the course.</summary>
    public int IntervalTicks { get; init; } = 100;

    /// <summary>Agility XP this obstacle contributes to one lap.</summary>
    public long Experience { get; init; }

    /// <summary>Player-facing description of what fitting this obstacle does.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Persistent utility this obstacle grants while fitted, keyed by a course-bonus name
    /// (see <see cref="CourseBonusKeys"/>). String-keyed for the same reason material
    /// properties are: a new bonus should be content, not a new field.
    /// </summary>
    public IReadOnlyDictionary<string, double> Bonuses { get; init; } = new Dictionary<string, double>();
}

/// <summary>
/// The bonus names a fitted obstacle may grant. Held here rather than scattered through
/// content so a typo in a JSON key fails validation instead of silently granting nothing.
/// </summary>
public static class CourseBonusKeys
{
    /// <summary>Fraction taken off the time cost of moving between Realm locations.</summary>
    public const string RealmTravelSpeed = "course.realm_travel_speed";

    /// <summary>Fraction taken off gathering intervals in the field.</summary>
    public const string GatheringSpeed = "course.gathering_speed";

    /// <summary>Fraction taken off the time an extraction interaction costs.</summary>
    public const string ExtractionSpeed = "course.extraction_speed";

    /// <summary>Added chance to avoid a Realm hazard outright.</summary>
    public const string HazardAvoidance = "course.hazard_avoidance";

    /// <summary>Added chance that pursuing an opportunity survives its risk roll.</summary>
    public const string OpportunitySafety = "course.opportunity_safety";

    public static readonly IReadOnlyList<string> All = new[]
    {
        RealmTravelSpeed,
        GatheringSpeed,
        ExtractionSpeed,
        HazardAvoidance,
        OpportunitySafety,
    };
}

/// <summary>Why an obstacle could not be fitted.</summary>
public enum CourseFitFailure
{
    None,
    UnknownObstacle,
    WrongSlot,
    LevelTooLow,
}

/// <summary>
/// The player's configured training course: at most one obstacle per <see cref="TrainingSlot"/>.
///
/// <para>Running a lap is the Agility XP faucet. The reason the course is configurable rather
/// than a single "Train Agility" button is that the fitted set also <em>is</em> the player's
/// standing utility loadout — choosing the climbing wall over the endurance run is choosing
/// faster Realm travel over safer hazards. That is Agility's whole active decision, and it is
/// made once and lived with, not clicked (docs/professions.md §7).</para>
/// </summary>
public sealed class TrainingCourse
{
    public const string AgilityProfessionId = "profession.agility";

    private readonly DataStore<TrainingObstacleDefinition> _obstacles;
    private readonly ProfessionSystem _professions;
    private readonly Dictionary<TrainingSlot, string> _fitted = new();

    public TrainingCourse(DataStore<TrainingObstacleDefinition> obstacles, ProfessionSystem professions)
    {
        _obstacles = obstacles ?? throw new ArgumentNullException(nameof(obstacles));
        _professions = professions ?? throw new ArgumentNullException(nameof(professions));
    }

    /// <summary>Raised after a lap completes, with the XP it granted.</summary>
    public event Action<long>? LapCompleted;

    public IReadOnlyDictionary<TrainingSlot, string> Fitted => _fitted;

    public bool IsEmpty => _fitted.Count == 0;

    public int AgilityLevel => _professions.GetProgress(AgilityProfessionId).Level;

    /// <summary>Obstacles the player may fit into <paramref name="slot"/> at their level.</summary>
    public IReadOnlyList<TrainingObstacleDefinition> AvailableFor(TrainingSlot slot)
    {
        var level = AgilityLevel;
        return _obstacles.GetAll()
            .Where(obstacle => obstacle.Slot == slot && obstacle.RequiredLevel <= level)
            .OrderBy(obstacle => obstacle.RequiredLevel)
            .ThenBy(obstacle => obstacle.Id, StringComparer.Ordinal)
            .ToList();
    }

    public CourseFitFailure Fit(TrainingSlot slot, string obstacleId)
    {
        if (!_obstacles.TryGetById(obstacleId, out var obstacle))
            return CourseFitFailure.UnknownObstacle;
        if (obstacle.Slot != slot)
            return CourseFitFailure.WrongSlot;
        if (obstacle.RequiredLevel > AgilityLevel)
            return CourseFitFailure.LevelTooLow;

        _fitted[slot] = obstacleId;
        return CourseFitFailure.None;
    }

    public void Clear(TrainingSlot slot) => _fitted.Remove(slot);

    /// <summary>The obstacles currently fitted, in slot order.</summary>
    public IReadOnlyList<TrainingObstacleDefinition> FittedObstacles() =>
        Enum.GetValues<TrainingSlot>()
            .Where(slot => _fitted.ContainsKey(slot))
            .Select(slot => _obstacles.GetById(_fitted[slot]))
            .ToList();

    /// <summary>Ticks one full lap takes — the sum of the fitted obstacles.</summary>
    public int LapIntervalTicks() => FittedObstacles().Sum(obstacle => obstacle.IntervalTicks);

    /// <summary>Agility XP one full lap grants.</summary>
    public long LapExperience() => FittedObstacles().Sum(obstacle => obstacle.Experience);

    /// <summary>
    /// Every fitted obstacle's utility, summed per bonus key. This is what the rest of the
    /// game reads; nothing else needs to know a course exists.
    /// </summary>
    public IReadOnlyDictionary<string, double> ActiveBonuses()
    {
        var totals = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var obstacle in FittedObstacles())
        {
            foreach (var bonus in obstacle.Bonuses)
                totals[bonus.Key] = totals.GetValueOrDefault(bonus.Key) + bonus.Value;
        }

        return totals;
    }

    public double BonusValue(string bonusKey) => ActiveBonuses().GetValueOrDefault(bonusKey);

    /// <summary>Runs one lap, granting its Agility XP. Returns the XP granted (0 for an
    /// empty course, which is the honest answer to running nothing).</summary>
    public long RunLap()
    {
        var xp = LapExperience();
        if (xp <= 0)
            return 0;

        _professions.GetProgress(AgilityProfessionId).AddXp(xp);
        LapCompleted?.Invoke(xp);
        return xp;
    }

    /// <summary>Replaces the fitted set (used when loading a save).</summary>
    public void Restore(IEnumerable<(TrainingSlot Slot, string ObstacleId)> fitted)
    {
        ArgumentNullException.ThrowIfNull(fitted);
        _fitted.Clear();
        foreach (var entry in fitted)
        {
            // Silently drop obstacles that no longer exist in content, rather than refusing
            // to load the save.
            if (_obstacles.TryGetById(entry.ObstacleId, out var obstacle) && obstacle.Slot == entry.Slot)
                _fitted[entry.Slot] = entry.ObstacleId;
        }
    }
}
