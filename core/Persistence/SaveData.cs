using Dungeons.Characters.Composition;
using Dungeons.Items;

namespace Dungeons.Persistence;

/// <summary>Persisted progression for one profession: total XP and per-activity mastery.</summary>
public sealed class ProfessionSave
{
    public string ProfessionId { get; init; } = string.Empty;
    public long Xp { get; init; }
    public Dictionary<string, int> Mastery { get; init; } = new();
}

/// <summary>
/// Versioned root of a save file. Stores ids and runtime values only — never
/// definitions (docs/architecture.md §27–28, docs/json-schema.md §22). Persistent
/// progression (professions, realm knowledge, discoveries) survives death; only
/// unsecured run loot is transient and is not saved.
/// </summary>
public sealed class SaveData
{
    /// <summary>The schema version a brand-new save is written with.</summary>
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public long SavedAtTick { get; init; }

    /// <summary>The four ids the character is composed from; null if none created yet.</summary>
    public CharacterBuild? Build { get; init; }

    /// <summary>Persistent, secured inventory.</summary>
    public List<ItemStack> Stash { get; init; } = new();

    public List<ProfessionSave> Professions { get; init; } = new();

    /// <summary>Per-realm knowledge, keyed by realm id.</summary>
    public Dictionary<string, int> RealmKnowledge { get; init; } = new();

    /// <summary>Discovered crafting-interaction ids.</summary>
    public List<string> Discoveries { get; init; } = new();
}
