namespace Dungeons.Loot;

/// <summary>
/// The loot system's own constants. There are only two, and both are safety rails rather than
/// balance knobs — every number that shapes what actually drops lives in the tables themselves,
/// which is the point of making loot data.
/// </summary>
public static class LootTuning
{
    /// <summary>
    /// How many levels of nested table one roll may descend. Content validation rejects cycles
    /// outright, so this only ever fires on a table graph deep enough to be a mistake; it exists
    /// so a bad edit degrades into missing loot rather than a stack overflow mid-fight.
    /// </summary>
    public const int MaxNestingDepth = 8;

    /// <summary>Ceiling on picks from a single weighted draw. A draw asking for hundreds of
    /// picks is a typo, not a design.</summary>
    public const int MaxPicksPerDraw = 32;
}
