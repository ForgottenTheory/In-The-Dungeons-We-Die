using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>
/// Generates a material's display name from its final state (docs/emergent-item-system.md §13).
///
/// <para>The name is a <b>pure function of state, never of history</b>. History-based names
/// grow without bound — "Ember-Steeped Storm-Quenched Oak-Bound Iron" — while state-based ones
/// stay short, and two players who reach the same material read the same word.</para>
///
/// <para>Hard constraints, all enforced by tests: at most three words, no <c>of X</c>
/// constructions, no tier words (Greater/Lesser/Superior), and never a number. Intensity comes
/// from vocabulary ladders instead — a hot material is <i>Searing</i>, not "Greater Warmed".</para>
///
/// <para>P1 has no traits, so the adjective slot is filled by the material's dominant property
/// rather than its dominant trait. That is what produces §19's "Warmed Iron" for a craft that
/// only managed heat 7. When traits arrive in P2 they take priority for the slot.</para>
/// </summary>
public sealed class NameGenerator
{
    /// <summary>Intensity tier boundaries — four bands across the 0–100 property scale.</summary>
    private static readonly double[] TierThresholds = { 25.0, 50.0, 75.0 };

    /// <summary>Syllables for the last-resort coinage (§13.4). Never a number, per the grammar.</summary>
    private static readonly string[] Onsets = { "ka", "ve", "thu", "mor", "sil", "dra", "ny", "gol", "ash", "bry", "cor", "ith", "lun", "ser", "vor", "zel" };
    private static readonly string[] Codas = { "ex", "ur", "an", "ith", "ok", "el", "ys", "ar", "en", "ol", "ax", "ir", "um", "eth", "on", "ys" };

    private readonly DataStore<MaterialDefinition> _materials;
    private readonly DataStore<PropertyDefinition> _properties;
    private readonly Dictionary<string, NameWordDefinition> _intensity = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NameWordDefinition> _formNouns = new(StringComparer.OrdinalIgnoreCase);

    public NameGenerator(
        DataStore<MaterialDefinition> materials,
        DataStore<PropertyDefinition> properties,
        DataStore<NameWordDefinition> grammar)
    {
        _materials = materials ?? throw new ArgumentNullException(nameof(materials));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        ArgumentNullException.ThrowIfNull(grammar);

        foreach (var word in grammar.GetAll())
        {
            var target = word.Kind == NameWordKind.Intensity ? _intensity : _formNouns;
            target[word.Key] = word;
        }
    }

    /// <summary>
    /// Names a finalized result. <paramref name="isTaken"/> reports whether another archetype
    /// already uses a name, so collisions resolve deterministically (§13.4).
    /// </summary>
    public string Generate(MaterialProfile profile, IReadOnlyList<string> tags, Func<string, bool>? isTaken = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tags);

        var root = RootNoun(profile);
        var form = FormNoun(tags, root);
        var name = Compose(Adjective(profile.Properties), root, form);

        if (isTaken is null || !isTaken(name))
            return name;

        // §13.4: append the second-strongest trait, then the essence qualifier, then — as a
        // last resort — a stable coinage derived from the signature. P1 has neither traits nor
        // essence, so it goes straight to the coinage. Never a number.
        return Compose(Coinage(profile.Signature), root, form);
    }

    /// <summary>
    /// The root comes from the dominant lineage root's <i>first</i> word, so "Iron Ingot"
    /// contributes "Iron" and "Ember Core" contributes "Ember" (§13.3). If that root's share
    /// falls away in later generations the name shifts with it, which §13.3 calls out as a
    /// rare and satisfying moment.
    /// </summary>
    private string RootNoun(MaterialProfile profile)
    {
        var root = profile.Lineage.DominantRoot;
        if (root is null)
            return "Residue";

        if (!_materials.TryGetById(root.RootId, out var material) || string.IsNullOrWhiteSpace(material.Name))
            return "Residue";

        return material.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    /// <summary>The current form's noun, or empty when the form needs none (metal) or already
    /// reads in the root ("Stormglass" must not become "Stormglass Glass").</summary>
    private string FormNoun(IReadOnlyList<string> tags, string root)
    {
        foreach (var value in FormValues(tags))
        {
            if (!_formNouns.TryGetValue(value, out var definition) || definition.Words.Count == 0)
                continue;

            var noun = definition.Words[0];
            if (root.Contains(noun, StringComparison.OrdinalIgnoreCase))
                continue;

            return noun;
        }

        return string.Empty;
    }

    /// <summary>
    /// The dominant property's ladder word. Reactive properties win over structural ones,
    /// because what a material <i>does</i> is more interesting than what it is made of — so
    /// heated iron reads "Emberlit Iron", not "Hardened Iron".
    /// </summary>
    private string Adjective(PropertySet properties)
    {
        return Dominant(properties, PropertyRole.Reactive)
            ?? Dominant(properties, PropertyRole.Structural)
            ?? string.Empty;
    }

    private string? Dominant(PropertySet properties, PropertyRole role)
    {
        string? best = null;
        var bestValue = 0.0;

        // Ordered so ties resolve the same way every time rather than by dictionary order.
        foreach (var key in properties.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            if (!_properties.TryGetById(key, out var definition) || definition.Role != role)
                continue;

            var value = properties.Get(key);
            if (value <= bestValue || !_intensity.ContainsKey(key))
                continue;

            best = key;
            bestValue = value;
        }

        if (best is null)
            return null;

        var ladder = _intensity[best].Words;
        return ladder.Count == 0 ? null : ladder[Math.Min(Tier(bestValue), ladder.Count - 1)];
    }

    /// <summary>Which rung of a four-tier ladder a value sits on.</summary>
    public static int Tier(double value)
    {
        var tier = 0;
        foreach (var threshold in TierThresholds)
        {
            if (value < threshold)
                break;
            tier++;
        }

        return tier;
    }

    /// <summary>
    /// A stable two-syllable coinage from the signature hash (§13.4). Deterministic, so the
    /// same material always reads the same way, and pronounceable rather than hexadecimal.
    /// </summary>
    public static string Coinage(string signature)
    {
        ArgumentNullException.ThrowIfNull(signature);

        var hash = 0;
        foreach (var c in signature)
            hash = unchecked(hash * 31 + c);
        hash &= 0x7FFFFFFF;

        var word = Onsets[hash % Onsets.Length] + Codas[(hash / Onsets.Length) % Codas.Length];
        return char.ToUpperInvariant(word[0]) + word[1..];
    }

    /// <summary>Assembles the parts, dropping empties. Three words is the hard ceiling and the
    /// root always contributes exactly one, so it cannot be exceeded.</summary>
    private static string Compose(string adjective, string root, string formNoun) =>
        string.Join(' ', new[] { adjective, root, formNoun }.Where(p => !string.IsNullOrEmpty(p)));

    private static IEnumerable<string> FormValues(IReadOnlyList<string> tags) =>
        tags.Where(t => TagFamilies.TryParse(t, out var family, out _)
                        && string.Equals(family, TagFamilies.Form.Name, StringComparison.Ordinal))
            .Select(t => t[(t.IndexOf(':') + 1)..])
            .OrderBy(v => v, StringComparer.Ordinal);
}
