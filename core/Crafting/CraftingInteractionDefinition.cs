using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>A minimum profession level required to perform a crafting interaction.</summary>
public sealed class ProfessionRequirement
{
    public string ProfessionId { get; init; } = string.Empty;
    public int Level { get; init; } = 1;
}

/// <summary>
/// A data-driven crafting interaction — often hidden until discovered through
/// experimentation. Combining the input materials, given the required profession
/// knowledge, yields the result and records a discovery. This is the vehicle for
/// cross-profession recipes such as Barkbound Iron (docs/json-schema.md §11,
/// docs/crafting.md §5–6).
/// </summary>
public sealed class CraftingInteractionDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<ItemStack> Inputs { get; init; } = Array.Empty<ItemStack>();
    public IReadOnlyList<ProfessionRequirement> ProfessionRequirements { get; init; } = Array.Empty<ProfessionRequirement>();

    public string ResultItemId { get; init; } = string.Empty;
    public int ResultQuantity { get; init; } = 1;

    /// <summary>Persistent id recorded when this interaction is first discovered.</summary>
    public string DiscoveryId { get; init; } = string.Empty;
}
