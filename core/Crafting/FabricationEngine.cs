using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

public sealed record FabricationRequest(string FormId, IReadOnlyDictionary<string, string> SlotMaterials);

public enum FabricationFailure
{
    None, UnknownForm, UnknownMaterial, MissingSlot, SlotRejected, MissingInputs,
}

public sealed record FabricationOutcome(
    FabricationFailure Failure,
    ItemInstance? Item,
    string Name,
    IReadOnlyList<TraitInstance> Expressed,
    IReadOnlyList<TraitInstance> Dormant,
    bool IsFirstOfItsKind)
{
    public bool Success => Failure == FabricationFailure.None;
    public static FabricationOutcome Failed(FabricationFailure failure) =>
        new(failure, null, string.Empty, Array.Empty<TraitInstance>(), Array.Empty<TraitInstance>(), false);
}

/// <summary>
/// What a fabrication <i>would</i> produce, computed before the player commits — the §6.2c
/// fairness principle extended to the terminal step (D30/R3): components are consumed forever,
/// so the payoff is never a surprise. Pure read-model: nothing is consumed or registered.
/// </summary>
public sealed record FabricationProjection(
    FabricationFailure Failure,
    string Name,
    IReadOnlyDictionary<string, double> Stats,
    IReadOnlyList<TraitInstance> Expressed,
    IReadOnlyList<TraitInstance> Dormant,
    IReadOnlyDictionary<string, double> Essence,
    ArmorStats? Armor,
    IReadOnlyList<(string Slot, string Material)> ComponentNames,
    bool WouldBeFirstOfItsKind,
    Genome Genome,
    IReadOnlyList<Affixes.RolledAffix> Innates)
{
    public bool CanFabricate => Failure == FabricationFailure.None;

    public static FabricationProjection Failed(FabricationFailure failure) => new(
        failure, string.Empty,
        new Dictionary<string, double>(), Array.Empty<TraitInstance>(), Array.Empty<TraitInstance>(),
        new Dictionary<string, double>(), null, Array.Empty<(string, string)>(), false,
        Genome.Empty, Array.Empty<Affixes.RolledAffix>());
}

/// <summary>
/// The §16 fabrication boundary — where materials stop. Terminal on purpose (§16.1): inputs
/// are consumed, integrity is irrelevant, and the output is an <see cref="ItemInstance"/> over
/// a <b>derived equipment definition</b> registered by signature into the same equipment store
/// authored gear lives in — the emergent-archetype pattern, applied to gear.
///
/// <para>The 0–100 ↔ combat-unit reconciliation happens here and only here: stat_map reads
/// normalize material properties and land on the instance in the legacy combat units, so
/// <c>EquipmentResolver</c>'s seam consumes fabricated gear without changing.</para>
/// </summary>
public sealed class FabricationEngine
{
    private readonly ContentBundle _content;
    private readonly Func<Inventory> _inventory;
    private readonly MaterialProfileResolver _profiles;
    private readonly InstanceIdSource _instanceIds;
    private readonly Randomness.IRandomSource? _random;

    /// <param name="random">The seeded roll source for affixes (R4b). Null = no rolling —
    /// items still get a genome and innates (both deterministic), never rolled modifiers.</param>
    public FabricationEngine(
        ContentBundle content, Func<Inventory> inventory,
        MaterialProfileResolver profiles, InstanceIdSource instanceIds,
        Randomness.IRandomSource? random = null)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _instanceIds = instanceIds ?? throw new ArgumentNullException(nameof(instanceIds));
        _random = random;
    }

    /// <summary>The pre-commit view: same composition as <see cref="Fabricate"/>, no side
    /// effects. One computation, two callers — the projection can never drift from the truth.</summary>
    public FabricationProjection Project(FabricationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var composed = Compose(request);
        if (composed.Failure != FabricationFailure.None)
            return FabricationProjection.Failed(composed.Failure);

        // Identity-bearing slot first: the edge leads the sentence, the binding closes it.
        var componentNames = composed.Components
            .OrderByDescending(c => composed.Form.Slots[c.Key].MassShare)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => (Slot: c.Key, Material: c.Value.Material.Name))
            .ToList();

        return new FabricationProjection(
            FabricationFailure.None,
            composed.Name,
            composed.Stats,
            composed.Expressed,
            composed.Dormant,
            composed.Essence,
            composed.Armor,
            componentNames,
            WouldBeFirstOfItsKind: !_content.Equipment.Contains(composed.Signature),
            composed.Genome,
            // Innates are deterministic — the preview can promise them (D-21: the genome
            // speaking directly). Rolled modifiers stay behind the commit, by design.
            Affixes.AffixRoller.Innates(composed.Genome, _content.Affixes.GetAll()));
    }

    public FabricationOutcome Fabricate(FabricationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var composed = Compose(request);
        if (composed.Failure != FabricationFailure.None)
            return FabricationOutcome.Failed(composed.Failure);

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
            Affixes.AffixRoller.Innates(composed.Genome, _content.Affixes.GetAll()));
        if (_random is not null)
        {
            affixes.AddRange(Affixes.AffixRoller.Roll(composed.Genome, "prefix", _content.Affixes.GetAll(), _random));
            affixes.AddRange(Affixes.AffixRoller.Roll(composed.Genome, "suffix", _content.Affixes.GetAll(), _random));
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
            Genome = composed.Genome,
            Affixes = affixes,
        };
        inventory.AddInstance(instance);

        return new FabricationOutcome(
            FabricationFailure.None, instance, composed.Name, composed.Expressed, composed.Dormant, isFirst);
    }

    // ---- The shared composition (§16.3 steps 1–6, side-effect free) --------------------------

    private sealed record ComposedItem(
        FabricationFailure Failure,
        FormTemplateDefinition Form,
        Dictionary<string, (MaterialDefinition Material, MaterialProfile Profile)> Components,
        Dictionary<string, double> Stats,
        List<TraitInstance> Expressed,
        List<TraitInstance> Dormant,
        Dictionary<string, double> Essence,
        ArmorStats? Armor,
        string Name,
        string Signature,
        Genome Genome)
    {
        public static ComposedItem Rejected(FabricationFailure failure) => new(
            failure, null!, new(), new(), new(), new(), new(), null, string.Empty, string.Empty,
            Genome.Empty);
    }

    private ComposedItem Compose(FabricationRequest request)
    {
        if (!_content.Forms.TryGetById(request.FormId, out var form))
            return ComposedItem.Rejected(FabricationFailure.UnknownForm);

        var components = new Dictionary<string, (MaterialDefinition Material, MaterialProfile Profile)>(StringComparer.Ordinal);
        foreach (var (slotName, slot) in form.Slots)
        {
            if (!request.SlotMaterials.TryGetValue(slotName, out var materialId))
                return ComposedItem.Rejected(FabricationFailure.MissingSlot);
            if (!_content.Materials.TryGetById(materialId, out var material))
                return ComposedItem.Rejected(FabricationFailure.UnknownMaterial);
            if (slot.RequiresTags.Count > 0
                && !slot.RequiresTags.Any(t => material.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
                return ComposedItem.Rejected(FabricationFailure.SlotRejected);
            components[slotName] = (material, _profiles.Resolve(material));
        }

        var inventory = _inventory();
        if (components.Values.GroupBy(c => c.Material.Id)
            .Any(g => inventory.GetQuantity(g.Key) < g.Count()))
            return ComposedItem.Rejected(FabricationFailure.MissingInputs);

        // ---- §16.3 step 2: stats, in combat units ------------------------------------------
        var stats = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stat, contributions) in form.StatMap)
        {
            var total = 0.0;
            foreach (var contribution in contributions)
            {
                // Slot "*" reads the mass-share-weighted total across every slot; a named slot
                // reads only that component — which is what makes placement a real decision.
                double propertyValue = contribution.Slot == FormSlots.AllSlots
                    ? form.Slots.Sum(s =>
                        components[s.Key].Profile.Properties.Get(contribution.Property) * s.Value.MassShare)
                    : components[contribution.Slot].Profile.Properties.Get(contribution.Property);
                total += propertyValue / 100.0 * contribution.Weight;
            }
            stats[stat] = Math.Round(Math.Max(0, total) * FabricationTuning.CombatUnitScale, 2);
        }

        // ---- §16.3 steps 3–4: traits through the aperture; the rest go dormant --------------
        var traitsByExpression = new List<(TraitInstance Trait, double ExpressedMagnitude)>();
        foreach (var (slotName, component) in components)
        {
            var aperture = form.Slots[slotName].Aperture;
            foreach (var trait in component.Profile.Traits)
            {
                var category = _content.Traits.TryGetById(trait.Id, out var def) ? def.Category : "structural";
                var apertureFactor = aperture.GetValueOrDefault(category, 1.0);
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
        var arcane = form.Slots.Sum(s => components[s.Key].Profile.Properties.Get(ItemProperties.Arcane) * s.Value.MassShare);
        var essence = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slotName, component) in components)
        {
            foreach (var (key, value) in component.Profile.Essence)
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
            foreach (var (property, lane) in FabricationTuning.ResponseLanes)
            {
                var value = form.Slots.Sum(s => components[s.Key].Profile.Properties.Get(property) * s.Value.MassShare);
                if (value > 0)
                    resistances[lane] = Math.Round(value / 100.0 * FabricationTuning.ResistancePerResponse, 3);
            }
            armor = new ArmorStats { Armor = 0, Resistances = resistances };
        }

        // ---- §16.3 step 7: signature → derived identity --------------------------------------
        // The heaviest slot names the item: an iron-edged longsword is an "Iron Longsword".
        var primarySlotName = form.Slots.OrderByDescending(s => s.Value.MassShare)
            .ThenBy(s => s.Key, StringComparer.Ordinal).First().Key;
        var name = ComposeName(form, components[primarySlotName].Material, expressed);
        var signature = ComputeSignature(form, components, stats);

        // ---- The Genome (affixes.md §2.1) — computed here, stored on the instance, never
        // recomputed. Potency is the mass-share-weighted mean of the components (a mean, so
        // junk still dilutes); generation is the deepest component's.
        var potency = (int)Math.Round(form.Slots.Sum(s =>
            components[s.Key].Profile.Potency * s.Value.MassShare));
        var genome = new Genome(
            form.Id,
            GenomeCalculator.Pressure(form, components),
            essence,
            expressed,
            dormant,
            form.Tags,
            potency,
            components.Values.Max(c => c.Profile.Generation),
            Array.Empty<string>()); // fabrication signatures are P4

        return new ComposedItem(
            FabricationFailure.None, form, components, stats, expressed, dormant, essence, armor, name, signature,
            genome);
    }

    /// <summary>§16.5 — the dominant expressed trait's adjective, the primary material's root
    /// word, the form noun: "Emberveined Iron Longsword". Never every component.</summary>
    private string ComposeName(FormTemplateDefinition form, MaterialDefinition primary, IReadOnlyList<TraitInstance> expressed)
    {
        var root = primary.Name.Split(' ')[0];
        var dominant = expressed.OrderByDescending(t => t.Magnitude).FirstOrDefault();
        var adjective = dominant is null ? null
            : _content.Traits.TryGetById(dominant.Id, out var def) ? def.Name : null;
        return adjective is null ? $"{root} {form.Name}" : $"{adjective} {root} {form.Name}";
    }

    /// <summary>Same form + same component archetypes + same stats = the same item kind —
    /// material archetype ids already encode their whole state, so this stays short.</summary>
    private static string ComputeSignature(
        FormTemplateDefinition form,
        Dictionary<string, (MaterialDefinition Material, MaterialProfile Profile)> components,
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
