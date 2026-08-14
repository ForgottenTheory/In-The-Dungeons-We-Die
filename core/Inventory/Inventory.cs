namespace Dungeons.Items;

/// <summary>
/// A simple quantity-keyed bag of stackable items. Milestone 3 uses it as the home
/// for profession outputs and inputs; capacity limits, item instances and the
/// stash/loadout/realm split arrive with later milestones (docs/architecture.md §23).
/// Transactions are all-or-nothing: a removal that cannot be satisfied changes nothing.
/// </summary>
public sealed class Inventory
{
    private readonly Dictionary<string, int> _quantities = new(StringComparer.Ordinal);

    /// <summary>Raised after any change to contents.</summary>
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

    /// <summary>Snapshot of current contents.</summary>
    public IReadOnlyList<ItemStack> Snapshot() =>
        _quantities.Select(pair => new ItemStack(pair.Key, pair.Value)).ToList();

    public void Clear()
    {
        if (_quantities.Count == 0)
            return;
        _quantities.Clear();
        Changed?.Invoke();
    }
}
