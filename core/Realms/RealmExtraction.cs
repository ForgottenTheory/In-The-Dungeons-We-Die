using Dungeons.Items;

namespace Dungeons.Realms;

/// <summary>Outcome of ending a run: what was secured to the Stash, or what was lost.</summary>
public sealed class ExtractionSummary
{
    public required bool Secured { get; init; }
    public required IReadOnlyList<ItemStack> Items { get; init; }

    public int TotalQuantity => Items.Sum(i => i.Quantity);
}

/// <summary>
/// Resolves the end of a Realm run (docs/realms.md §5–6, docs/architecture.md §14).
/// Extraction moves unsecured run loot into the persistent Stash; death forfeits it.
/// Either way the run inventory is emptied and the run is ended. Persistent
/// progression (professions, knowledge, discoveries) lives elsewhere and is untouched.
/// </summary>
public static class RealmExtraction
{
    /// <summary>Secures all run loot into <paramref name="stash"/> and ends the run.</summary>
    public static ExtractionSummary Secure(RealmRun run, Inventory stash)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(stash);

        var items = run.RunInventory.Snapshot();
        foreach (var stack in items)
            stash.Add(stack);
        run.RunInventory.Clear();
        run.End();

        return new ExtractionSummary { Secured = true, Items = items };
    }

    /// <summary>Forfeits all unsecured run loot (death) and ends the run. The Stash is untouched.</summary>
    public static ExtractionSummary Forfeit(RealmRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var items = run.RunInventory.Snapshot();
        run.RunInventory.Clear();
        run.End();

        return new ExtractionSummary { Secured = false, Items = items };
    }
}
