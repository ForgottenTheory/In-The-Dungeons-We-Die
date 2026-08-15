using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Characters.Rules;
using Dungeons.Content;
using Xunit;

namespace Dungeons.Tests.Characters;

/// <summary>
/// Loads the real shipped JSON content (game/data) and composes every
/// species × class × prefix × suffix combination. This validates that all enums
/// parse, every referenced rule id has a handler, and no combination throws —
/// the "fail loudly for broken content" guarantee (docs/json-schema.md §21).
/// </summary>
public class ContentValidationTests
{
    private static string DataDir => LocateDataDir();

    private static DataStore<T> Load<T>(string subfolder) where T : IDefinition
    {
        var store = new DataStore<T>();
        foreach (var file in Directory.GetFiles(Path.Combine(DataDir, subfolder), "*.json"))
            store.LoadOne(File.ReadAllText(file));
        return store;
    }

    [Fact]
    public void EveryCombinationComposesWithoutError()
    {
        var species = Load<SpeciesDefinition>("species");
        var classes = Load<BaseClassDefinition>("classes");
        var prefixes = Load<PrefixDefinition>("prefixes");
        var suffixes = Load<SuffixDefinition>("suffixes");

        // Meets docs/vertical-slice.md §5 minimums.
        Assert.True(species.Count >= 2);
        Assert.True(classes.Count >= 2);
        Assert.True(prefixes.Count >= 3);
        Assert.True(suffixes.Count >= 5);

        var rules = new RuleRegistry(new ICharacterRule[]
        {
            new UnreasonableConfidenceRule(),
            new InappropriateOptimismRule(),
        });
        var composer = new CharacterComposer(species, classes, prefixes, suffixes, rules);

        foreach (var sp in species.GetAll())
        foreach (var cl in classes.GetAll())
        foreach (var pr in prefixes.GetAll())
        foreach (var su in suffixes.GetAll())
        {
            var build = new CharacterBuild(new SpeciesId(sp.Id), new BaseClassId(cl.Id), new PrefixId(pr.Id), new SuffixId(su.Id));
            var character = new Character(composer.Compose(build, AttributeSet.Uniform(5)));
            Assert.True(character.Health.Max > 0, $"{build} produced non-positive health.");
        }
    }

    [Fact]
    public void AtLeastOneSuffixIsARuleBreaker()
    {
        var suffixes = Load<SuffixDefinition>("suffixes");
        Assert.Contains(suffixes.GetAll(), s => s.RuleIds.Count > 0);
    }

    private static string LocateDataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "game", "data");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate game/data from the test output directory.");
    }
}
