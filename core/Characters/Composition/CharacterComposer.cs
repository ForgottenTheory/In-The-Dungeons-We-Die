using Dungeons.Characters.Modifiers;
using Dungeons.Characters.Rules;
using Dungeons.Content;

namespace Dungeons.Characters.Composition;

/// <summary>
/// Resolves a <see cref="CharacterBuild"/> into a <see cref="CharacterBlueprint"/>
/// by applying the modifier pipeline across all four identity components and
/// resolving their rule hooks. Unknown component ids or rule ids fail loudly.
/// Contains no per-class special-casing — everything flows through data-driven
/// modifiers, tags and rules (docs/architecture.md §17–18).
/// </summary>
public sealed class CharacterComposer
{
    private readonly DataStore<SpeciesDefinition> _species;
    private readonly DataStore<BaseClassDefinition> _baseClasses;
    private readonly DataStore<PrefixDefinition> _prefixes;
    private readonly DataStore<SuffixDefinition> _suffixes;
    private readonly RuleRegistry _rules;

    public CharacterComposer(
        DataStore<SpeciesDefinition> species,
        DataStore<BaseClassDefinition> baseClasses,
        DataStore<PrefixDefinition> prefixes,
        DataStore<SuffixDefinition> suffixes,
        RuleRegistry rules)
    {
        _species = species;
        _baseClasses = baseClasses;
        _prefixes = prefixes;
        _suffixes = suffixes;
        _rules = rules;
    }

    public CharacterBlueprint Compose(CharacterBuild build, AttributeSet baseline)
    {
        ArgumentNullException.ThrowIfNull(build);

        var species = Resolve(_species, build.SpeciesId, "species");
        var baseClass = Resolve(_baseClasses, build.BaseClassId, "base class");
        var prefix = Resolve(_prefixes, build.PrefixId, "prefix");
        var suffix = Resolve(_suffixes, build.SuffixId, "suffix");

        var components = new CharacterComponentDefinition[] { species, baseClass, prefix, suffix };
        var modifiers = components.SelectMany(c => c.Modifiers).Select(m => m.ToModifier()).ToList();

        var attributes = ResolveAttributes(baseline, modifiers);

        var maxHealth = ModifierPipeline.ResolveInt(StatId.MaxHealth, ResourceCalculator.MaxHealth(attributes), modifiers);
        var maxMana = ModifierPipeline.ResolveInt(StatId.MaxMana, ResourceCalculator.MaxMana(attributes), modifiers);
        var maxStamina = ModifierPipeline.ResolveInt(StatId.MaxStamina, ResourceCalculator.MaxStamina(attributes), modifiers);

        var tags = new HashSet<string>(StringComparer.Ordinal);
        var abilities = new List<string>();
        var ruleIds = new List<string>();
        foreach (var component in components)
        {
            tags.UnionWith(component.Tags);
            abilities.AddRange(component.AbilityIds);
            ruleIds.AddRange(component.RuleIds);
        }

        var rules = ruleIds.Distinct(StringComparer.Ordinal).Select(_rules.Resolve).ToList();

        // Identity order per docs/classes.md §1: Species + Prefix + Base Class + Suffix.
        var displayName = $"{species.Name} {prefix.Name} {baseClass.Name} {suffix.Name}";

        return new CharacterBlueprint
        {
            Build = build,
            DisplayName = displayName,
            BaseAttributes = attributes,
            MaxHealth = maxHealth,
            MaxMana = maxMana,
            MaxStamina = maxStamina,
            PrimaryResource = baseClass.PrimaryResource,
            Tags = tags,
            AbilityIds = abilities,
            Rules = rules,
        };
    }

    private static AttributeSet ResolveAttributes(AttributeSet baseline, IReadOnlyList<StatModifier> modifiers)
    {
        var result = baseline;
        foreach (var stat in StatIds.Attributes)
        {
            var attribute = StatIds.ToAttribute(stat);
            var value = ModifierPipeline.ResolveInt(stat, baseline[attribute], modifiers);
            result = result.With(attribute, value);
        }

        return result;
    }

    private static T Resolve<T>(DataStore<T> store, string id, string label) where T : CharacterComponentDefinition
    {
        if (store.TryGetById(id, out var definition))
            return definition;
        throw new KeyNotFoundException($"Unknown {label} id '{id}' referenced by character build.");
    }
}
