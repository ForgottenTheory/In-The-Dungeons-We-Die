namespace Dungeons.Items;

/// <summary>
/// A specific owned item — every piece of equipment the identity forge mints. Since
/// migration Phase 7 (D54) an instance carries only the identity model: its sentences, its
/// stakes and its base delivery. Authored gear needs no instance at all.
/// </summary>
public sealed class ItemInstance
{
    public required long InstanceId { get; init; }

    /// <summary>The definition this instance derives its identity from.</summary>
    public required string BaseDefinitionId { get; init; }

    public required ItemType ItemType { get; init; }

    /// <summary>Generated display name, e.g. "Vital Oakbound Iron Longsword".</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Definition ids of the materials this instance was made from.</summary>
    public IReadOnlyList<string> Provenance { get; init; } = Array.Empty<string>();

    // ---- The identity model (migration Phase 3, D50/D51 — save v13) -------------------------

    /// <summary>The mundane physical floor (D46), computed from base reads at the mint and
    /// consumed by the equipment resolver. Null on non-identity gear.</summary>
    public Crafting.Identity.ItemBaseDelivery? BaseDelivery { get; init; }

    /// <summary>Every effect sentence the mint crystallized — floor, generated, signature,
    /// drawback — recompiled into live grants deterministically whenever worn (D50).</summary>
    public IReadOnlyList<Crafting.Identity.ItemEffectSentence> IdentitySentences { get; init; } =
        Array.Empty<Crafting.Identity.ItemEffectSentence>();

    /// <summary>The identities this item actively expresses (D51).</summary>
    public IReadOnlyList<Crafting.Identity.IdentityStake> ExpressedIdentities { get; init; } =
        Array.Empty<Crafting.Identity.IdentityStake>();

    /// <summary>Identities beyond the form's cap: recorded, inert, never deleted — the
    /// reforge/awaken hooks of the future (D51).</summary>
    public IReadOnlyList<Crafting.Identity.IdentityStake> DormantIdentities { get; init; } =
        Array.Empty<Crafting.Identity.IdentityStake>();

    public bool IsEquipment => ItemType is ItemType.Weapon or ItemType.Armor;
}
