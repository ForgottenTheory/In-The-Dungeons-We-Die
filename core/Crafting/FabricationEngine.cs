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

    public FabricationEngine(
        ContentBundle content, Func<Inventory> inventory,
        MaterialProfileResolver profiles, InstanceIdSource instanceIds)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _instanceIds = instanceIds ?? throw new ArgumentNullException(nameof(instanceIds));
    }

    public FabricationOutcome Fabricate(FabricationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_content.Forms.TryGetById(request.FormId, out var form))
            return FabricationOutcome.Failed(FabricationFailure.UnknownForm);

        var components = new Dictionary<string, (MaterialDefinition Material, MaterialProfile Profile)>(StringComparer.Ordinal);
        foreach (var (slotName, slot) in form.Slots)
        {
            if (!request.SlotMaterials.TryGetValue(slotName, out var materialId))
                return FabricationOutcome.Failed(FabricationFailure.MissingSlot);
            if (!_content.Materials.TryGetById(materialId, out var material))
                return FabricationOutcome.Failed(FabricationFailure.UnknownMaterial);
            if (slot.RequiresTags.Count > 0
                && !slot.RequiresTags.Any(t => material.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
                return FabricationOutcome.Failed(FabricationFailure.SlotRejected);
            components[slotName] = (material, _profiles.Resolve(material));
        }

        var inventory = _inventory();
        if (components.Values.GroupBy(c => c.Material.Id)
            .Any(g => inventory.GetQuantity(g.Key) < g.Count()))
            return FabricationOutcome.Failed(FabricationFailure.MissingInputs);

        foreach (var (material, _) in components.Values)
            inventory.TryRemove(material.Id, 1);

        // ---- §16.3 step 2: stats, in combat units ------------------------------------------
        var stats = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stat, reads) in form.StatMap)
        {
            var total = 0.0;
            foreach (var read in reads)
            {
                double value = read.Slot == "*"
                    ? form.Slots.Sum(s => components[s.Key].Profile.Properties.Get(read.Property) * s.Value.MassShare)
                    : components[read.Slot].Profile.Properties.Get(read.Property);
                total += value / 100.0 * read.W;
            }
            stats[stat] = Math.Round(Math.Max(0, total) * FabricationTuning.CombatUnitScale, 2);
        }

        // ---- §16.3 steps 3–4: traits through the aperture; the rest go dormant --------------
        var expressed = new List<(TraitInstance Trait, double Expressed)>();
        foreach (var (slotName, component) in components)
        {
            var aperture = form.Slots[slotName].Aperture;
            foreach (var trait in component.Profile.Traits)
            {
                var category = _content.Traits.TryGetById(trait.Id, out var def) ? def.Category : "structural";
                var gate = aperture.GetValueOrDefault(category, 1.0);
                expressed.Add((trait, trait.Magnitude * gate));
            }
        }

        var ranked = expressed
            .OrderByDescending(t => t.Expressed)
            .ThenBy(t => t.Trait.Id, StringComparer.Ordinal)
            .ToList();
        var kept = ranked.Take(form.TraitCap)
            .Select(t => new TraitInstance(t.Trait.Id, Math.Round(t.Expressed, 1))).ToList();
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

        // ---- §16.3 step 7: signature → derived definition → instance ------------------------
        var primary = form.Slots.OrderByDescending(s => s.Value.MassShare)
            .ThenBy(s => s.Key, StringComparer.Ordinal).First().Key;
        var name = ComposeName(form, components[primary].Material, kept);
        var signature = Signature(form, components, stats);

        var isFirst = !_content.Equipment.Contains(signature);
        if (isFirst)
        {
            _content.Equipment.Add(new EquipmentDefinition
            {
                Id = signature,
                Name = name,
                Slot = form.Type,
                Tags = form.Tags,
                Moves = form.Moves,
                Armor = armor,
                Properties = new Dictionary<string, double>(stats),
                ExpressedTraits = kept,
                DormantTraits = dormant,
                Essence = essence,
            });
        }

        var instance = new ItemInstance
        {
            InstanceId = _instanceIds.Next(),
            BaseDefinitionId = signature,
            ItemType = form.Type == EquipmentSlot.Weapon ? ItemType.Weapon : ItemType.Armor,
            DisplayName = name,
            Properties = new PropertySet(stats),
            Provenance = components.Values.Select(c => c.Material.Id).ToList(),
            Traits = kept.Select(t => t.Id).ToList(),
        };
        inventory.AddInstance(instance);

        return new FabricationOutcome(FabricationFailure.None, instance, name, kept, dormant, isFirst);
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
    private static string Signature(
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
