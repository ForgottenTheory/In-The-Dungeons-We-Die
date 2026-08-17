using System.Text.Json.Serialization;
using Dungeons.Content;
using Dungeons.Combat;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>Slot-name conventions shared by form templates and everything that reads them.</summary>
public static class FormSlots
{
    /// <summary>
    /// The wildcard slot name. A stat-map read against it takes the mass-share-weighted total
    /// across every slot, rather than one named component.
    /// </summary>
    public const string AllSlots = "*";
}

/// <summary>One component slot of a form (§16.2): what it accepts, how much of the item it
/// is, and how much of each trait category it lets express.</summary>
public sealed class FormSlot
{
    /// <summary>Any-of tag gate — <c>["form:metal", "form:crystal"]</c> means either works.</summary>
    [JsonPropertyName("requires_tags")]
    public IReadOnlyList<string> RequiresTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("mass_share")]
    public double MassShare { get; init; } = 1.0;

    /// <summary>Per-trait-category expression gate, 0–1. An Emberveined edge expresses at 1.0;
    /// the same material as a binding at 0.2. Whatever the gate holds back goes dormant.</summary>
    public Dictionary<string, double> Aperture { get; init; } = new();
}

/// <summary>One weighted read in a stat map: this slot's property, at this weight.
/// Slot <see cref="FormSlots.AllSlots"/> means the mass-share-weighted total across all slots.</summary>
public sealed class StatContribution
{
    public string Slot { get; init; } = FormSlots.AllSlots;
    public string Property { get; init; } = string.Empty;
    /// <summary>How strongly this read contributes to the stat, relative to the other reads.</summary>
    public double Weight { get; init; } = 1.0;
}

/// <summary>
/// A fabrication form (§16.2) — the terminal boundary where materials become equipment.
/// Stats read from <b>named slots</b>, never a blend, so component placement is a real
/// decision; the same material is excellent in one form and useless in another, which is what
/// stops a single "best material" existing.
/// </summary>
public sealed class FormTemplateDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public EquipmentSlot Type { get; init; } = EquipmentSlot.Weapon;

    public Dictionary<string, FormSlot> Slots { get; init; } = new();

    /// <summary>Stat name → weighted property reads. Values land on the fabricated instance
    /// in <b>combat units</b> (the 0–100 → ~0–5 reconciliation lives in
    /// <see cref="FabricationTuning.CombatUnitScale"/>), so the existing
    /// <c>EquipmentResolver</c> seam consumes fabricated gear unchanged.</summary>
    [JsonPropertyName("stat_map")]
    public Dictionary<string, IReadOnlyList<StatContribution>> StatMap { get; init; } = new();

    /// <summary>§10.4 — equipment keeps the top 4 traits by expressed magnitude.</summary>
    [JsonPropertyName("trait_cap")]
    public int TraitCap { get; init; } = 4;

    /// <summary>The moves this form grants — a longsword swings like a longsword whatever it
    /// is made of; the material adjusts the packets through the existing instance-mass seam.</summary>
    public IReadOnlyList<MoveGrantSpec> Moves { get; init; } = Array.Empty<MoveGrantSpec>();

    /// <summary>Tags the derived equipment definition carries (satisfies `equippedTag`).</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

/// <summary>The 0–100 ↔ combat-unit reconciliation, in one place (C2a; itemization.md §2).</summary>
public static class FabricationTuning
{
    /// <summary>A stat_map read of weight 1.0 on a property at 100 lands at this many combat
    /// units — chosen so a plain iron-ingot longsword matches the authored Iron Sword
    /// (mass 62, hardness 65 → ≈3 across the legacy ~0–5 scale), pinned by the parity test.</summary>
    public const double CombatUnitScale = 5.0;

    /// <summary>A response property at 100 grants this much lane resistance on armour forms.</summary>
    public const double ResistancePerResponse = 0.30;

    /// <summary>The §16.3 trait categories apertures gate. Closed, like every vocabulary.</summary>
    public static readonly IReadOnlySet<string> TraitCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "structural", "thermal", "charge", "toxic", "vital", "arcane",
    };

    /// <summary>Response property → damage lane, for armour resistance derivation.</summary>
    public static readonly IReadOnlyDictionary<string, string> ResponseLanes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [ItemProperties.HeatResistance] = "heat",
        [ItemProperties.ColdResistance] = "cold",
        [ItemProperties.ToxinResistance] = "toxin",
    };
}
