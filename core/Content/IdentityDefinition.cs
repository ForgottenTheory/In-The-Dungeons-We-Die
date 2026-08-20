namespace Dungeons.Content;

/// <summary>
/// One material identity — a named mechanical door (Dense, Vital, Ember…) that opens an
/// effect family. The foundation of the crafting redesign (docs/identity-foundation.md §3,
/// DECISIONS D42/D44). The roster is deliberately small and closed by design review: a new
/// identity is a design decision, never a casual content addition —
/// <c>IdentityContentTests.TheRosterIsExactlyD44s24</c> pins the shipped set on purpose.
///
/// <para>Identity here always means <b>material</b> identity — not character identity (D22)
/// and not enemy identity (D26).</para>
///
/// <para>What an identity's effect family contains (payloads, rungs) is <b>not</b> stored
/// here — that is the payload registry's job and arrives with the Signature Resolver
/// (migration Phase 3). This definition is the vocabulary entry the rest of the content
/// references.</para>
/// </summary>
public sealed class IdentityDefinition : IDefinition
{
    /// <summary>Stable id, <c>identity.&lt;slug&gt;</c> (e.g. <c>identity.dense</c>).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The player-facing name — identities are legible by design (D42).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Which roster cluster it belongs to — one of <see cref="ContentValidator.IdentityClusters"/>
    /// (physical · precision · sustain · elemental · magical · occult · fortune · meta).
    /// Grouping for tooling and docs; carries no mechanics.
    /// </summary>
    public string Cluster { get; init; } = string.Empty;

    /// <summary>The one-sentence boundary — what this identity is, against its neighbours.</summary>
    public string Description { get; init; } = string.Empty;
}
