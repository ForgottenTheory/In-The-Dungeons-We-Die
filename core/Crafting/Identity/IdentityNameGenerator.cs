using Dungeons.Content;

namespace Dungeons.Crafting.Identity;

/// <summary>
/// Names an identity-model material — a pure function of final state, never of history
/// (the D42 lesson: history-based names grow without bound). The shape the reality tests
/// settled (docs/identity-foundation.md §12): identity adjectives, a root-derived
/// "-bound" adjective for a strong secondary root, the primary root's own name — at most
/// <see cref="IdentityCraftTuning.MaxNameWords"/> words, no tiers, no numbers, no "of".
/// <c>Dense Oakbound Iron Ingot</c> is the canonical output. Provisional until the naming
/// grammar gets its own content pass; deterministic always.
/// </summary>
public static class IdentityNameGenerator
{
    public static string NameFor(IdentityMaterialState state, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(content);

        var rootsByWeight = state.Roots
            .OrderByDescending(root => root.Weight)
            .ThenBy(root => root.DefinitionId, StringComparer.Ordinal)
            .ToList();
        var primaryName = rootsByWeight.Count > 0 ? RootName(rootsByWeight[0].DefinitionId, content) : "Material";

        // Carriers read as what they are: the vessel of one drawn-out identity.
        if (state.IsCarrier && state.Identities.Count == 1)
            return $"{FirstWord(primaryName)} {IdentityName(state.Identities[0].Id, content)} {IdentityCraftTuning.CarrierNoun}";

        // Identity adjectives, deepest first; a strong secondary root earns its "-bound".
        var identityAdjectives = state.Identities
            .Select((stake, index) => (stake, index))
            .OrderByDescending(pair => pair.stake.Rank)
            .ThenBy(pair => pair.index)
            .Select(pair => IdentityName(pair.stake.Id, content))
            .ToList();
        var boundAdjective = rootsByWeight.Count > 1
            && rootsByWeight[1].Weight >= IdentityCraftTuning.RootAdjectiveThreshold
                ? FirstWord(RootName(rootsByWeight[1].DefinitionId, content)) + "bound"
                : null;

        // Keep by priority (best adjective, then the bound word, then the rest) inside the
        // word budget; display in reading order: identities · bound · root.
        var budget = IdentityCraftTuning.MaxNameWords - primaryName.Split(' ').Length;
        var priority = new List<string>();
        if (identityAdjectives.Count > 0)
            priority.Add(identityAdjectives[0]);
        if (boundAdjective is not null)
            priority.Add(boundAdjective);
        priority.AddRange(identityAdjectives.Skip(1));
        var kept = priority.Take(Math.Max(budget, 0)).ToHashSet(StringComparer.Ordinal);

        var parts = identityAdjectives.Where(kept.Contains).ToList();
        if (boundAdjective is not null && kept.Contains(boundAdjective))
            parts.Add(boundAdjective);
        parts.Add(primaryName);
        return string.Join(' ', parts);
    }

    private static string RootName(string definitionId, ContentBundle content) =>
        content.Materials.TryGetById(definitionId, out var definition) ? definition.Name : "Material";

    private static string IdentityName(string identityId, ContentBundle content)
    {
        if (content.Identities.TryGetById(identityId, out var identity))
            return identity.Name;
        var slug = identityId[(identityId.LastIndexOf('.') + 1)..];
        return slug.Length == 0 ? identityId : char.ToUpperInvariant(slug[0]) + slug[1..];
    }

    private static string FirstWord(string name)
    {
        var space = name.IndexOf(' ');
        return space < 0 ? name : name[..space];
    }
}
