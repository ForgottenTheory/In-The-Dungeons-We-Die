using Dungeons.Items;

namespace Dungeons.Content;

/// <summary>One ancestral root and its share of a material's makeup (0–1).</summary>
public sealed record RootShare(string RootId, double Weight);

/// <summary>
/// A material's fixed-size, lossy ancestry (docs/emergent-item-system.md §14). Roots carry
/// forward by weighted merge and are renormalized each generation; parent links are
/// <b>one level only</b> — the full tree is reconstructible by walking the archetype
/// registry, so a recursive copy is never embedded.
/// </summary>
public sealed record Lineage(
    IReadOnlyList<RootShare> Roots,
    int Generation,
    string ProcessId,
    IReadOnlyList<string> ParentSignatures)
{
    /// <summary>Maximum roots retained; anything beyond (or under the trace weight) is dropped.</summary>
    public const int MaxRoots = 3;

    /// <summary>Roots below this share are dropped into an implicit "trace" remainder (§14).</summary>
    public const double TraceWeight = 0.05;

    /// <summary>The lineage of an authored base material: itself, wholly, at generation 1.</summary>
    public static Lineage ForBase(string materialId) => new(
        new[] { new RootShare(materialId, 1.0) },
        Generation: 1,
        ProcessId: string.Empty,
        ParentSignatures: Array.Empty<string>());

    /// <summary>The heaviest root — what naming, flavour and valuation read (§13.3).</summary>
    public RootShare? DominantRoot =>
        Roots.Count == 0 ? null : Roots.Aggregate((a, b) => b.Weight > a.Weight ? b : a);
}

/// <summary>
/// The emergent-system state of a material kind: its properties plus the meta fields that
/// are deliberately <b>not</b> in <see cref="PropertySet"/> — potency, integrity and
/// generation (docs/emergent-item-system.md §6, §18). Keeping them off the property bag is
/// what stops the reaction algebra treating "how refined is this" as something alloyable.
///
/// <para>Every material kind has one: authored materials get theirs derived by
/// <see cref="MaterialProfileResolver"/>; emergent archetypes are born with an explicit one
/// computed by the reaction engine.</para>
///
/// <para>Essence and traits are P3/P2 and deliberately absent here.</para>
/// </summary>
public sealed record MaterialProfile(
    PropertySet Properties,
    int Potency,
    int Integrity,
    Lineage Lineage,
    string Signature)
{
    /// <summary>Depth counter, sourced from <see cref="Lineage"/> so the two can never
    /// disagree (§6.4 lists it as a meta field; §14 stores it on the lineage).</summary>
    public int Generation => Lineage.Generation;

    /// <summary>An integrity-0 material does not exist — it was destroyed (§6.2c).</summary>
    public bool IsDestroyed => Integrity <= 0;
}
