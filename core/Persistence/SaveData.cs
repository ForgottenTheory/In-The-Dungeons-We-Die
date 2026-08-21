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

/// <summary>A crop in the ground when the game was closed (v7).</summary>
public sealed class FarmingPlotSave
{
    public int Index { get; init; }
    public string ActionId { get; init; } = string.Empty;
    public long ReadyAtTick { get; init; }
}

/// <summary>One obstacle fitted to the Agility training course (v7).</summary>
public sealed class TrainingCourseSlotSave
{
    public string Slot { get; init; } = string.Empty;
    public string ObstacleId { get; init; } = string.Empty;
}

/// <summary>
/// The prepared run loadout (v10): where the player meant to go and what they meant to take.
///
/// <para>Worn equipment is <b>not</b> here — it is already persisted in <see cref="SaveData.Equipment"/>,
/// and a loadout that carried its own copy would give the save two answers to "what is the
/// player wearing". This holds only the two facts that had nowhere else to live.</para>
/// </summary>
public sealed class LoadoutSave
{
    public string? RealmId { get; init; }

    /// <summary>Consumables the player intends to carry in. A declaration, not a reservation —
    /// the items themselves are still counted in <see cref="SaveData.Stash"/>.</summary>
    public List<ItemStack> Packed { get; init; } = new();
}

/// <summary>Serializable form of an <see cref="ItemInstance"/>. Since v14 an instance is the
/// identity model and nothing else — the property/genome/affix fields of v6–v13 are gone,
/// and the serializer simply ignores them in old files (D49/D54: items reset).</summary>
public sealed class ItemInstanceSave
{
    public long InstanceId { get; init; }
    public string BaseDefinitionId { get; init; } = string.Empty;
    public ItemType ItemType { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public List<string> Provenance { get; init; } = new();

    /// <summary>v13: the identity-minted base delivery (D46). Null on non-identity gear.</summary>
    public BaseDeliverySave? BaseDelivery { get; init; }

    /// <summary>v13: every effect sentence the mint crystallized (D50).</summary>
    public List<ItemEffectSentenceSave> IdentitySentences { get; init; } = new();

    /// <summary>v13: expressed and dormant identities (D51).</summary>
    public List<IdentityStakeSave> ExpressedIdentities { get; init; } = new();
    public List<IdentityStakeSave> DormantIdentities { get; init; } = new();
}

/// <summary>Serializable form of an <see cref="Dungeons.Crafting.Identity.ItemBaseDelivery"/> (v13).</summary>
public sealed class BaseDeliverySave
{
    public double DamageBonus { get; init; }
    public int WindupTicks { get; init; }
    public double Armor { get; init; }
}

/// <summary>Serializable form of an <see cref="Dungeons.Crafting.Identity.ItemEffectSentence"/> (v13):
/// stable vocabulary ids plus the rolled numbers — grants recompile from these
/// deterministically, so nothing compiled is stored.</summary>
public sealed class ItemEffectSentenceSave
{
    /// <summary>An <c>ItemEffectCategory</c> enum member name; an unreadable value loads Generated.</summary>
    public string Category { get; init; } = "Generated";

    public string TriggerId { get; init; } = string.Empty;
    public string BehaviorId { get; init; } = string.Empty;
    public string PayloadId { get; init; } = string.Empty;
    public double Magnitude { get; init; }
    public double Chance { get; init; } = 1.0;
    public bool AfflictsWearer { get; init; }
}

/// <summary>One identity carried by a persisted identity-model material (v12).</summary>
public sealed class IdentityStakeSave
{
    public string Id { get; init; } = string.Empty;
    public int Rank { get; init; } = 1;
}

/// <summary>One provenance root of a persisted identity-model material (v12).</summary>
public sealed class IdentityRootSave
{
    public string DefinitionId { get; init; } = string.Empty;
    public double Weight { get; init; }
}

/// <summary>
/// An emergent material minted by the identity bench (v12, D42): the fingerprint id plus the
/// full eight-facet state — a regenerable cache, like every emergent registration. Stability
/// is not stored: it derives from identity count vs capacity, always.
/// </summary>
public sealed class IdentityArchetypeSave
{
    /// <summary>The fingerprint, which is also the material's id.</summary>
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
    public List<IdentityStakeSave> Identities { get; init; } = new();
    public List<string> Latent { get; init; } = new();
    public int Capacity { get; init; } = 1;

    /// <summary>A <c>Condition</c> enum member name; an unreadable value loads Pristine.</summary>
    public string Condition { get; init; } = "Pristine";

    public int Quality { get; init; }
    public bool IsCarrier { get; init; }
    public List<IdentityRootSave> Roots { get; init; } = new();
}

/// <summary>
/// Versioned root of a save file. Stores ids and runtime values only — never
/// definitions (docs/architecture.md §27–28). Persistent progression (professions, realm
/// knowledge, discoveries) survives death; only unsecured run loot is transient and is not
/// saved.
/// </summary>
public sealed class SaveData
{
    /// <summary>
    /// The schema version a brand-new save is written with.
    ///
    /// <para>History: v4 emergent archetypes · v5 learned moves · v6 genome+affixes ·
    /// v7 offline (passive action, plots, course, timestamps) · v8 gold · v9 the Armor→Body
    /// slot rename (the first real migration; <c>SaveMapper.TryReadSlot</c> still honors it) ·
    /// v10 the loadout · v11 character XP · v12 identity archetypes · v13 identity-minted
    /// item fields.</para>
    ///
    /// <para><b>v14 — the identity system stands alone (Phase 7, D49/D54).</b> The
    /// property-model sections die with the old crafting system: instances lose their
    /// property map, quality, traits, genome and rolled affixes; the property-model emergent
    /// archetype list is gone. There is no faithful mapping from property-shaped items to the
    /// identity model, so loading any pre-v14 save keeps every progression section exactly
    /// (professions and mastery, realm knowledge, character XP, learned moves, discoveries,
    /// plots, the course, gold) and drops every item section (stash stacks and instances,
    /// worn equipment, both emergent registries, the packed loadout) — <b>progression
    /// survives, items reset</b>. The starter-kit rule re-equips on first load.</para>
    /// </summary>
    public const int CurrentSchemaVersion = 14;

    /// <summary>The first version whose item sections survive a load (D49/D54).</summary>
    public const int ItemsSurviveVersion = 14;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public long SavedAtTick { get; init; }

    /// <summary>Wall-clock time the save was written, as Unix seconds. Ticks alone cannot
    /// measure an absence: the simulation clock stops when the game does (v7).</summary>
    public long SavedAtUnixSeconds { get; init; }

    /// <summary>The passive action left running when the game closed, if any. Offline
    /// progress pays this one out on return (v7).</summary>
    public string? PassiveActionId { get; init; }

    /// <summary>Crops in the ground (v7).</summary>
    public List<FarmingPlotSave> FarmingPlots { get; init; } = new();

    /// <summary>The Agility training course's fitted obstacles (v7).</summary>
    public List<TrainingCourseSlotSave> TrainingCourse { get; init; } = new();

    /// <summary>The prepared run loadout (v10). Null for a player who has never prepared.</summary>
    public LoadoutSave? Loadout { get; init; }

    /// <summary>The four ids the character is composed from; null if none created yet.</summary>
    public CharacterBuild? Build { get; init; }

    /// <summary>
    /// The character's own XP (v11), earned in Realms and never in the Hideout. The level and
    /// every attribute point that follows from it derive from this one number.
    /// </summary>
    public long CharacterXp { get; init; }

    /// <summary>Persistent, secured stackable inventory.</summary>
    public List<ItemStack> Stash { get; init; } = new();

    /// <summary>Persistent, secured unique item instances (identity-minted gear).</summary>
    public List<ItemInstanceSave> StashInstances { get; init; } = new();

    /// <summary>Secured coin (v8). Unsecured coin carried inside a Realm is transient, exactly
    /// like unsecured stacks, and is not saved.</summary>
    public long Gold { get; init; }

    /// <summary>Equipped item instances, keyed by slot name.</summary>
    public Dictionary<string, ItemInstanceSave> Equipment { get; init; } = new();

    /// <summary>Next item-instance id to issue, so loaded ids never collide.</summary>
    public long NextInstanceId { get; init; } = 1;

    public List<ProfessionSave> Professions { get; init; } = new();

    /// <summary>Per-realm knowledge, keyed by realm id.</summary>
    public Dictionary<string, int> RealmKnowledge { get; init; } = new();

    /// <summary>Discovered crafting-interaction ids.</summary>
    public List<string> Discoveries { get; init; } = new();

    /// <summary>Identity-model emergent materials (v12) — minted by the verb bench.</summary>
    public List<IdentityArchetypeSave> IdentityArchetypes { get; init; } = new();

    /// <summary>Move ids learned from technique items, in learn order (M2′ acquisition).</summary>
    public List<string> LearnedMoves { get; init; } = new();

    /// <summary>Derived equipment definitions minted by the identity forge. Without them, a
    /// minted item in the stash points at a definition that no longer exists after load.</summary>
    public List<EquipmentArchetypeSave> EmergentEquipment { get; init; } = new();
}

/// <summary>Serializable form of a forge-derived <c>EquipmentDefinition</c>. Since v14 a
/// derived definition is minimal — name, slot, tags, moves; delivery and effects live on the
/// instance (D46/D50), so nothing else exists to persist.</summary>
public sealed class EquipmentArchetypeSave
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slot { get; init; } = "Weapon";
    public List<string> Tags { get; init; } = new();
    public List<string> MoveIds { get; init; } = new();
}
