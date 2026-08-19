using Dungeons.Combat;

namespace Dungeons.Characters;

/// <summary>
/// The character's own XP → level curve, and what Realm work is worth.
///
/// <para><b>Deliberately the same shape as <c>ProfessionLeveling</c></b> — cumulative
/// <c>rate·(L-1)·L/2</c>, capped at 99 — so the game has one idea of what a level is rather than
/// three. Only the rate differs, because character XP is scarce where profession XP is
/// constant.</para>
///
/// <para><b>Every number here is a first pass and nobody has played it.</b> The shape is the
/// decision; the magnitudes belong to the parked balance backlog.</para>
/// </summary>
public static class CharacterLeveling
{
    public const int MinLevel = 1;
    public const int MaxLevel = 99;

    /// <summary>XP the step from level L to L+1 costs, per level. Ten times a profession's,
    /// because a profession advances on every swing and a character advances on expeditions.</summary>
    public const long XpPerLevelStep = 50;

    /// <summary>
    /// Character XP for defeating something, from the health it had. Health rather than a
    /// hand-authored per-actor number: 481 actors ship, and an XP field on each would be 481
    /// chances to forget one — the composed family+role+actor fold already decides how much
    /// creature is standing there.
    /// </summary>
    public const double XpPerEnemyHealth = 0.25;

    /// <summary>What surviving with the loot is worth. Paid on extraction only — dying pays
    /// nothing, which is the extraction decision applied to progression itself.</summary>
    public const long XpForExtracting = 25;

    /// <summary>Cumulative XP required to be exactly <paramref name="level"/>.</summary>
    public static long XpForLevel(int level)
    {
        if (level <= MinLevel)
            return 0;
        var capped = Math.Min(level, MaxLevel);
        return XpPerLevelStep * (capped - 1) * capped / 2;
    }

    public static int LevelForXp(long xp)
    {
        if (xp < 0)
            throw new ArgumentOutOfRangeException(nameof(xp), xp, "XP cannot be negative.");

        var level = MinLevel;
        while (level < MaxLevel && XpForLevel(level + 1) <= xp)
            level++;
        return level;
    }

    /// <summary>
    /// What one defeated enemy is worth. An elite and a boss are worth more than their health
    /// alone says, because the thing that makes them hard is not only how long they last.
    /// </summary>
    public static long XpForDefeating(int enemyMaxHealth, EnemyRank rank)
    {
        var fromHealth = Math.Max(1.0, enemyMaxHealth * XpPerEnemyHealth);
        var forRank = rank switch
        {
            EnemyRank.Elite => 1.5,
            EnemyRank.Boss => 2.0,
            _ => 1.0,
        };

        return (long)Math.Round(fromHealth * forRank);
    }
}
