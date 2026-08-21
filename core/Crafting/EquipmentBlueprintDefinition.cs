using System.Text.Json.Serialization;
using Dungeons.Content;
using Dungeons.Combat;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>Slot-name conventions shared by form templates and everything that reads them.</summary>
public static class BlueprintSlots
{
    /// <summary>
    /// The wildcard slot name. A base read against it takes the mass-share-weighted total
    /// across every slot, rather than one named component.
    /// </summary>
    public const string AllSlots = "*";
}

/// <summary>One component slot of a form: what it accepts, how much of the item it is, and
/// whose identities speak first when the cap forces a choice (D51).</summary>
public sealed class BlueprintSlot
{
    /// <summary>Any-of tag gate — <c>["form:metal", "form:crystal"]</c> means either works.</summary>
    [JsonPropertyName("requires_tags")]
    public IReadOnlyList<string> RequiresTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("mass_share")]
    public double MassShare { get; init; } = 1.0;

    /// <summary>
    /// Identity-model expression priority (D51, §8.1): when the components carry more
    /// identities than the form's <see cref="EquipmentBlueprintDefinition.IdentityCap"/>,
    /// identities from higher-priority slots express first — the edge speaks before the
    /// binding. A plain readable integer on purpose: D51 forbids re-growing per-slot
    /// percentage apertures on the deterministic floor.
    /// </summary>
    [JsonPropertyName("identity_priority")]
    public int IdentityPriority { get; init; }
}

/// <summary>The closed vocabularies of the identity-model form fields (D46/D51). Both are
/// deliberately code-level sets: an item stat only joins when machinery consumes it (D30),
/// and the base stats are the four of D46 forever.</summary>
public static class IdentityFormVocabulary
{
    /// <summary>Weapon damage bonus, in combat units.</summary>
    public const string Damage = "damage";

    /// <summary>Swing weight: the read adds windup ticks — lower is faster.</summary>
    public const string Speed = "speed";

    /// <summary>Armor, in combat units.</summary>
    public const string Armor = "armor";

    /// <summary>The item stats a base read may feed. Each resolves in play today — damage and
    /// speed through resolved weapon moves, armor through the worn-armor profile.</summary>
    public static readonly IReadOnlySet<string> ItemStats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Damage, Speed, Armor,
    };

    /// <summary>The four visible base stats (D46) as base-read sources.</summary>
    public static readonly IReadOnlySet<string> BaseStats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "heft", "bite", "toughness", "give",
    };
}

/// <summary>One weighted base read (§11.5, D46): this slot's base stat, at this weight, in
/// base units. Slot <see cref="BlueprintSlots.AllSlots"/> reads the mass-share-weighted total
/// across every slot — "Heft across the whole".</summary>
public sealed class BaseReadContribution
{
    public string Slot { get; init; } = BlueprintSlots.AllSlots;

    /// <summary>One of <see cref="IdentityFormVocabulary.BaseStats"/>.</summary>
    public string Stat { get; init; } = string.Empty;

    public double Weight { get; init; } = 1.0;
}

/// <summary>
/// A form — the terminal boundary where materials become equipment (identity model since
/// Phase 7, D54). Base reads come from <b>named slots</b>, never a blend, so component
/// placement is a real decision; the same material is excellent in one form and wasted in
/// another, which is what stops a single "best material" existing.
/// </summary>
public sealed class EquipmentBlueprintDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Other names this same form may be called. A Falchion, a Scimitar and a Sabre are one
    /// blueprint — the same slots, the same reads, the same moves — so they are names here
    /// rather than five near-identical forms that the "no two forms are the same form" rule
    /// would rightly reject.
    ///
    /// <para>The pick is <b>deterministic from the derived definition id</b>, not random: the
    /// same materials in the same form always mint the same name, in preview and at the forge alike.
    /// A variant is cosmetic by construction — nothing reads it, so it can never become a
    /// mechanical difference by accident.</para>
    /// </summary>
    [JsonPropertyName("name_variants")]
    public IReadOnlyList<string> NameVariants { get; init; } = Array.Empty<string>();

    public EquipmentSlot Type { get; init; } = EquipmentSlot.Weapon;

    public Dictionary<string, BlueprintSlot> Slots { get; init; } = new();

    /// <summary>
    /// Identity-model active-identity cap (D51, §8.1): how many identities the finished item
    /// may actively express; the rest are recorded Dormant. Deliberately a different concept
    /// from material capacity (which governs crafting-side risk) — neither implies the other.
    /// <b>Unset means the form has not been migrated to the identity model yet</b> — the same
    /// coexistence seam as <see cref="Content.MaterialDefinition.Capacity"/>.
    /// </summary>
    [JsonPropertyName("identity_cap")]
    public int? IdentityCap { get; init; }

    /// <summary>
    /// Identity-model base reads (D46, §11.5): item stat → weighted reads of slot base
    /// stats, replacing the property <see cref="StatMap"/>. "Longsword reads Bite off the
    /// edge, Heft across the whole." Results land in combat units through
    /// <see cref="Identity.IdentityFabricationTuning"/>'s one scale constant.
    /// </summary>
    [JsonPropertyName("base_reads")]
    public Dictionary<string, IReadOnlyList<BaseReadContribution>> BaseReads { get; init; } = new();

    /// <summary>
    /// The form's own generation lean (§8 stage 1): a shield leans <c>on_block</c> the way
    /// its stat map leans defense. The same shape materials use for personality — favored
    /// vocabulary keys, weights-not-recipes — because it is the same idea wearing a form.
    /// Null = the form pushes generation nowhere.
    /// </summary>
    [JsonPropertyName("generation_profile")]
    public Content.SignatureProfile? GenerationProfile { get; init; }

    /// <summary>The moves this form grants — a longsword swings like a longsword whatever it
    /// is made of; the material adjusts the packets through the existing instance-mass seam.</summary>
    public IReadOnlyList<MoveGrantSpec> Moves { get; init; } = Array.Empty<MoveGrantSpec>();

    /// <summary>Tags the derived equipment definition carries (satisfies `equippedTag`).</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
