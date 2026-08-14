namespace Dungeons.Persistence;

/// <summary>
/// Versioned root of a save file. Milestone 1 keeps the payload intentionally
/// small — persistent gameplay state (characters, stash, professions, realm
/// knowledge, discoveries) is added by later milestones. Only ids and runtime
/// values are stored; definitions are never serialized into saves
/// (see docs/architecture.md §27–28, docs/json-schema.md §22).
/// </summary>
public sealed class SaveData
{
    /// <summary>The schema version a brand-new save is written with.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Schema version this save was written with. Present so future migrations
    /// are possible; migration logic itself is deferred.
    /// </summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Simulation tick captured at save time (placeholder runtime value).</summary>
    public long SavedAtTick { get; init; }

    /// <summary>Placeholder persistent currency, proving runtime values round-trip.</summary>
    public long Coins { get; init; }
}
