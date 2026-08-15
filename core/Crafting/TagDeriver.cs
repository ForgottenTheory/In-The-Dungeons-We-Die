using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>
/// Derives the tags of a transformed material (docs/emergent-item-system.md §4.2).
///
/// <para>Tags are <b>derived, never inherited wholesale</b>. Carrying them forward would give
/// a generation-5 material forty tags; deriving them keeps the count naturally around 6–9 and
/// means tags always describe what the thing <i>is now</i>.</para>
///
/// <para>Three sources, in priority order: what the process asserts, what the resulting state
/// implies, and a narrow lineage carry.</para>
/// </summary>
public sealed class TagDeriver
{
    private readonly DataStore<PropertyDefinition> _properties;

    public TagDeriver(DataStore<PropertyDefinition> properties)
    {
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
    }

    public IReadOnlyList<string> Derive(
        IReadOnlyList<string> substrateTags,
        ProcessDefinition process,
        PropertySet result)
    {
        ArgumentNullException.ThrowIfNull(substrateTags);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(result);

        var tags = new List<string>(substrateTags);

        // §4.2 source 3, the exclusion half: `part:` never carries. It describes which bit of a
        // creature or plant the material was cut from, which stops meaning anything the moment
        // the material is transformed into something else.
        tags.RemoveAll(t => IsFamily(t, TagFamilies.Part.Name));

        // §4.2 source 1 — the process asserts. Clears run first so `form:*` can wipe the old
        // form before the new one is set, which is what stops a ground ingot being
        // simultaneously a powder and an ingot.
        foreach (var clear in process.TagEffects.Clear)
        {
            if (!TagFamilies.TryParse(clear, out var family, out var value))
                continue;

            if (value == ProcessTagEffects.ClearFamilyWildcard)
                tags.RemoveAll(t => IsFamily(t, family));
            else
                tags.RemoveAll(t => string.Equals(t, clear, StringComparison.OrdinalIgnoreCase));
        }

        tags.AddRange(process.TagEffects.Set);

        // §4.2 source 2 — state thresholds. Declared on the properties themselves, so a
        // material that ends up genuinely toxic is tagged venomous however it got there.
        foreach (var definition in _properties.GetAll())
        {
            foreach (var grant in definition.GrantsTags)
            {
                if (result.Get(definition.Id) >= grant.Min)
                    tags.Add(grant.Tag);
            }
        }

        return Normalize(tags);
    }

    /// <summary>
    /// De-duplicates and enforces single-value families. A threshold grant can collide with a
    /// carried tag — <c>comp:organic</c> arriving on something already <c>comp:inorganic</c> —
    /// and the later source wins, since it describes the material as it now is.
    /// </summary>
    private static IReadOnlyList<string> Normalize(List<string> tags)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Reversed so the last-added value of a single-value family is the one kept.
        for (var i = tags.Count - 1; i >= 0; i--)
        {
            var tag = tags[i];
            if (!seen.Add(tag))
                continue;

            if (TagFamilies.TryParse(tag, out var family, out _)
                && TagFamilies.TryGet(family, out var definition)
                && definition.Max == 1
                && result.Any(t => IsFamily(t, family)))
            {
                continue;
            }

            result.Add(tag);
        }

        result.Reverse();
        return result;
    }

    private static bool IsFamily(string tag, string family) =>
        TagFamilies.TryParse(tag, out var tagFamily, out _)
        && string.Equals(tagFamily, family, StringComparison.Ordinal);
}
