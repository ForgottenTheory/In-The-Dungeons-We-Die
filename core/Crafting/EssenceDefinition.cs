using Dungeons.Content;

namespace Dungeons.Crafting;

/// <summary>
/// One of the seven typed essences (docs/emergent-item-system.md §5.2): the rare supernatural
/// layer over the mundane reactive properties. A burning coal has <c>heat: 70</c> and no
/// essence; an Ember Core has <c>heat</c> AND <c>essence.fire</c>. There is deliberately no
/// arcane essence (§5.2.1) — <c>arcane</c> stays a property, the medium elements travel
/// through, and no untyped default sneaks into a typed list.
/// </summary>
public sealed class EssenceDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>The bare vector key materials author (<c>"essence": { "fire": 60 }</c>).</summary>
    public string Key => Id.StartsWith("essence.", StringComparison.Ordinal) ? Id["essence.".Length..] : Id;

    /// <summary>The mundane property this essence amplifies and rides along with — a process
    /// whose channel moves the anchor moves this essence at a bonus (§8.4). Empty for radiant,
    /// which anchors on <c>resonance</c> by authoring it here explicitly.</summary>
    public string Anchor { get; init; } = string.Empty;

    /// <summary>Essence keys this one annihilates against (§8.5) — fire/frost,
    /// nature/necrotic, radiant/necrotic, radiant/abyssal.</summary>
    public IReadOnlyList<string> Opposes { get; init; } = Array.Empty<string>();
}

/// <summary>§5.3 capacity, §8.4 transfer and §5.2.1 amplification constants.</summary>
public static class EssenceTuning
{
    /// <summary>Capacity per point of resonance: <c>capacity = resonance × 1.5</c> (§5.3).
    /// Essence beyond it is strain — powerful magic needs a worthy vessel.</summary>
    public const double CapacityPerResonance = 1.5;

    /// <summary>Extra gain share when the process channel includes the essence's anchor —
    /// §8.4's "plus a bonus": a forge that moves heat carries fire essence more willingly.</summary>
    public const double AnchorChannelBonus = 0.5;

    /// <summary>Below this an essence reading is noise and is pruned, mirroring §8.3's
    /// property floor.</summary>
    public const double Floor = 1.0;

    /// <summary>§5.3: how much total essence a vessel of this resonance can hold cleanly.</summary>
    public static double Capacity(double resonance) => Math.Max(0, resonance) * CapacityPerResonance;

    /// <summary>§5.3 strain: the amount over capacity, which feeds effective instability.</summary>
    public static double Strain(IReadOnlyDictionary<string, double> essence, double resonance) =>
        Math.Max(0, essence.Values.Sum() - Capacity(resonance));

    /// <summary>§5.2.1 rule 2 — essence expresses through the arcane medium:
    /// <c>essence × (0.6 + 0.4 × arcane/100)</c>. Fire essence in a mundane host burns weakly;
    /// in an arcane-charged one it burns properly. Consumed by fabrication (P5) and reporting.</summary>
    public static double Expression(double essenceValue, double arcane) =>
        essenceValue * (0.6 + 0.4 * Math.Clamp(arcane, 0, 100) / 100.0);
}
