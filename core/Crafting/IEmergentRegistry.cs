using Dungeons.Content;

namespace Dungeons.Crafting;

/// <summary>The outcome of looking a signature up: the material, and whether the player just
/// brought it into existence.</summary>
/// <param name="Definition">The archetype, newly registered or already known.</param>
/// <param name="IsFirstDiscovery">True when this signature had never been produced before.</param>
public sealed record RegistryLookup(MaterialDefinition Definition, bool IsFirstDiscovery);

/// <summary>
/// The emergent archetype registry: <c>signature → generated definition</c>
/// (docs/emergent-item-system.md §12).
///
/// <para>It is a <b>deterministic cache, not progress</b> (§12.4). Because a signature is a
/// pure function of state, regenerating an entry produces an identical result — so where it
/// is stored is an engineering choice that affects no gameplay. It lives in the save today,
/// behind this interface so it can move to an install-level store later without the engine
/// noticing. The codex — what <i>this character</i> has discovered — is a separate, always
/// per-save thing, and is P6.</para>
/// </summary>
public interface IEmergentRegistry
{
    /// <summary>How many archetypes have been generated.</summary>
    int Count { get; }

    /// <summary>Every generated archetype, for persistence and inspection.</summary>
    IReadOnlyCollection<MaterialDefinition> All { get; }

    /// <summary>True if <paramref name="signature"/> has been produced before.</summary>
    bool Contains(string signature);

    bool TryGet(string signature, out MaterialDefinition definition);

    /// <summary>
    /// Returns the archetype for <paramref name="signature"/>, creating and registering it via
    /// <paramref name="create"/> if this is the first time it has been produced.
    /// </summary>
    RegistryLookup GetOrRegister(string signature, Func<MaterialDefinition> create);

    /// <summary>Restores archetypes from a save. Entries already present are left alone —
    /// they are byte-identical by construction.</summary>
    void Restore(IEnumerable<MaterialDefinition> archetypes);
}
