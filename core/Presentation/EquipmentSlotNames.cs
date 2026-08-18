using Dungeons.Items;

namespace Dungeons.Presentation;

/// <summary>
/// Player-facing names for the places a piece of equipment is worn.
///
/// <para><see cref="EquipmentSlot"/>'s member names are persistent identifiers — save keys and
/// content fields (D32) — and they are chosen to be unambiguous in data, not to read as English.
/// <c>Ring1</c> is the clearest case. So the one-way presentation rule (D30) applies to slots
/// exactly as it does to properties: the save and the content say <c>Ring1</c>, and the player
/// never does.</para>
/// </summary>
public static class EquipmentSlotNames
{
    /// <summary>
    /// What <b>kind</b> of place this is — for an item that may not be worn yet.
    ///
    /// <para>Both ring positions read simply "ring". A ring sitting in the stash has no position:
    /// it takes one at equip time, and every ring definition declares <c>Ring1</c> regardless of
    /// where it will end up. Printing "Ring I" on its own item card would state a fact that is
    /// not true yet — and would be flatly wrong for the ring that ends up in the other hand.</para>
    /// </summary>
    public static string CategoryOf(EquipmentSlot slot) =>
        EquipmentSlots.RingPositions.Contains(slot)
            ? "ring"
            : slot.ToString().ToLowerInvariant();

    /// <summary>
    /// <b>Which</b> position this is — for the character sheet, where the two rings sit in a list
    /// and the player has to be able to tell which one they are about to take off.
    ///
    /// <para>Roman numerals rather than "left"/"right": the positions are interchangeable, and
    /// naming them for hands would invite someone to give handedness a meaning it does not have
    /// (D33). Numerals say "these are two of the same thing" without implying an order that
    /// matters.</para>
    /// </summary>
    public static string PositionOf(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Ring1 => "Ring I",
        EquipmentSlot.Ring2 => "Ring II",
        _ => slot.ToString(),
    };
}
