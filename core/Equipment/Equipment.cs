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
