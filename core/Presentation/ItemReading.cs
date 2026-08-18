using Dungeons.Affixes;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;

namespace Dungeons.Presentation;

/// <summary>One innate or rolled modifier, in player language ($roll already substituted).</summary>
public sealed record AffixLine(string Text, int Tier, bool Innate, string? Drawback);

/// <summary>One family the item potential supports, for the pre-roll readout ("engineer, then gamble").</summary>
public sealed record GenomeSupport(string Family, int BestTier, double Weight);

/// <summary>One damage packet of a granted move, in gameplay language: lane + combat units.</summary>
public sealed record PacketReading(string Lane, double Amount);

/// <summary>One move an item grants, resolved with the instance's properties applied — the
/// same numbers combat will use, which is what makes them gameplay stats, not simulation.</summary>
public sealed record MoveReading(
    string Name,
    IReadOnlyList<PacketReading> Packets,
    int ImpactTicks,
    int RecoveryTicks,
    IReadOnlyList<string> Costs);

/// <summary>
/// The §6 reveal hierarchy's view model (docs/presentation-architecture.md §3): identity →
/// combat stats → traits as named effects → material influence. Innates and rolled modifiers
/// join between combat stats and traits when R4 lands. Works identically for fabricated,
/// authored and (future) dropped equipment — it keys off instance + definition, never origin.
/// </summary>
public sealed record ItemReading(
    string Name,
    string Slot,
    IReadOnlyList<MoveReading> Moves,
    double Armor,
    IReadOnlyDictionary<string, double> Resistances,
    IReadOnlyList<AffixLine> Innates,
    IReadOnlyList<AffixLine> Modifiers,
    IReadOnlyList<TraitReading> Expressed,
    IReadOnlyList<string> DormantNames,
    IReadOnlyList<string> MadeOf,
    IReadOnlyList<(string Slot, string Material)> ComponentNames);

public static class ItemReadings
{
    /// <summary>Reads an owned item. <paramref name="instance"/> may be null for a bare
    /// definition (authored gear in a list).</summary>
    public static ItemReading From(ItemInstance? instance, EquipmentDefinition definition, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(content);

        var moves = definition.Slot == EquipmentSlot.Weapon
            ? EquipmentResolver.ResolveWeaponMoves(definition, instance, content.Moves)
                .Select(MoveReadingOf).ToList()
            : (IReadOnlyList<MoveReading>)Array.Empty<MoveReading>();

        var armor = EquipmentResolver.ResolveArmor(definition, instance);

        var expressed = definition.ExpressedTraits.Count > 0
            ? definition.ExpressedTraits
            : (instance?.Traits ?? Array.Empty<string>() as IReadOnlyList<string>)
                .Select(id => new TraitInstance(id, 0)).ToList();

        var madeOf = (instance?.Provenance ?? Array.Empty<string>() as IReadOnlyList<string>)
            .Select(id => content.Materials.TryGetById(id, out var m) ? m.Name : id)
            .ToList();

        var lines = AffixLines(instance?.Affixes ?? Array.Empty<RolledAffix>(), content);

        return new ItemReading(
            instance?.DisplayName ?? definition.Name,
            EquipmentSlotNames.CategoryOf(definition.Slot),
            moves,
            armor.Armor,
            armor.Resistances,
            lines.Where(l => l.Innate).ToList(),
            lines.Where(l => !l.Innate).ToList(),
            expressed.Select(t => TraitReadingOf(t, content)).ToList(),
            definition.DormantTraits.Select(t => TraitName(t.Id, content)).ToList(),
            madeOf,
            Array.Empty<(string, string)>());
    }

    /// <summary>Rolled affixes as player lines — the only place $roll meets text.</summary>
    public static IReadOnlyList<AffixLine> AffixLines(IReadOnlyList<RolledAffix> affixes, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(affixes);
        ArgumentNullException.ThrowIfNull(content);

        var lines = new List<AffixLine>();
        foreach (var rolled in affixes)
        {
            if (!content.Affixes.TryGetById(rolled.AffixId, out var definition))
                continue;

            lines.Add(new AffixLine(
                ModifierGrants.Describe(rolled, definition),
                rolled.Tier,
                string.Equals(definition.Slot, "innate", StringComparison.OrdinalIgnoreCase),
                string.IsNullOrWhiteSpace(definition.Drawback) ? null : definition.Drawback));
        }

        return lines;
    }

    /// <summary>Reads a fabrication projection as the item it would become — an ephemeral
    /// definition built from the projected stats, resolved through the same
    /// <see cref="EquipmentResolver"/> seam, so the preview and the minted item can never
    /// disagree. Nothing is registered.</summary>
    public static ItemReading From(EquipmentAssemblyPreview projection, EquipmentBlueprintDefinition form, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(content);

        var ephemeral = new EquipmentDefinition
        {
            Id = "equip.__projection",
            Name = projection.Name,
            Slot = form.Type,
            Tags = form.Tags,
            Moves = form.Moves,
            Armor = projection.Armor,
            Properties = new Dictionary<string, double>(
                projection.Stats.ToDictionary(s => s.Key, s => s.Value)),
            ExpressedTraits = projection.Expressed,
            DormantTraits = projection.Dormant,
            Essence = projection.Essence.ToDictionary(e => e.Key, e => e.Value),
        };

        return From(null, ephemeral, content) with
        {
            ComponentNames = projection.ComponentNames,
            // The preview promises the deterministic layer only: innates are the item potential
            // speaking (D-21); rolled modifiers stay behind the commit, by design.
            Innates = AffixLines(projection.Innates, content),
        };
    }

    /// <summary>The pre-roll item potential translation (§2.3, semantic half): which families this
    /// item potential supports and at what ceiling — grouped best-tier-per-family, strongest first.</summary>
    public static IReadOnlyList<GenomeSupport> Supports(ItemPotential itemPotential, ContentBundle content, int max = 5)
    {
        ArgumentNullException.ThrowIfNull(itemPotential);
        ArgumentNullException.ThrowIfNull(content);

        return content.Affixes.GetAll()
            .Where(d => d.Slot is "prefix" or "suffix")
            .Where(d => ModifierGenerator.IsAvailableFor(d, itemPotential, Array.Empty<string>()))
            .Select(d => (d.Family, Tier: ModifierGenerator.MaximumModifierTier(d, itemPotential), Weight: ModifierGenerator.ChanceWeightFor(d, itemPotential)))
            .Where(x => x.Tier is not null && x.Weight > 0)
            .GroupBy(x => x.Family, StringComparer.OrdinalIgnoreCase)
            .Select(g => new GenomeSupport(
                g.Key,
                g.Min(x => x.Tier!.Tier),
                g.Max(x => x.Weight)))
            .OrderByDescending(s => s.Weight)
            .ThenBy(s => s.Family, StringComparer.Ordinal)
            .Take(max)
            .ToList();
    }

    private static MoveReading MoveReadingOf(MoveDefinition move) => new(
        move.Name,
        move.Packets.Select(p => new PacketReading(LaneText(p), p.Amount)).ToList(),
        move.Timing.TimeToImpactTicks,
        move.Timing.RecoveryTicks,
        move.Costs.Select(c => $"{c.Amount:0.#} {Capitalize(c.Resource)}").ToList());

    private static string LaneText(Packet packet) =>
        packet.Aspect is { Length: > 0 } aspect
            ? $"{packet.Type}·{Capitalize(aspect)}"
            : packet.Type.ToString();

    private static TraitReading TraitReadingOf(TraitInstance trait, ContentBundle content) =>
        content.Traits.TryGetById(trait.Id, out var def)
            ? new TraitReading(trait, def.Name, def.Drawback)
            : new TraitReading(trait, trait.Id, string.Empty);

    private static string TraitName(string id, ContentBundle content) =>
        content.Traits.TryGetById(id, out var def) ? def.Name : id;

    private static string Capitalize(string word) =>
        word.Length > 1 ? char.ToUpperInvariant(word[0]) + word[1..] : word.ToUpperInvariant();
}
