using Dungeons.Content;

namespace Dungeons.Crafting;

/// <summary>
/// The archetype registry, backed by the shared material store.
///
/// <para>Registering an emergent archetype <b>into the same
/// <see cref="DataStore{T}"/> the authored library lives in</b> is the load-bearing decision
/// here. It is what makes §0 Decision 3's promise true: emergent materials flow through every
/// existing code path — <c>Inventory</c> stacks, lookups, crafting inputs, loot, the UI —
/// with no special-casing anywhere, and nothing in the game needs to care whether an input
/// was authored or generated.</para>
/// </summary>
public sealed class EmergentRegistry : IEmergentRegistry
{
    private readonly DataStore<MaterialDefinition> _materials;
    private readonly Dictionary<string, MaterialDefinition> _generated = new(StringComparer.Ordinal);

    public EmergentRegistry(DataStore<MaterialDefinition> materials)
    {
        _materials = materials ?? throw new ArgumentNullException(nameof(materials));
    }

    public int Count => _generated.Count;

    public IReadOnlyCollection<MaterialDefinition> All => _generated.Values;

    public bool Contains(string signature) => _generated.ContainsKey(signature);

    public bool TryGet(string signature, out MaterialDefinition definition) =>
        _generated.TryGetValue(signature, out definition!);

    public RegistryLookup GetOrRegister(string signature, Func<MaterialDefinition> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        ArgumentNullException.ThrowIfNull(create);

        if (_generated.TryGetValue(signature, out var known))
            return new RegistryLookup(known, IsFirstDiscovery: false);

        var definition = create()
            ?? throw new InvalidOperationException($"No definition was created for signature '{signature}'.");

        if (!string.Equals(definition.Id, signature, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"An emergent archetype's id must be its signature, but '{definition.Id}' was registered under '{signature}'.");
        }

        Add(definition);
        return new RegistryLookup(definition, IsFirstDiscovery: true);
    }

    public void Restore(IEnumerable<MaterialDefinition> archetypes)
    {
        ArgumentNullException.ThrowIfNull(archetypes);

        foreach (var archetype in archetypes)
        {
            if (_generated.ContainsKey(archetype.Id))
                continue;

            Add(archetype);
        }
    }

    private void Add(MaterialDefinition definition)
    {
        _generated[definition.Id] = definition;

        // An authored material could in principle already hold the id; the `emergent.` prefix
        // makes that a content bug rather than a collision, so let DataStore say so loudly.
        if (!_materials.Contains(definition.Id))
            _materials.Add(definition);
    }
}
