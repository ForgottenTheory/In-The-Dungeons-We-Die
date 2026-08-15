namespace Dungeons.Items;

/// <summary>
/// A bag holding both <b>stacks</b> (quantity-keyed stackable items — raw materials,
/// consumables) and <b>instances</b> (unique <see cref="ItemInstance"/>s — equipment
/// and generated materials). Stack transactions are all-or-nothing. Used for both the
/// Stash and a run's unsecured inventory (docs/architecture.md §23, docs/itemization.md §4).
/// </summary>
public sealed class Inventory
{
    private readonly Dictionary<string, int> _quantities = new(StringComparer.Ordinal);
    private readonly List<ItemInstance> _instances = new();

    /// <summary>Raised after any change to contents (stacks or instances).</summary>
    public event Action? Changed;

    public int GetQuantity(string itemId) => _quantities.TryGetValue(itemId, out var qty) ? qty : 0;

    public bool Contains(string itemId, int quantity = 1) => GetQuantity(itemId) >= quantity;

    public void Add(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("Item id is null or empty.", nameof(itemId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");

        _quantities[itemId] = GetQuantity(itemId) + quantity;
        Changed?.Invoke();
    }

    public void Add(ItemStack stack) => Add(stack.ItemId, stack.Quantity);

    /// <summary>Removes items if enough are present. Returns false and changes nothing otherwise.</summary>
    public bool TryRemove(string itemId, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
        var current = GetQuantity(itemId);
        if (current < quantity)
            return false;

        var remaining = current - quantity;
        if (remaining == 0)
            _quantities.Remove(itemId);
        else
            _quantities[itemId] = remaining;

        Changed?.Invoke();
        return true;
    }

    /// <summary>True if every stack could be removed together.</summary>
    public bool CanRemoveAll(IEnumerable<ItemStack> stacks)
    {
        ArgumentNullException.ThrowIfNull(stacks);
        // Aggregate in case the same id appears more than once.
        var needed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var stack in stacks)
            needed[stack.ItemId] = (needed.TryGetValue(stack.ItemId, out var n) ? n : 0) + stack.Quantity;

        return needed.All(pair => GetQuantity(pair.Key) >= pair.Value);
    }

    /// <summary>Removes every stack atomically. Returns false and changes nothing if any is short.</summary>
    public bool TryRemoveAll(IReadOnlyCollection<ItemStack> stacks)
    {
        if (!CanRemoveAll(stacks))
            return false;
        foreach (var stack in stacks)
            TryRemove(stack.ItemId, stack.Quantity);
        return true;
    }

    /// <summary>Snapshot of stackable contents.</summary>
    public IReadOnlyList<ItemStack> Snapshot() =>
        _quantities.Select(pair => new ItemStack(pair.Key, pair.Value)).ToList();

    // --- Unique instances ---------------------------------------------------

    /// <summary>Snapshot of unique item instances.</summary>
    public IReadOnlyList<ItemInstance> Instances => _instances.ToList();

    public int InstanceCount => _instances.Count;

    public void AddInstance(ItemInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances.Add(instance);
        Changed?.Invoke();
    }

    public ItemInstance? GetInstance(long instanceId) =>
        _instances.FirstOrDefault(i => i.InstanceId == instanceId);

    /// <summary>Removes an instance by id. Returns the removed instance, or null if absent.</summary>
    public ItemInstance? RemoveInstance(long instanceId)
    {
        var index = _instances.FindIndex(i => i.InstanceId == instanceId);
        if (index < 0)
            return null;
        var removed = _instances[index];
        _instances.RemoveAt(index);
        Changed?.Invoke();
        return removed;
    }

    public void Clear()
    {
        if (_quantities.Count == 0 && _instances.Count == 0)
            return;
        _quantities.Clear();
        _instances.Clear();
        Changed?.Invoke();
    }
}
