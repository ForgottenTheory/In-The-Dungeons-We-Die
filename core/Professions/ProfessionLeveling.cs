namespace Dungeons.Professions;

/// <summary>
/// Deterministic XP → level curve shared by every profession. Placeholder balance:
/// advancing from level L to L+1 costs 100·L XP, so cumulative XP for level L is
/// 100·(L-1)·L/2. Capped at <see cref="MaxLevel"/>.
/// </summary>
public static class ProfessionLeveling
{
    public const int MinLevel = 1;
    public const int MaxLevel = 99;

    /// <summary>Cumulative XP required to be exactly <paramref name="level"/>.</summary>
    public static long XpForLevel(int level)
    {
        if (level <= MinLevel)
            return 0;
        var l = Math.Min(level, MaxLevel);
        return 100L * (l - 1) * l / 2;
    }

    /// <summary>The level a given cumulative XP total corresponds to.</summary>
    public static int LevelForXp(long xp)
    {
        if (xp < 0)
            throw new ArgumentOutOfRangeException(nameof(xp), xp, "XP cannot be negative.");

        var level = MinLevel;
        while (level < MaxLevel && XpForLevel(level + 1) <= xp)
            level++;
        return level;
    }
}
