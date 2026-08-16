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

/// <summary>Serializable form of an <see cref="ItemInstance"/> (PropertySet flattened to a map).</summary>
public sealed class ItemInstanceSave
{
    public long InstanceId { get; init; }
    public string BaseDefinitionId { get; init; } = string.Empty;
    public ItemType ItemType { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public ItemQuality Quality { get; init; } = ItemQuality.Normal;
    public Dictionary<string, double> Properties { get; init; } = new();
    public List<string> Provenance { get; init; } = new();
    public List<string> Traits { get; init; } = new();
}

/// <summary>One ancestral root and its share, flattened for the save.</summary>
public sealed class LineageRootSave
{
    public string RootId { get; init; } = string.Empty;
    public double Weight { get; init; }
}

/// <summary>
/// Serializable form of an emergent material archetype (docs/emergent-item-system.md §12.4).
///
/// <para>This is the one place the save stores something definition-shaped, and it is not an
/// exception to the "ids, never definitions" rule so much as a consequence of it: an emergent
/// archetype <i>has</i> no authored definition to refer back to. It is a deterministic cache
/// — the same signature always regenerates the same content — so losing it would cost nothing
/// but the names, and the codex that records what the player discovered stays separate (P6).</para>
/// </summary>
public sealed class EmergentArchetypeSave
{
    /// <summary>The canonical signature, which is also the material's id.</summary>
    public string Signature { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
    public Dictionary<string, double> Properties { get; init; } = new();
    public int Potency { get; init; }
    public int Integrity { get; init; }
    public int Generation { get; init; } = 1;

    /// <summary>The process that produced it.</summary>
    public string ProcessId { get; init; } = string.Empty;

    public List<LineageRootSave> Roots { get; init; } = new();

    /// <summary>One level of parent links only — the full tree is walked through the registry.</summary>
    public List<string> ParentSignatures { get; init; } = new();
}

/// <summary>
/// Versioned root of a save file. Stores ids and runtime values only — never
/// definitions (docs/architecture.md §27–28, docs/json-schema.md §22). Persistent
/// progression (professions, realm knowledge, discoveries) survives death; only
/// unsecured run loot is transient and is not saved.
/// </summary>
public sealed class SaveData
{
    /// <summary>
    /// The schema version a brand-new save is written with.
    ///
    /// <para>v4 added <see cref="EmergentArchetypes"/>. A v3 save still loads: the new field
    /// simply arrives empty, and any archetype a stack refers to is regenerated the next time
    /// that state is reached. No migration step is needed.</para>
    ///
    /// <para>v5 added <see cref="LearnedMoves"/> (M2′ technique acquisition). Same rule: an
    /// older save loads with an empty learned list.</para>
    /// </summary>
    public const int CurrentSchemaVersion = 5;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public long SavedAtTick { get; init; }

    /// <summary>The four ids the character is composed from; null if none created yet.</summary>
    public CharacterBuild? Build { get; init; }

    /// <summary>Persistent, secured stackable inventory.</summary>
    public List<ItemStack> Stash { get; init; } = new();

    /// <summary>Persistent, secured unique item instances (generated materials, gear).</summary>
    public List<ItemInstanceSave> StashInstances { get; init; } = new();

    /// <summary>Equipped item instances, keyed by slot name.</summary>
    public Dictionary<string, ItemInstanceSave> Equipment { get; init; } = new();

    /// <summary>Next item-instance id to issue, so loaded ids never collide.</summary>
    public long NextInstanceId { get; init; } = 1;

    public List<ProfessionSave> Professions { get; init; } = new();

    /// <summary>Per-realm knowledge, keyed by realm id.</summary>
    public Dictionary<string, int> RealmKnowledge { get; init; } = new();

    /// <summary>Discovered crafting-interaction ids.</summary>
    public List<string> Discoveries { get; init; } = new();

    /// <summary>Emergent material archetypes this save has produced (§12.4).</summary>
    public List<EmergentArchetypeSave> EmergentArchetypes { get; init; } = new();

    /// <summary>Move ids learned from technique items, in learn order (M2′ acquisition).</summary>
    public List<string> LearnedMoves { get; init; } = new();
}
