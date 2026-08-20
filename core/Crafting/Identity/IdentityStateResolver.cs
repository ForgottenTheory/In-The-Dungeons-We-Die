using Dungeons.Content;

namespace Dungeons.Crafting.Identity;

/// <summary>
/// Turns an authored <see cref="MaterialDefinition"/> into its starting
/// <see cref="IdentityMaterialState"/> — the identity-model counterpart of the old
/// <see cref="MaterialStateResolver"/>, living beside it during the migration (D42).
/// </summary>
public static class IdentityStateResolver
{
    /// <summary>
    /// The definition's starting state, or <b>null for a material that has not been migrated
    /// to the identity model</b> (no authored <see cref="MaterialDefinition.Capacity"/>).
    /// Null is the coexistence seam: the identity engine refuses unmigrated inputs with a
    /// clear failure instead of guessing at a capacity the author never chose.
    /// </summary>
    public static IdentityMaterialState? StateOf(MaterialDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Capacity is not int capacity)
            return null;

        return new IdentityMaterialState
        {
            Identities = definition.Identities
                .Select(grant => new IdentityStake(grant.Id, grant.Rank))
                .ToArray(),
            Latent = definition.Latent.ToArray(),
            Capacity = capacity,
            Condition = Condition.Pristine,
            Quality = IdentityCraftTuning.DefaultQuality,
            IsCarrier = false,
            Roots = new[] { new ProvenanceRoot(definition.Id, 1.0) },
        };
    }
}
