namespace Dungeons.Characters;

/// <summary>Notification that the character's level increased.</summary>
public readonly record struct CharacterLevelUp(int OldLevel, int NewLevel);

/// <summary>
/// The character's own persistent progression: total XP, from which the level derives, from
/// which the Base's attribute growth derives.
///
/// <para><b>Realm work only.</b> Nothing in the Hideout feeds this — not professions, not
/// crafting, not discoveries. That is the whole reason the layered progression model survives:
/// if fishing raised combat attributes, every track would collapse into one power number and
/// GDD §4's "no single Character Level represents everything" would be a comment rather than a
/// rule. <c>ProgressionEcosystemTests</c> holds it.</para>
///
/// <para>Survives death, like every other persistent track (GDD §13.1). What a run risks is its
/// unsecured loot, never the levels it earned.</para>
/// </summary>
public sealed class CharacterProgress
{
    public CharacterProgress(long xp = 0)
    {
        if (xp < 0)
            throw new ArgumentOutOfRangeException(nameof(xp), xp, "XP cannot be negative.");
        Xp = xp;
    }

    public long Xp { get; private set; }

    public int Level => CharacterLeveling.LevelForXp(Xp);

    /// <summary>Adds XP and reports the level-up if one happened, so the caller does not have to
    /// remember to read the level before and after.</summary>
    public CharacterLevelUp? AddXp(long amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "XP gain cannot be negative.");
        if (amount == 0)
            return null;

        var before = Level;
        Xp += amount;
        var after = Level;

        return after > before ? new CharacterLevelUp(before, after) : null;
    }

    /// <summary>Replaces the total outright. For loading a save, not for gameplay.</summary>
    public void Restore(long xp)
    {
        Xp = Math.Max(0, xp);
    }

    // --- UI/display helpers -------------------------------------------------

    public long XpIntoCurrentLevel => Xp - CharacterLeveling.XpForLevel(Level);

    public long XpForNextLevel =>
        Level >= CharacterLeveling.MaxLevel
            ? 0
            : CharacterLeveling.XpForLevel(Level + 1) - CharacterLeveling.XpForLevel(Level);

    public double ProgressToNextLevel => XpForNextLevel == 0 ? 1.0 : (double)XpIntoCurrentLevel / XpForNextLevel;
}
