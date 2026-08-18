namespace Dungeons.Realms;

/// <summary>
/// Realm progression tuning — how much Realm Knowledge each action grants. Kept in Core
/// (not authored as magic numbers in the client) so the values are one place, documented,
/// and testable. Knowledge is a raw per-realm counter today; these are its increments.
/// </summary>
public static class RealmTuning
{
    public const int KnowledgePerEnter = 1;
    public const int KnowledgePerTravel = 1;
    public const int KnowledgePerEvent = 1;
    public const int KnowledgePerCombatCleared = 2;
    public const int KnowledgePerDescend = 2;
    public const int KnowledgePerExtract = 3;

    /// <summary>
    /// The active-play performance score credited for working a gathering node inside a Realm.
    /// Not 1.0 and not 0: doing the work while something is hunting you is active play, but it
    /// is not the focused, well-timed attempt a Hideout minigame rewards. Provisional — it sits
    /// with the rest of the parked balance backlog.
    /// </summary>
    public const double RealmGatherPerformance = 0.5;
}
