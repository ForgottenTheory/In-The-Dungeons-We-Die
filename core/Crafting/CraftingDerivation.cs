using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>
/// Derives the properties of a generated item from its input materials. This is the
/// <b>single seam</b> the emergent property-based reaction simulation will replace
/// (docs/crafting.md §17): bonding/affinity, property transfer, opposing-property
/// resolution, reactions, thresholds, instability/mutation, etc.
/// <para>
/// The current implementation is deliberately trivial — an additive merge of the
/// inputs' properties — enough to prove that crafted instances carry properties
/// derived from their parents and can be crafted again recursively.
/// </para>
/// </summary>
public static class CraftingDerivation
{
    public static PropertySet Derive(IReadOnlyList<PropertySet> inputProperties)
    {
        ArgumentNullException.ThrowIfNull(inputProperties);
        var result = PropertySet.Empty;
        foreach (var input in inputProperties)
            result = result.Combine(input, (a, b) => a + b);
        return result;
    }
}
