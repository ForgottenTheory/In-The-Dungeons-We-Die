namespace Dungeons.Professions;

/// <summary>
/// Persistent runtime progression for one profession: total XP (from which level is
/// derived) plus per-activity mastery. Death never removes this (docs/progression.md §3).
/// </summary>
public sealed class ProfessionProgress
{
    private readonly Dictionary<string, int> _masteryByActionId = new(StringComparer.Ordinal);

    public ProfessionProgress(string professionId, long xp = 0)
    {
        if (string.IsNullOrWhiteSpace(professionId))
            throw new ArgumentException("Profession id is null or empty.", nameof(professionId));
        if (xp < 0)
            throw new ArgumentOutOfRangeException(nameof(xp), xp, "XP cannot be negative.");
        ProfessionId = professionId;
        Xp = xp;
    }

    public string ProfessionId { get; }
    public long Xp { get; private set; }
    public int Level => ProfessionLeveling.LevelForXp(Xp);

    public void AddXp(long amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "XP gain cannot be negative.");
        Xp += amount;
    }

    public int GetMastery(string actionId) => _masteryByActionId.TryGetValue(actionId, out var m) ? m : 0;

    /// <summary>Read-only view of all activity mastery, for persistence.</summary>
    public IReadOnlyDictionary<string, int> Masteries => _masteryByActionId;

    public void AddMastery(string actionId, int amount)
    {
        if (amount <= 0)
            return;
        _masteryByActionId[actionId] = GetMastery(actionId) + amount;
    }

    // --- UI/display helpers -------------------------------------------------

    /// <summary>XP already banked toward the current level.</summary>
    public long XpIntoCurrentLevel => Xp - ProfessionLeveling.XpForLevel(Level);

    /// <summary>XP span between the current level and the next (0 at max level).</summary>
    public long XpForNextLevel =>
        Level >= ProfessionLeveling.MaxLevel
            ? 0
            : ProfessionLeveling.XpForLevel(Level + 1) - ProfessionLeveling.XpForLevel(Level);

    /// <summary>Progress toward the next level in [0, 1] (1 at max level).</summary>
    public double ProgressToNextLevel => XpForNextLevel == 0 ? 1.0 : (double)XpIntoCurrentLevel / XpForNextLevel;
}
