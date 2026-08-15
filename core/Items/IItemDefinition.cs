using Dungeons.Content;

namespace Dungeons.Items;

/// <summary>
/// Shared contract for anything that is an "item" definition (materials, equipment,
/// consumables). Lets inventory, crafting and equipment reason about items uniformly
/// while each concrete type keeps its own extra data (docs/itemization.md §1).
/// </summary>
public interface IItemDefinition : IDefinition
{
    string Name { get; }
    ItemType ItemType { get; }

    /// <summary>Whether identical copies collapse into a quantity stack (true) or are unique instances (false).</summary>
    bool Stackable { get; }

    /// <summary>Intrinsic material properties of this item kind.</summary>
    PropertySet BaseProperties { get; }
}
