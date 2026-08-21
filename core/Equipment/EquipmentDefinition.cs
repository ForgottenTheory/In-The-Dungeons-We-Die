using Dungeons.Combat;

namespace Dungeons.Items;

/// <summary>
/// Where a piece of equipment is worn. One entry per body location the player fills, which is
/// what makes a "full loadout" a real thing rather than two slots wearing the word.
///
/// <para><b>These member names are persisted</b> — they are the keys of
/// <c>SaveData.Equipment</c> and the <c>slot</c> field of every <c>equipment/</c> and
/// <c>forms/</c> definition (docs/code-map.md §12). Adding a member is free; renaming one needs
/// the content and a save migration in the same commit (the v9 Armor→Body rename was exactly
/// that, retired with D54's item reset).</para>
///
/// <para>The two rings were appended, and appending really is free: slots persist <b>by name</b>
/// (<c>SaveMapper</c> writes <c>slot.ToString()</c>), so a save written before they existed
/// simply carries no ring keys and loads as a character wearing no rings — which is what a
/// character who has never owned one is. No migration, no schema bump.</para>
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
    Ring1,
    Ring2,
}

/// <summary>Facts about slots that would otherwise be re-derived at each call site.</summary>
public static class EquipmentSlots
{
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
        EquipmentSlot.Body, EquipmentSlot.Hands, EquipmentSlot.Feet,
        EquipmentSlot.Trinket, EquipmentSlot.Ring1, EquipmentSlot.Ring2,
    };

    /// <summary>
    /// The ring positions, in the order they fill. Rings are the one case where <em>which</em>
    /// position a piece takes is not decided by the piece: a definition must name a single slot,
    /// so every ring names <see cref="EquipmentSlot.Ring1"/>, but the second ring the player puts
    /// on belongs on the other hand rather than on top of the first.
    /// </summary>
    public static readonly IReadOnlyList<EquipmentSlot> RingPositions =
        new[] { EquipmentSlot.Ring1, EquipmentSlot.Ring2 };

    /// <summary>
    /// Every position a piece declared for <paramref name="declaredSlot"/> may legally occupy —
    /// itself, plus any position it is interchangeable with. Only rings have more than one, and
    /// stating it here is what stops "the player owns two ring slots and can only ever fill one".
    /// </summary>
    public static IReadOnlyList<EquipmentSlot> InterchangeablePositions(EquipmentSlot declaredSlot) =>
        RingPositions.Contains(declaredSlot) ? RingPositions : new[] { declaredSlot };
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

    /// <summary>Intrinsic material-style properties, as a name→value map — the authored
    /// combat-unit channel (<c>mass</c> → damage/windup, <c>hardness</c> → armour) the
    /// resolver reads for hand-authored gear. Identity mints carry an explicit
    /// <c>ItemBaseDelivery</c> on the instance instead and author nothing here.</summary>
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
