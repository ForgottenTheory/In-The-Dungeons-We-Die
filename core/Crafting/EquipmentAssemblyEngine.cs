using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

public sealed record EquipmentAssemblyRequest(string BlueprintId, IReadOnlyDictionary<string, string> SlotMaterials);

public enum EquipmentAssemblyFailure
{
    None, UnknownBlueprint, UnknownMaterial, MissingSlot, SlotRejected, MissingInputs,
}

public sealed record EquipmentAssemblyOutcome(
    EquipmentAssemblyFailure Failure,
    ItemInstance? Item,
    string Name,
    IReadOnlyList<TraitInstance> Expressed,
    IReadOnlyList<TraitInstance> Dormant,
    bool IsFirstOfItsKind)
{
    public bool Success => Failure == EquipmentAssemblyFailure.None;
    public static EquipmentAssemblyOutcome Failed(EquipmentAssemblyFailure failure) =>
        new(failure, null, string.Empty, Array.Empty<TraitInstance>(), Array.Empty<TraitInstance>(), false);
}

/// <summary>
/// What a fabrication <i>would</i> produce, computed before the player commits — the §6.2c
/// fairness principle extended to the terminal step (D30/R3): components are consumed forever,
/// so the payoff is never a surprise. Pure read-model: nothing is consumed or registered.
/// </summary>
public sealed record EquipmentAssemblyPreview(
    EquipmentAssemblyFailure Failure,
    string Name,
    IReadOnlyDictionary<string, double> Stats,
    IReadOnlyList<TraitInstance> Expressed,
    IReadOnlyList<TraitInstance> Dormant,
    IReadOnlyDictionary<string, double> Essence,
    ArmorStats? Armor,
    IReadOnlyList<(string Slot, string Material)> ComponentNames,
    bool WouldBeFirstOfItsKind,
    ItemPotential Potential,
    IReadOnlyList<Affixes.RolledAffix> Innates)
{
    public bool CanFabricate => Failure == EquipmentAssemblyFailure.None;

    public static EquipmentAssemblyPreview Failed(EquipmentAssemblyFailure failure) => new(
        failure, string.Empty,
        new Dictionary<string, double>(), Array.Empty<TraitInstance>(), Array.Empty<TraitInstance>(),
        new Dictionary<string, double>(), null, Array.Empty<(string, string)>(), false,
        ItemPotential.Empty, Array.Empty<Affixes.RolledAffix>());
}

/// <summary>
/// Assembles finished equipment from an <see cref="EquipmentBlueprintDefinition"/> plus one
/// material per named slot — the §16 boundary where materials stop.
///
/// <para>Terminal on purpose (§16.1): inputs are consumed, workability stops mattering, and the
/// output is an <see cref="ItemInstance"/> over a <b>derived equipment definition</b> registered
/// by signature into the same store authored gear lives in.</para>
///
/// <para>The 0–100 ↔ combat-unit reconciliation happens <b>here and only here</b>: stat_map
/// contributions normalise material properties onto the instance in combat units, so
/// <c>EquipmentResolver</c>'s seam consumes assembled gear without changing.</para>
///
/// <para>The item's <see cref="ItemPotential"/> is computed here too, and is what
/// <see cref="Dungeons.Affixes.ModifierGenerator"/> then rolls against.</para>
/// </summary>
public sealed class EquipmentAssemblyEngine
{
    private readonly ContentBundle _content;
    private readonly Func<Inventory> _inventory;
    private readonly MaterialStateResolver _materialStates;
    private readonly InstanceIdSource _instanceIds;
    private readonly Randomness.IRandomSource? _random;

    /// <param name="random">The seeded roll source for affixes (R4b). Null = no rolling —
    /// items still get an item potential and innates (both deterministic), never rolled modifiers.</param>
    public EquipmentAssemblyEngine(
        ContentBundle content, Func<Inventory> inventory,
        MaterialStateResolver materialStates, InstanceIdSource instanceIds,
        Randomness.IRandomSource? random = null)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _materialStates = materialStates ?? throw new ArgumentNullException(nameof(materialStates));
        _instanceIds = instanceIds ?? throw new ArgumentNullException(nameof(instanceIds));
        _random = random;
    }

    /// <summary>The pre-commit view: same composition as <see cref="Fabricate"/>, no side
    /// effects. One computation, two callers — the projection can never drift from the truth.</summary>
    public EquipmentAssemblyPreview Preview(EquipmentAssemblyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var composed = Compose(request);
        if (composed.Failure != EquipmentAssemblyFailure.None)
            return EquipmentAssemblyPreview.Failed(composed.Failure);

        // Identity-bearing slot first: the edge leads the sentence, the binding closes it.
        var componentNames = composed.Components
            .OrderByDescending(c => composed.Form.Slots[c.Key].MassShare)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => (Slot: c.Key, Material: c.Value.Material.Name))
            .ToList();

        return new EquipmentAssemblyPreview(
            EquipmentAssemblyFailure.None,
            composed.Name,
            composed.Stats,
            composed.Expressed,
            composed.Dormant,
            composed.Essence,
            composed.Armor,
            componentNames,
            WouldBeFirstOfItsKind: !_content.Equipment.Contains(composed.Signature),
            composed.Potential,
            // Innates are deterministic — the preview can promise them (D-21: the item potential
            // speaking directly). Rolled modifiers stay behind the commit, by design.
            Affixes.ModifierGenerator.Innates(composed.Potential, _content.Affixes.GetAll()));
    }

    public EquipmentAssemblyOutcome Assemble(EquipmentAssemblyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var composed = Compose(request);
        if (composed.Failure != EquipmentAssemblyFailure.None)
            return EquipmentAssemblyOutcome.Failed(composed.Failure);

        var form = composed.Form;
        var inventory = _inventory();

        foreach (var (material, _) in composed.Components.Values)
            inventory.TryRemove(material.Id, 1);

        var isFirst = !_content.Equipment.Contains(composed.Signature);
        if (isFirst)
        {
            _content.Equipment.Add(new EquipmentDefinition
            {
                Id = composed.Signature,
                Name = composed.Name,
                Slot = form.Type,
                Tags = form.Tags,
                Moves = form.Moves,
                Armor = composed.Armor,
                Properties = new Dictionary<string, double>(composed.Stats),
                ExpressedTraits = composed.Expressed,
                DormantTraits = composed.Dormant,
                Essence = composed.Essence,
            });
        }

        // Innates first (deterministic), then the rolled prefixes and suffixes (§3.1 order).
        var affixes = new List<Affixes.RolledAffix>(
            Affixes.ModifierGenerator.Innates(composed.Potential, _content.Affixes.GetAll()));
        if (_random is not null)
        {
            affixes.AddRange(Affixes.ModifierGenerator.Roll(composed.Potential, "prefix", _content.Affixes.GetAll(), _random));
            affixes.AddRange(Affixes.ModifierGenerator.Roll(composed.Potential, "suffix", _content.Affixes.GetAll(), _random));
        }

        var instance = new ItemInstance
        {
            InstanceId = _instanceIds.Next(),
            BaseDefinitionId = composed.Signature,
            ItemType = form.Type == EquipmentSlot.Weapon ? ItemType.Weapon : ItemType.Armor,
            DisplayName = composed.Name,
            Properties = new PropertySet(composed.Stats),
            Provenance = composed.Components.Values.Select(c => c.Material.Id).ToList(),
            Traits = composed.Expressed.Select(t => t.Id).ToList(),
            Potential = composed.Potential,
            Affixes = affixes,
        };
        inventory.AddInstance(instance);

        return new EquipmentAssemblyOutcome(
            EquipmentAssemblyFailure.None, instance, composed.Name, composed.Expressed, composed.Dormant, isFirst);
    }

    // ---- The shared composition (§16.3 steps 1–6, side-effect free) --------------------------

    private sealed record ComposedItem(
        EquipmentAssemblyFailure Failure,
        EquipmentBlueprintDefinition Form,
        Dictionary<string, (MaterialDefinition Material, MaterialState State)> Components,
        Dictionary<string, double> Stats,
        List<TraitInstance> Expressed,
        List<TraitInstance> Dormant,
        Dictionary<string, double> Essence,
        ArmorStats? Armor,
        string Name,
        string Signature,
        ItemPotential Potential)
    {
        public static ComposedItem Rejected(EquipmentAssemblyFailure failure) => new(
            failure, null!, new(), new(), new(), new(), new(), null, string.Empty, string.Empty,
            ItemPotential.Empty);
    }

    private ComposedItem Compose(EquipmentAssemblyRequest request)
    {
        if (!_content.Forms.TryGetById(request.BlueprintId, out var form))
            return ComposedItem.Rejected(EquipmentAssemblyFailure.UnknownBlueprint);

        var components = new Dictionary<string, (MaterialDefinition Material, MaterialState State)>(StringComparer.Ordinal);
        foreach (var (slotName, slot) in form.Slots)
        {
            if (!request.SlotMaterials.TryGetValue(slotName, out var materialId))
                return ComposedItem.Rejected(EquipmentAssemblyFailure.MissingSlot);
            if (!_content.Materials.TryGetById(materialId, out var material))
                return ComposedItem.Rejected(EquipmentAssemblyFailure.UnknownMaterial);
            if (slot.RequiresTags.Count > 0
                && !slot.RequiresTags.Any(t => material.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
                return ComposedItem.Rejected(EquipmentAssemblyFailure.SlotRejected);
            components[slotName] = (material, _materialStates.StateOf(material));
        }

        var inventory = _inventory();
        if (components.Values.GroupBy(c => c.Material.Id)
            .Any(g => inventory.GetQuantity(g.Key) < g.Count()))
            return ComposedItem.Rejected(EquipmentAssemblyFailure.MissingInputs);

        // ---- §16.3 step 2: stats, in combat units ------------------------------------------
        var stats = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stat, contributions) in form.StatMap)
        {
            var total = 0.0;
            foreach (var contribution in contributions)
            {
                // Slot "*" reads the mass-share-weighted total across every slot; a named slot
                // reads only that component — which is what makes placement a real decision.
                double propertyValue = contribution.Slot == BlueprintSlots.AllSlots
                    ? form.Slots.Sum(s =>
                        components[s.Key].State.Properties.Get(contribution.Property) * s.Value.MassShare)
                    : components[contribution.Slot].State.Properties.Get(contribution.Property);
                total += propertyValue / 100.0 * contribution.Weight;
            }
            stats[stat] = Math.Round(Math.Max(0, total) * EquipmentAssemblyTuning.CombatUnitScale, 2);
        }

        // ---- §16.3 steps 3–4: traits through the trait expression; the rest go dormant --------------
        var traitsByExpression = new List<(TraitInstance Trait, double ExpressedMagnitude)>();
        foreach (var (slotName, component) in components)
        {
            var traitExpression = form.Slots[slotName].TraitExpression;
            foreach (var trait in component.State.Traits)
            {
                var category = _content.Traits.TryGetById(trait.Id, out var def) ? def.Category : "structural";
                var apertureFactor = traitExpression.GetValueOrDefault(category, 1.0);
                traitsByExpression.Add((trait, trait.Magnitude * apertureFactor));
            }
        }

        var ranked = traitsByExpression
            .OrderByDescending(t => t.ExpressedMagnitude)
            .ThenBy(t => t.Trait.Id, StringComparer.Ordinal)
            .ToList();
        var expressed = ranked.Take(form.TraitCap)
            .Select(t => new TraitInstance(t.Trait.Id, Math.Round(t.ExpressedMagnitude, 1))).ToList();
        var dormant = ranked.Skip(form.TraitCap).Select(t => t.Trait).ToList();

        // ---- §16.3 step 5: essence, mass-share weighted, arcane-amplified -------------------
        var arcane = form.Slots.Sum(s => components[s.Key].State.Properties.Get(ItemProperties.Arcane) * s.Value.MassShare);
        var essence = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slotName, component) in components)
        {
            foreach (var (key, value) in component.State.Essence)
            {
                var share = value * form.Slots[slotName].MassShare;
                essence[key] = essence.GetValueOrDefault(key)
                    + EssenceTuning.Expression(share, arcane);
            }
        }

        // ---- Armour derivation: response properties → lane resistances ----------------------
        ArmorStats? armor = null;
        if (form.Type == EquipmentSlot.Armor)
        {
            var resistances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var (property, lane) in EquipmentAssemblyTuning.ResponseLanes)
            {
                var value = form.Slots.Sum(s => components[s.Key].State.Properties.Get(property) * s.Value.MassShare);
                if (value > 0)
                    resistances[lane] = Math.Round(value / 100.0 * EquipmentAssemblyTuning.ResistancePerResponse, 3);
            }
            armor = new ArmorStats { Armor = 0, Resistances = resistances };
        }

        // ---- §16.3 step 7: signature → derived identity --------------------------------------
        // The heaviest slot names the item: an iron-edged longsword is an "Iron Longsword".
        var primarySlotName = form.Slots.OrderByDescending(s => s.Value.MassShare)
            .ThenBy(s => s.Key, StringComparer.Ordinal).First().Key;
        var name = ComposeName(form, components[primarySlotName].Material, expressed);
        var signature = ComputeSignature(form, components, stats);

        // ---- The ItemPotential (affixes.md §2.1) — computed here, stored on the instance, never
        // recomputed. MaterialStrength is the mass-share-weighted mean of the components (a mean, so
        // junk still dilutes); generation is the deepest component's.
        var materialStrength = (int)Math.Round(form.Slots.Sum(s =>
            components[s.Key].State.MaterialStrength * s.Value.MassShare));
        var itemPotential = new ItemPotential(
            form.Id,
            ItemPotentialCalculator.MaterialInfluence(form, components),
            essence,
            expressed,
            dormant,
            form.Tags,
            materialStrength,
            components.Values.Max(c => c.State.Generation),
            Array.Empty<string>()); // fabrication signatures are P4

        return new ComposedItem(
            EquipmentAssemblyFailure.None, form, components, stats, expressed, dormant, essence, armor, name, signature,
            itemPotential);
    }

    /// <summary>§16.5 — the dominant expressed trait's adjective, the primary material's root
    /// word, the form noun: "Emberveined Iron Longsword". Never every component.</summary>
    private string ComposeName(
        EquipmentBlueprintDefinition blueprint,
        MaterialDefinition primaryMaterial,
        IReadOnlyList<TraitInstance> expressed)
    {
        var root = primaryMaterial.Name.Split(' ')[0];
        var dominant = expressed.OrderByDescending(t => t.Magnitude).FirstOrDefault();
        var adjective = dominant is null ? null
            : _content.Traits.TryGetById(dominant.Id, out var def) ? def.Name : null;
        return adjective is null ? $"{root} {blueprint.Name}" : $"{adjective} {root} {blueprint.Name}";
    }

    /// <summary>Same form + same component archetypes + same stats = the same item kind —
    /// material archetype ids already encode their whole state, so this stays short.</summary>
    private static string ComputeSignature(
        EquipmentBlueprintDefinition form,
        Dictionary<string, (MaterialDefinition Material, MaterialState State)> components,
        Dictionary<string, double> stats)
    {
        var canonical = new StringBuilder("form=").Append(form.Id);
        foreach (var (slot, component) in components.OrderBy(c => c.Key, StringComparer.Ordinal))
            canonical.Append('|').Append(slot).Append(':').Append(component.Material.Id);
        foreach (var (stat, value) in stats.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
            canonical.Append('|').Append(stat.ToLowerInvariant()).Append(':')
                .Append(value.ToString("0.##", CultureInfo.InvariantCulture));

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))[..8].ToLowerInvariant();
        return "equip.emergent." + hash;
    }
}
