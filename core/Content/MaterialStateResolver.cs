using Dungeons.Items;

namespace Dungeons.Content;

/// <summary>
/// Produces the <see cref="MaterialState"/> for any material kind.
///
/// <para>Emergent archetypes are born with an explicit profile and are returned as-is.
/// Authored materials have none — the ~470-material library predates the emergent system —
/// so theirs is <b>derived</b> from data they already carry (rarity tag, state tag, property
/// profile), per <see cref="MaterialStateTuning"/>. A material may override either scalar
/// in JSON when the derivation is wrong for it.</para>
///
/// <para>Which properties count toward material strength comes from the property registry's roles, not
/// a code list: Structural and Reactive properties express when the material is used;
/// Response properties are derived (§2.2) and Sourcing properties describe how hard the
/// thing was to obtain (§2.2), so neither may inflate material strength.</para>
///
/// <para>Pure and deterministic — same definition in, same profile out.</para>
/// </summary>
public sealed class MaterialStateResolver
{
    private readonly IReadOnlySet<string> _expressiveProperties;
    private readonly Dictionary<string, MaterialState> _cache = new(StringComparer.Ordinal);

    public MaterialStateResolver(DataStore<PropertyDefinition> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        _expressiveProperties = properties.GetAll()
            .Where(p => p.Role is PropertyRole.Structural or PropertyRole.Reactive)
            .Select(p => p.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The property ids that contribute to derived material strength (Structural + Reactive).</summary>
    public IReadOnlySet<string> ExpressiveProperties => _expressiveProperties;

    /// <summary>
    /// The profile of <paramref name="definition"/> — its own if it has one (emergent),
    /// otherwise derived and cached (authored).
    /// </summary>
    public MaterialState StateOf(MaterialDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.State is not null)
            return definition.State;

        if (_cache.TryGetValue(definition.Id, out var cached))
            return cached;

        var derived = Derive(definition);
        _cache[definition.Id] = derived;
        return derived;
    }

    private MaterialState Derive(MaterialDefinition definition)
    {
        var properties = definition.BaseProperties;
        return new MaterialState(
            Properties: properties,
            MaterialStrength: definition.MaterialStrength ?? DeriveMaterialStrength(definition.Tags, properties, _expressiveProperties),
            Workability: definition.Workability ?? DeriveWorkability(definition.Tags),
            Lineage: Lineage.ForBase(definition.Id),
            // An authored material is its own archetype, so its signature is simply its id.
            Signature: definition.Id)
        {
            Essence = new Dictionary<string, double>(definition.Essence),
        };
    }

    /// <summary>
    /// Derived material strength (1–100) for an authored material: an expressiveness term over its own
    /// strongest properties, plus a modest rarity bonus (§6.1, <see cref="MaterialStateTuning"/>).
    /// </summary>
    public static int DeriveMaterialStrength(
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
            && MaterialStateTuning.MaterialStrengthByRarity.TryGetValue(rarity, out var bonus)
                ? bonus
                : MaterialStateTuning.DefaultRarityBonus;

        var materialStrength = MaterialStateTuning.MaterialStrengthFloor
            + MaterialStateTuning.MaterialStrengthSlope * expressiveness
            + rarityBonus;

        return (int)Math.Clamp(Math.Round(materialStrength, MidpointRounding.AwayFromZero), 1, 100);
    }

    /// <summary>Derived workability for an authored material, from its <c>state:</c> tag (§6.2).</summary>
    public static int DeriveWorkability(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var state = TagValue(tags, TagFamilies.State.Name);
        return state is not null && MaterialStateTuning.WorkabilityByState.TryGetValue(state, out var workability)
            ? workability
            : MaterialStateTuning.DefaultWorkability;
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

        return MaterialStateTuning.PeakWeight * peak + MaterialStateTuning.SecondPeakWeight * second;
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
