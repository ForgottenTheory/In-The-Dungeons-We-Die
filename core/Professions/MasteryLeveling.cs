namespace Dungeons.Professions;

/// <summary>
/// Mastery points → mastery level, for one action.
///
/// <para><b>The curve is deliberately linear: one completion, one level, to a ceiling of 99.</b>
/// Every other level track in the game bends (professions cost 100·L per level), and mastery
/// could too — but bending it here would be a <em>balance</em> change smuggled into an
/// integration pass. Linear means today's per-point numbers are exactly today's per-level
/// numbers, so nothing about how an action feels moves in this commit. When the balance pass
/// comes, it changes this one method and the shipped ladder in <c>mastery/</c>, and nothing
/// else in the game needs to know (GDD §7.3).</para>
///
/// <para>Level <b>0</b> is an action never performed. GDD's "1–99" counts the levels that can be
/// gained; a pickaxe you have never swung has not mastered anything.</para>
/// </summary>
public static class MasteryLeveling
{
    public const int MinLevel = 0;
    public const int MaxLevel = 99;

    public static int LevelFor(int masteryPoints) => Math.Clamp(masteryPoints, MinLevel, MaxLevel);

    public static bool IsMastered(int masteryPoints) => LevelFor(masteryPoints) >= MaxLevel;
}
