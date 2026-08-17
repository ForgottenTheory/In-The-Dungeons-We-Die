using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>
/// Decides what a destroyed material leaves behind (docs/emergent-item-system.md §6.2c).
/// Total by construction — there is always a fallback — because a craft that consumed the
/// player's inputs and returned nothing at all is exactly what stops people experimenting.
/// </summary>
public sealed class ByproductResolver
{
    private readonly DataStore<ByproductDefinition> _byproducts;

    public ByproductResolver(DataStore<ByproductDefinition> byproducts)
    {
        _byproducts = byproducts ?? throw new ArgumentNullException(nameof(byproducts));
    }

    /// <summary>
    /// The byproduct left by destroying <paramref name="quantity"/> units of a material with
    /// <paramref name="tags"/>. Chosen by the first <c>form:</c> tag any byproduct covers, so
    /// an iron ingot (<c>form:metal</c>, <c>form:ingot</c>) yields slag without either tag
    /// being special-cased.
    /// </summary>
    public ItemStack? ByproductFor(IReadOnlyList<string> tags, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (quantity <= 0)
            return null;

        var definition = Match(tags) ?? Fallback();
        return definition is null ? null : new ItemStack(definition.Material, quantity);
    }

    private ByproductDefinition? Match(IReadOnlyList<string> tags)
    {
        foreach (var tag in tags)
        {
            if (!TagFamilies.TryParse(tag, out var family, out var value)
                || !string.Equals(family, TagFamilies.Form.Name, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var byproduct in _byproducts.GetAll())
            {
                if (byproduct.Covers(value))
                    return byproduct;
            }
        }

        return null;
    }

    private ByproductDefinition? Fallback() =>
        _byproducts.GetAll().FirstOrDefault(b => b.Fallback);
}
