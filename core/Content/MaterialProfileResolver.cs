using Dungeons.Items;

namespace Dungeons.Content;

/// <summary>
/// Produces the <see cref="MaterialProfile"/> for any material kind.
///
/// <para>Emergent archetypes are born with an explicit profile and are returned as-is.
/// Authored materials have none — the ~470-material library predates the emergent system —
/// so theirs is <b>derived</b> from data they already carry (rarity tag, state tag, property
/// profile), per <see cref="MaterialProfileTuning"/>. A material may override either scalar
/// in JSON when the derivation is wrong for it.</para>
///
/// <para>Which properties count toward potency comes from the property registry's roles, not
/// a code list: Structural and Reactive properties express when the material is used;
/// Response properties are derived (§2.2) and Sourcing properties describe how hard the
/// thing was to obtain (§2.2), so neither may inflate potency.</para>
///
/// <para>Pure and deterministic — same definition in, same profile out.</para>
/// </summary>
public sealed class MaterialProfileResolver
{
    private readonly IReadOnlySet<string> _expressiveProperties;
    private readonly Dictionary<string, MaterialProfile> _cache = new(StringComparer.Ordinal);

    public MaterialProfileResolver(DataStore<PropertyDefinition> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        _expressiveProperties = properties.GetAll()
            .Where(p => p.Role is PropertyRole.Structural or PropertyRole.Reactive)
            .Select(p => p.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The property ids that contribute to derived potency (Structural + Reactive).</summary>
    public IReadOnlySet<string> ExpressiveProperties => _expressiveProperties;

    /// <summary>
    /// The profile of <paramref name="definition"/> — its own if it has one (emergent),
    /// otherwise derived and cached (authored).
    /// </summary>
    public MaterialProfile Resolve(MaterialDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Profile is not null)
            return definition.Profile;

        if (_cache.TryGetValue(definition.Id, out var cached))
            return cached;

        var derived = Derive(definition);
        _cache[definition.Id] = derived;
        return derived;
    }

    private MaterialProfile Derive(MaterialDefinition definition)
    {
        var properties = definition.BaseProperties;
        return new MaterialProfile(
            Properties: properties,
            Potency: definition.Potency ?? DerivePotency(definition.Tags, properties, _expressiveProperties),
            Integrity: definition.Integrity ?? DeriveIntegrity(definition.Tags),
            Lineage: Lineage.ForBase(definition.Id),
            // An authored material is its own archetype, so its signature is simply its id.
            Signature: definition.Id);
    }

    /// <summary>
    /// Derived potency (1–100) for an authored material: an expressiveness term over its own
    /// strongest properties, plus a modest rarity bonus (§6.1, <see cref="MaterialProfileTuning"/>).
    /// </summary>
    public static int DerivePotency(
        IReadOnlyList<string> tags,
        PropertySet properties,
        IReadOnlySet<string> expressiveProperties)
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(expressiveProperties);

        var expressiveness = Expressiveness(properties, expressiveProperties);

        var rarity = TagValue(tags, TagFamilies.Rarity.Name);
        var rarityBonus = rarity is not null
            && MaterialProfileTuning.PotencyByRarity.TryGetValue(rarity, out var bonus)
                ? bonus
                : MaterialProfileTuning.DefaultRarityBonus;

        var potency = MaterialProfileTuning.PotencyFloor
            + MaterialProfileTuning.PotencySlope * expressiveness
            + rarityBonus;

        return (int)Math.Clamp(Math.Round(potency, MidpointRounding.AwayFromZero), 1, 100);
    }

    /// <summary>Derived integrity for an authored material, from its <c>state:</c> tag (§6.2).</summary>
    public static int DeriveIntegrity(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var state = TagValue(tags, TagFamilies.State.Name);
        return state is not null && MaterialProfileTuning.IntegrityByState.TryGetValue(state, out var integrity)
            ? integrity
            : MaterialProfileTuning.DefaultIntegrity;
    }

    /// <summary>
    /// A blend of the strongest and second-strongest expressive property, so that a material
    /// with a broad strong profile reads as more potent than a one-trick one, and a material
    /// whose only high number is <c>harvest_resistance</c> reads as no more potent at all.
    /// </summary>
    private static double Expressiveness(PropertySet properties, IReadOnlySet<string> expressiveProperties)
    {
        double peak = 0.0, second = 0.0;
        foreach (var key in properties.Keys)
        {
            if (!expressiveProperties.Contains(key))
                continue;

            var value = properties.Get(key);
            if (value > peak)
            {
                second = peak;
                peak = value;
            }
            else if (value > second)
            {
                second = value;
            }
        }

        return MaterialProfileTuning.PeakWeight * peak + MaterialProfileTuning.SecondPeakWeight * second;
    }

    /// <summary>The value of the first <c>family:value</c> tag in <paramref name="family"/>, if any.</summary>
    private static string? TagValue(IReadOnlyList<string> tags, string family)
    {
        foreach (var tag in tags)
        {
            if (TagFamilies.TryParse(tag, out var tagFamily, out var value)
                && string.Equals(tagFamily, family, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return null;
    }
}
