using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Characters.Modifiers;
using Dungeons.Characters.Rules;
using Dungeons.Content;

namespace Dungeons.Tests.Characters;

/// <summary>Helpers for building in-memory definition stores and composers in tests.</summary>
internal static class CharacterTestData
{
    public static DataStore<T> Store<T>(params T[] items) where T : CharacterComponentDefinition
    {
        var store = new DataStore<T>();
        foreach (var item in items)
            store.Add(item);
        return store;
    }

    public static ModifierData Mod(StatId stat, ModifierOperation op, double value) =>
        new() { Stat = stat, Op = op, Value = value };

    public static RuleRegistry RealRules() =>
        new(new ICharacterRule[] { new UnreasonableConfidenceRule(), new InappropriateOptimismRule() });

    public static CharacterComposer Composer(
        DataStore<SpeciesDefinition> species,
        DataStore<BaseClassDefinition> classes,
        DataStore<PrefixDefinition> prefixes,
        DataStore<SuffixDefinition> suffixes,
        RuleRegistry? rules = null) =>
        new(species, classes, prefixes, suffixes, rules ?? new RuleRegistry(Array.Empty<ICharacterRule>()));
}
