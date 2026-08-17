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

    /// <summary>v6: the genome, computed once at fabrication and never recomputed
    /// (docs/affixes.md §2.1). Null for pre-affix and authored gear.</summary>
    public GenomeSave? Genome { get; init; }

    /// <summary>v6: innates + rolled modifiers, in display order.</summary>
    public List<RolledAffixSave> Affixes { get; init; } = new();
}

/// <summary>Serializable form of a <see cref="Dungeons.Crafting.Genome"/>.</summary>
public sealed class GenomeSave
{
    public string FormId { get; init; } = string.Empty;
    public Dictionary<string, double> Pressure { get; init; } = new();
    public Dictionary<string, double> Essence { get; init; } = new();
    public List<TraitInstanceSave> Expressed { get; init; } = new();
    public List<TraitInstanceSave> Dormant { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public int Potency { get; init; }
    public int GenerationDepth { get; init; }
    public List<string> Signatures { get; init; } = new();
}

public sealed class TraitInstanceSave
{
    public string Id { get; init; } = string.Empty;
    public double Magnitude { get; init; }
}

public sealed class RolledAffixSave
{
    public string AffixId { get; init; } = string.Empty;
    public int Tier { get; init; }
    public double Roll { get; init; }
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

    /// <summary>Named traits (C1a), id → magnitude. Absent on pre-C1 saves — loads empty.</summary>
    public Dictionary<string, double> Traits { get; init; } = new();

    /// <summary>The essence vector (C1b), bare keys. Absent on pre-C1 saves — loads empty.</summary>
    public Dictionary<string, double> Essence { get; init; } = new();
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
    ///
    /// <para>v6 added the Genome and rolled affixes to <see cref="ItemInstanceSave"/> (R4b,
    /// D30). Older instances load with a null genome and no affixes — pre-affix gear stays
    /// plain rather than being retroactively rolled.</para>
    /// </summary>
    public const int CurrentSchemaVersion = 6;

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

    /// <summary>Derived equipment definitions minted by fabrication (C2a). Like emergent
    /// material archetypes: without them, a fabricated item in the stash points at a
    /// definition that no longer exists after load.</summary>
    public List<EquipmentArchetypeSave> EmergentEquipment { get; init; } = new();
}

/// <summary>Serializable form of a fabrication-derived <c>EquipmentDefinition</c> (C2a).</summary>
public sealed class EquipmentArchetypeSave
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slot { get; init; } = "Weapon";
    public List<string> Tags { get; init; } = new();
    public List<string> MoveIds { get; init; } = new();
    public double ArmorValue { get; init; }
    public Dictionary<string, double> ArmorResistances { get; init; } = new();
    public Dictionary<string, double> Properties { get; init; } = new();
    public Dictionary<string, double> ExpressedTraits { get; init; } = new();
    public Dictionary<string, double> DormantTraits { get; init; } = new();
    public Dictionary<string, double> Essence { get; init; } = new();
    public bool HasArmor { get; init; }
}
