namespace Dungeons.Items;

/// <summary>
/// A character's equipped items, one <see cref="ItemInstance"/> per slot. Equipping
/// returns whatever was displaced so the caller can put it back in the inventory.
/// </summary>
public sealed class Equipment
{
    private readonly Dictionary<EquipmentSlot, ItemInstance> _slots = new();

    public event Action? Changed;

    public ItemInstance? InSlot(EquipmentSlot slot) => _slots.TryGetValue(slot, out var i) ? i : null;

    public IReadOnlyDictionary<EquipmentSlot, ItemInstance> Slots => _slots;

    /// <summary>Equips <paramref name="item"/> in <paramref name="slot"/>; returns the previously equipped item, if any.</summary>
    public ItemInstance? Equip(EquipmentSlot slot, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var previous = InSlot(slot);
        _slots[slot] = item;
        Changed?.Invoke();
        return previous;
    }

    /// <summary>
    /// Equips into the first free position this item may occupy, falling back to the position it
    /// declares once they are all full. For every slot but the rings there is exactly one
    /// position and this is <see cref="Equip"/>; for rings it is the difference between owning a
    /// second ring slot and being able to use it.
    ///
    /// <para>The fallback displaces <see cref="EquipmentSlots.RingPositions"/>'s first entry
    /// rather than picking one at random, so a third ring always evicts the same predictable
    /// one.</para>
    /// </summary>
    /// <returns>The displaced item, if the fallback had to evict one.</returns>
    public ItemInstance? EquipInFirstFreePosition(EquipmentSlot declaredSlot, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var positions = EquipmentSlots.InterchangeablePositions(declaredSlot);
        return Equip(positions.FirstOrDefault(position => InSlot(position) is null, positions[0]), item);
    }

    public ItemInstance? Unequip(EquipmentSlot slot)
    {
        if (!_slots.Remove(slot, out var removed))
            return null;
        Changed?.Invoke();
        return removed;
    }

    public void Clear()
    {
        if (_slots.Count == 0)
            return;
        _slots.Clear();
        Changed?.Invoke();
    }
}
