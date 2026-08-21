namespace Dungeons.Items;

/// <summary>
/// A bag holding <b>stacks</b> (quantity-keyed stackable items — raw materials, consumables),
/// <b>instances</b> (unique <see cref="ItemInstance"/>s — equipment and generated materials)
/// and <b>coin</b>. Stack transactions are all-or-nothing. Used for both the Stash and a run's
/// unsecured inventory (docs/architecture.md §23, docs/itemization.md §4).
///
/// <para>Gold lives here rather than on a separate purse for one reason that matters: it makes
/// coin obey the extraction risk model for free. Gold picked up inside a Realm sits in the
/// unsecured run inventory and is lost on death; gold in the Stash is safe — the same rule as
/// every other thing the player carries, with no second code path to keep in step.</para>
/// </summary>
public sealed class Inventory
{
    private readonly Dictionary<string, int> _quantities = new(StringComparer.Ordinal);
    private readonly List<ItemInstance> _instances = new();

    /// <summary>Raised after any change to contents (stacks, instances or coin).</summary>
    public event Action? Changed;

    /// <summary>Coin in this bag. The game's only currency; nothing spends it yet.</summary>
    public long Gold { get; private set; }

    public void AddGold(long amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Gold amount must be positive.");
        Gold += amount;
        Changed?.Invoke();
    }

    /// <summary>Spends coin if there is enough. Returns false and changes nothing otherwise.</summary>
    public bool TrySpendGold(long amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Gold amount must be positive.");
        if (Gold < amount)
            return false;

        Gold -= amount;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Sets coin outright. For loading a save, not for gameplay.</summary>
    public void RestoreGold(long amount)
    {
        Gold = Math.Max(0, amount);
        Changed?.Invoke();
    }

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

    /// <summary>
    /// Adds every stack, raising <see cref="Changed"/> <b>once</b> for the whole batch.
    /// Bulk grants must come through here: each <see cref="Changed"/> triggers a full UI
    /// rebuild upstream, so a per-stack loop over a large batch turns one click into
    /// thousands of rebuilds of a growing bag — quadratic work that freezes the game.
    /// </summary>
    public void AddAll(IEnumerable<ItemStack> stacks)
    {
        ArgumentNullException.ThrowIfNull(stacks);

        // Validate the whole batch before touching contents — stack transactions are
        // all-or-nothing, and a throw halfway through must not leave a partial grant.
        var validatedStacks = stacks.ToList();
        foreach (var stack in validatedStacks)
        {
            if (string.IsNullOrWhiteSpace(stack.ItemId))
                throw new ArgumentException("Item id is null or empty.", nameof(stacks));
            if (stack.Quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(stacks), stack.Quantity, "Quantity must be positive.");
        }

        if (validatedStacks.Count == 0)
            return;

        foreach (var stack in validatedStacks)
            _quantities[stack.ItemId] = GetQuantity(stack.ItemId) + stack.Quantity;
        Changed?.Invoke();
    }

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
        if (_quantities.Count == 0 && _instances.Count == 0 && Gold == 0)
            return;
        _quantities.Clear();
        _instances.Clear();
        Gold = 0;
        Changed?.Invoke();
    }
}
