using Dungeons.Combat;

namespace Dungeons.Items;

/// <summary>
/// Where a piece of equipment is worn. One entry per body location the player fills, which is
/// what makes a "full loadout" a real thing rather than two slots wearing the word.
///
/// <para><b>These member names are persisted</b> — they are the keys of
/// <c>SaveData.Equipment</c> and the <c>slot</c> field of every <c>equipment/</c> and
/// <c>forms/</c> definition (docs/code-map.md §12). Adding a member is free; renaming one needs
/// the content and a save migration in the same commit, which is exactly what
/// <see cref="EquipmentSlots.LegacyBodySlotName"/> records.</para>
/// </summary>
public enum EquipmentSlot
{
    Weapon,
    Offhand,
    Head,
    Body,
    Hands,
    Feet,
    Trinket,
}

/// <summary>Facts about slots that would otherwise be re-derived at each call site.</summary>
public static class EquipmentSlots
{
    /// <summary>
    /// The slot name written by save schemas v1–v8, when the body slot was called <c>Armor</c>.
    /// Renamed in v9 because the expansion to seven slots made it actively wrong — a helm is
    /// armour too. <see cref="Dungeons.Persistence.SaveMapper"/> maps it on load; nothing else
    /// should ever need to know it existed.
    /// </summary>
    public const string LegacyBodySlotName = "Armor";

    /// <summary>
    /// Slots whose worn item mitigates damage. A trinket is worn but is not armour, and a weapon
    /// is held rather than worn — so this is stated rather than inferred from "not a weapon",
    /// which would quietly turn every charm into a breastplate.
    /// </summary>
    public static readonly IReadOnlySet<EquipmentSlot> ArmorBearing =
        new HashSet<EquipmentSlot> { EquipmentSlot.Offhand, EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Hands, EquipmentSlot.Feet };

    public static bool GrantsArmor(EquipmentSlot slot) => ArmorBearing.Contains(slot);

    /// <summary>Every slot, in the order a character sheet reads: what you fight with, then
    /// head to foot, then what you carry.</summary>
    public static readonly IReadOnlyList<EquipmentSlot> DisplayOrder = new[]
    {
        EquipmentSlot.Weapon, EquipmentSlot.Offhand, EquipmentSlot.Head,
        EquipmentSlot.Body, EquipmentSlot.Hands, EquipmentSlot.Feet, EquipmentSlot.Trinket,
    };
}

/// <summary>Armor base stats (before instance-property derivation).</summary>
public sealed class ArmorStats
{
    public double Armor { get; init; }
    public Dictionary<string, double> Resistances { get; init; } = new();
}

/// <summary>
/// Data-driven equipment blueprint (a non-stackable <see cref="IItemDefinition"/>).
/// A specific worn item is always an <see cref="ItemInstance"/>; the definition holds
/// the base stats that the instance's derived properties adjust (docs/itemization.md §3).
/// </summary>
public sealed class EquipmentDefinition : IItemDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public EquipmentSlot Slot { get; init; } = EquipmentSlot.Weapon;

    /// <summary>
    /// The moves this item grants while worn (E4, docs/moves.md §5.1). <b>Weapon-granted moves
    /// are mandatory, not optional</b> — the Fighter's entire identity is "moveset comes from
    /// the weapon; reconfigures by re-equipping". This replaces the old <c>WeaponStats</c>
    /// block: a weapon's numbers now live on its moves, adjusted per instance by
    /// <c>EquipmentResolver</c>.
    /// </summary>
    public IReadOnlyList<MoveGrantSpec> Moves { get; init; } = Array.Empty<MoveGrantSpec>();

    /// <summary>Move modifiers granted while worn, by id.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("move_modifiers")]
    public IReadOnlyList<string> MoveModifierIds { get; init; } = Array.Empty<string>();

    public ArmorStats? Armor { get; init; }

    /// <summary>Traits expressed through the fabrication trait expression (C2a §16.3). Empty on
    /// authored gear.</summary>
    public IReadOnlyList<Crafting.TraitInstance> ExpressedTraits { get; init; } = Array.Empty<Crafting.TraitInstance>();

    /// <summary>Traits the trait expression or cap held back — kept for value, flavour, and future
    /// refabrication (§16.2's dormancy rule).</summary>
    public IReadOnlyList<Crafting.TraitInstance> DormantTraits { get; init; } = Array.Empty<Crafting.TraitInstance>();

    /// <summary>Mass-share-weighted, arcane-amplified essence (§16.3 step 5).</summary>
    public IReadOnlyDictionary<string, double> Essence { get; init; } = new Dictionary<string, double>();

    /// <summary>Intrinsic material-style properties, as a name→value map.</summary>
    public Dictionary<string, double> Properties { get; init; } = new();

    /// <summary>
    /// The broad category, for inventory routing only. Everything that is not held as a weapon
    /// routes as <see cref="ItemType.Armor"/> — including a trinket, which is not armour but is
    /// carried, listed and equipped exactly like one. Whether a piece actually <em>mitigates</em>
    /// is <see cref="EquipmentSlots.GrantsArmor"/>'s question, never this one's.
    /// </summary>
    public ItemType ItemType => Slot == EquipmentSlot.Weapon ? ItemType.Weapon : ItemType.Armor;
    public bool Stackable => false;
    public PropertySet BaseProperties => new(Properties);
}
