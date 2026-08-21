using System.Security.Cryptography;
using System.Text;
using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Randomness;

namespace Dungeons.Crafting.Identity;

/// <summary>One forge click: which form, and which owned material fills each slot.</summary>
public sealed record IdentityFabricationInvocation(
    string FormId,
    IReadOnlyDictionary<string, string> ComponentItemIdsBySlot);

/// <summary>Why the forge refused before composing. Same split as the verb bench: gates are
/// about the player's situation, composition failures about the materials'.</summary>
public enum IdentityFabricationGateFailure
{
    UnknownForm,
    ComponentNotAKnownMaterial,
    ComponentNotOnHand,

    /// <summary>The material has no identity model yet — the coexistence seam, worded at
    /// the gate exactly like the verb bench's SubstrateNotMigrated.</summary>
    ComponentNotMigrated,
}

/// <summary>The pre-commit view: the composed item side and the full effect projection —
/// floor, scored table, odds. One computation with <see cref="IdentityFabricationEngine.Fabricate"/>.</summary>
public sealed record IdentityFabricationPreview(
    IdentityFabricationGateFailure? GateFailure,
    string? GateDetail,
    IdentityComposition? Composition,
    ItemEffectProjection? Effects,
    bool WouldBeFirstOfItsKind);

/// <summary>What the forge produced.</summary>
public sealed record IdentityFabricationResult(
    IdentityFabricationGateFailure? GateFailure,
    string? GateDetail,
    IdentityCompositionFailure CompositionFailure,
    ItemInstance? Item,
    IReadOnlyList<ItemEffectSentence> Sentences,
    bool FirstOfItsKind);

/// <summary>
/// The identity-model mint (migration Phase 3, D50) — the terminal boundary where identity
/// materials become equipment, succeeding <see cref="EquipmentAssemblyEngine"/>'s genome
/// path. Compose (D46/D51) → resolve effects (the item-effect pipeline) → consume → register
/// the derived definition → mint the instance. Preview and Fabricate share every step but
/// the dice and the side effects.
/// </summary>
public sealed class IdentityFabricationEngine
{
    /// <summary>Derived-definition id prefix. Starts with the same <c>equip.emergent.</c>
    /// the old fabrication used so the existing save pipe persists these definitions
    /// unchanged; the <c>i</c> keeps the two id spaces from ever colliding.</summary>
    public const string DerivedEquipmentIdPrefix = "equip.emergent.i";

    private readonly ContentBundle _content;
    private readonly ItemEffectResolver _effects;
    private readonly Func<Inventory> _inventory;
    private readonly InstanceIdSource _instanceIds;
    private readonly IRandomSource _random;

    public IdentityFabricationEngine(
        ContentBundle content,
        Func<Inventory> inventory,
        InstanceIdSource instanceIds,
        IRandomSource random)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _effects = new ItemEffectResolver(content);
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _instanceIds = instanceIds ?? throw new ArgumentNullException(nameof(instanceIds));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public IdentityFabricationPreview Preview(IdentityFabricationInvocation invocation)
    {
        var gate = CheckGates(invocation);
        if (gate.Failure is not null)
            return new IdentityFabricationPreview(gate.Failure, gate.Detail, null, null, false);

        var composition = IdentityEquipmentComposer.Compose(
            gate.Form!, gate.Components!, _content, FormNoun(gate.Form!, DerivedDefinitionId(invocation)));
        if (composition.Failure != IdentityCompositionFailure.None)
            return new IdentityFabricationPreview(null, null, composition, null, false);

        return new IdentityFabricationPreview(
            null, null, composition,
            _effects.Project(composition),
            WouldBeFirstOfItsKind: !_content.Equipment.Contains(DerivedDefinitionId(invocation)));
    }

    public IdentityFabricationResult Fabricate(IdentityFabricationInvocation invocation)
    {
        var gate = CheckGates(invocation);
        if (gate.Failure is { } gateFailure)
            return Refused(gateFailure, gate.Detail);

        var composition = IdentityEquipmentComposer.Compose(
            gate.Form!, gate.Components!, _content, FormNoun(gate.Form!, DerivedDefinitionId(invocation)));
        if (composition.Failure != IdentityCompositionFailure.None)
        {
            return new IdentityFabricationResult(
                null, null, composition.Failure, null, Array.Empty<ItemEffectSentence>(), false);
        }

        var resolution = _effects.Resolve(composition, _random);

        foreach (var componentItemId in invocation.ComponentItemIdsBySlot.Values)
            _inventory().TryRemove(componentItemId, 1);

        // The derived definition is shared by every mint of the same components in the same
        // form (the definitions stack; the instances differ by their sentences) — the same
        // definition-vs-instance split the old fabrication proved.
        var definitionId = DerivedDefinitionId(invocation);
        var isFirst = !_content.Equipment.Contains(definitionId);
        if (isFirst)
        {
            _content.Equipment.Add(new EquipmentDefinition
            {
                Id = definitionId,
                Name = composition.Name,
                Slot = gate.Form!.Type,
                Tags = gate.Form.Tags,
                Moves = gate.Form.Moves,
            });
        }

        var item = new ItemInstance
        {
            InstanceId = _instanceIds.Next(),
            BaseDefinitionId = definitionId,
            ItemType = gate.Form!.Type == EquipmentSlot.Weapon ? ItemType.Weapon : ItemType.Armor,
            DisplayName = composition.Name,
            Provenance = invocation.ComponentItemIdsBySlot.Values.Distinct(StringComparer.Ordinal).ToList(),
            BaseDelivery = composition.BaseDelivery,
            IdentitySentences = resolution.Sentences,
            ExpressedIdentities = composition.Expressed,
            DormantIdentities = composition.Dormant,
        };
        _inventory().AddInstance(item);

        return new IdentityFabricationResult(
            null, null, IdentityCompositionFailure.None, item, resolution.Sentences, isFirst);
    }

    // --- Gates ---------------------------------------------------------------

    private sealed record GateCheck(
        IdentityFabricationGateFailure? Failure, string? Detail,
        EquipmentBlueprintDefinition? Form = null,
        IReadOnlyDictionary<string, (MaterialDefinition Definition, IdentityMaterialState State)>? Components = null);

    private GateCheck CheckGates(IdentityFabricationInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        if (!_content.Forms.TryGetById(invocation.FormId, out var form))
            return new GateCheck(IdentityFabricationGateFailure.UnknownForm, $"Unknown form '{invocation.FormId}'.");

        var components = new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>(StringComparer.Ordinal);
        foreach (var (slotName, componentItemId) in invocation.ComponentItemIdsBySlot)
        {
            if (!_content.Materials.TryGetById(componentItemId, out var definition))
                return new GateCheck(IdentityFabricationGateFailure.ComponentNotAKnownMaterial,
                    $"'{componentItemId}' is not a known material.");

            if (!_inventory().Contains(componentItemId, 1))
                return new GateCheck(IdentityFabricationGateFailure.ComponentNotOnHand,
                    $"No {definition.Name} on hand.");

            var state = IdentityStateResolver.StateOf(definition);
            if (state is null)
            {
                return new GateCheck(IdentityFabricationGateFailure.ComponentNotMigrated,
                    $"{definition.Name} has no identity model yet — the identity forge cannot work it.");
            }

            components[slotName] = (definition, state);
        }

        return new GateCheck(null, null, form, components);
    }

    /// <summary>How many trailing hex characters of the derived id seed the noun pick —
    /// eight fits <c>uint</c> and is already well distributed.</summary>
    private const int NounHashHexLength = 8;

    /// <summary>
    /// The form's noun, which may be one of its <c>name_variants</c> (D34's ~120 weapon
    /// names). Chosen from the derived definition id rather than the RNG, because that id
    /// already means "this exact item kind": the same materials in the same form always read
    /// the same way — two identical blades are never a Falchion and a Scimitar — and the
    /// preview promises the noun the forge will actually mint. The old fabrication's rule,
    /// carried across in Phase 7 (D54) so the name library survived its engine.
    /// </summary>
    public static string FormNoun(EquipmentBlueprintDefinition form, string derivedDefinitionId)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(derivedDefinitionId);

        if (form.NameVariants.Count == 0)
            return form.Name;

        var options = new List<string>(form.NameVariants.Count + 1) { form.Name };
        options.AddRange(form.NameVariants);

        var hash = Convert.ToUInt32(derivedDefinitionId[^NounHashHexLength..], 16);
        return options[(int)(hash % (uint)options.Count)];
    }

    /// <summary>The derived definition's identity: the form plus what filled each slot —
    /// exactly the facts two mints must share to be the same kind of item.</summary>
    public static string DerivedDefinitionId(IdentityFabricationInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var canonical = new StringBuilder(invocation.FormId);
        foreach (var (slotName, componentItemId) in invocation.ComponentItemIdsBySlot
            .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            canonical.Append('|').Append(slotName).Append('=').Append(componentItemId);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return DerivedEquipmentIdPrefix + Convert.ToHexString(hash)[..Fingerprint.HashHexLength].ToLowerInvariant();
    }

    private static IdentityFabricationResult Refused(IdentityFabricationGateFailure failure, string? detail) =>
        new(failure, detail, IdentityCompositionFailure.None, null, Array.Empty<ItemEffectSentence>(), false);
}
