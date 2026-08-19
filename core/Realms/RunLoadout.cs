using Dungeons.Items;

namespace Dungeons.Realms;

/// <summary>
/// What the party has decided to take on the next expedition: <b>where they are going</b> and
/// <b>what they pack</b>. Survives between runs, so preparing once is preparation, not a chore
/// repeated at every portal.
///
/// <para><b>Worn equipment is deliberately not stored here.</b> The <see cref="Equipment"/>
/// container already <em>is</em> the gear half of a loadout — it is authoritative, it already
/// persists, and combat already resolves from it. Copying it into a second structure would mean
/// two answers to "what is the player wearing", and the moment those disagree the bug is
/// invisible. So the preparation screen edits the real equipment through the normal equip path,
/// and this type holds only what genuinely had nowhere else to live.</para>
///
/// <para>Packed consumables are a <em>declaration</em>, not a reservation: the items stay in the
/// Stash until the run actually starts. Nothing is held hostage by a plan the player made and
/// forgot about, and spending a salve at the bench can never leave the pack in a broken state —
/// see <see cref="LoadoutCheck.PackableFrom"/> for what happens when the plan outruns the
/// shelf.</para>
/// </summary>
public sealed class RunLoadout
{
    private readonly Dictionary<string, int> _packedConsumables = new(StringComparer.Ordinal);

    /// <summary>Raised after any change to the destination or the pack.</summary>
    public event Action? Changed;

    /// <summary>The Realm this loadout is prepared for, or null before the player picks one.</summary>
    public string? RealmId { get; private set; }

    /// <summary>Item id → how many the player intends to carry in.</summary>
    public IReadOnlyDictionary<string, int> PackedConsumables => _packedConsumables;

    public int PackedQuantity(string itemId) =>
        _packedConsumables.TryGetValue(itemId, out var quantity) ? quantity : 0;

    /// <summary>The pack as stacks, in a stable order so the UI and the save never disagree
    /// about what "the same loadout" looks like.</summary>
    public IReadOnlyList<ItemStack> PackedStacks() =>
        _packedConsumables
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new ItemStack(entry.Key, entry.Value))
            .ToList();

    public void SelectRealm(string realmId)
    {
        if (string.IsNullOrWhiteSpace(realmId))
            throw new ArgumentException("Realm id is null or empty.", nameof(realmId));
        if (RealmId == realmId)
            return;

        RealmId = realmId;
        Changed?.Invoke();
    }

    public void Pack(string itemId, int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("Item id is null or empty.", nameof(itemId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");

        _packedConsumables[itemId] = PackedQuantity(itemId) + quantity;
        Changed?.Invoke();
    }

    /// <summary>Removes up to <paramref name="quantity"/> from the pack. Unpacking more than is
    /// packed empties the entry rather than failing — "take it out" always means the same thing
    /// however many times the player clicks it.</summary>
    public void Unpack(string itemId, int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");

        var packed = PackedQuantity(itemId);
        if (packed == 0)
            return;

        var remaining = packed - quantity;
        if (remaining <= 0)
            _packedConsumables.Remove(itemId);
        else
            _packedConsumables[itemId] = remaining;

        Changed?.Invoke();
    }

    /// <summary>
    /// Empties the pack, leaving the destination alone.
    ///
    /// <para>Entering a Realm deliberately does <b>not</b> call this. A loadout is a standing
    /// plan — "I always take three salves" — and a pack that emptied itself every expedition
    /// would make the player rebuild it before every run, which is exactly the chore the
    /// preparation screen exists to remove. Taking more than the Stash holds is already handled
    /// by clamping, so a standing plan degrades gracefully instead of overdrawing.</para>
    /// </summary>
    public void ClearPacked()
    {
        if (_packedConsumables.Count == 0)
            return;

        _packedConsumables.Clear();
        Changed?.Invoke();
    }

    /// <summary>Restores a persisted loadout. For loading a save, not for gameplay.</summary>
    public void Restore(string? realmId, IEnumerable<ItemStack> packed)
    {
        ArgumentNullException.ThrowIfNull(packed);

        RealmId = string.IsNullOrWhiteSpace(realmId) ? null : realmId;
        _packedConsumables.Clear();
        foreach (var stack in packed.Where(stack => stack.Quantity > 0))
            _packedConsumables[stack.ItemId] = PackedQuantity(stack.ItemId) + stack.Quantity;

        Changed?.Invoke();
    }
}
