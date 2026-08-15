namespace Dungeons.Items;

/// <summary>
/// Pure, engine-independent formatting of items for display (inventory, codex, tooltips).
/// Kept in Core so every presentation surface — the debug console today, the emergent-item
/// codex later — renders instances the same way, and so it is unit-testable.
/// </summary>
public static class ItemFormat
{
    /// <summary>"name value, name value …" over a property set (empty string if none).</summary>
    public static string Properties(PropertySet properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return string.Join(", ", properties.AsDictionary().Select(p => $"{p.Key} {p.Value:0.##}"));
    }

    /// <summary>"Display Name #id (prop v, prop v)" — the standard one-line item-instance label.</summary>
    public static string InstanceLabel(ItemInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var props = instance.Properties.Count > 0 ? " (" + Properties(instance.Properties) + ")" : string.Empty;
        return $"{instance.DisplayName} #{instance.InstanceId}{props}";
    }
}
