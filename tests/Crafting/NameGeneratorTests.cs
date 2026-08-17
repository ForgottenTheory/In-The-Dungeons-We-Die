using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// Emergent naming (docs/emergent-item-system.md §13).
///
/// <para>§17 lists "name spam" as a real failure mode — procedurally generated names are the
/// thing players judge an emergent system by, and a bad grammar makes every result read like
/// loot-generator noise. So the grammar's hard constraints are tested as constraints, across
/// the whole material library, rather than spot-checked on a few happy cases.</para>
/// </summary>
public class NameGeneratorTests
{
    private static DataStore<MaterialDefinition> Materials() =>
        TestPaths.LoadStore<MaterialDefinition>("materials");

    private static NameGenerator Generator(DataStore<MaterialDefinition>? materials = null) =>
        new(materials ?? Materials(),
            TestPaths.LoadStore<PropertyDefinition>("properties"),
            TestPaths.LoadStore<NameWordDefinition>("name_grammar"));

    private static MaterialState State(
        IDictionary<string, double> properties,
        string root = "material.iron_ingot",
        int generation = 2,
        string signature = "emergent.7f3a91c4") =>
        new(
            PropertySet.FromValues(new Dictionary<string, double>(properties)),
            MaterialStrength: 49,
            Workability: 72,
            Lineage: new Lineage(new[] { new RootShare(root, 1.0) }, generation, "process.forge_infusion", new[] { root }),
            Signature: signature);

    // ---- The §19 names -------------------------------------------------------------------

    /// <summary>
    /// §19 attempt 1 names its heat-7 result <b>"Warmed Iron"</b>. That falls out of the
    /// lowest rung of the heat ladder plus the dominant lineage root — with no trait involved,
    /// which is exactly the P1 case.
    /// </summary>
    [Fact]
    public void WorkedExample_NamesTheNaiveCraftWarmedIron()
    {
        var name = Generator().Generate(
            State(new Dictionary<string, double> { ["heat"] = 7, ["hardness"] = 65, ["mass"] = 62 }),
            new[] { "form:metal", "form:ingot", "state:extract" });

        Assert.Equal("Warmed Iron", name);
    }

    /// <summary>The same craft pushed harder climbs the ladder rather than gaining a tier
    /// word — §13.2's whole point.</summary>
    [Theory]
    [InlineData(7, "Warmed Iron")]
    [InlineData(35, "Emberlit Iron")]
    [InlineData(60, "Cindered Iron")]
    [InlineData(95, "Searing Iron")]
    public void IntensityClimbsTheLadder(double heat, string expected)
    {
        var name = Generator().Generate(
            State(new Dictionary<string, double> { ["heat"] = heat, ["hardness"] = 65, ["mass"] = 62 }),
            new[] { "form:metal" });

        Assert.Equal(expected, name);
    }

    /// <summary>§13.3: the root crossed with the form. Grinding iron gives "Iron Dust".</summary>
    [Fact]
    public void FormChangesTheNoun()
    {
        var generator = Generator();
        var properties = new Dictionary<string, double> { ["hardness"] = 40, ["mass"] = 30 };

        Assert.Equal("Tempered Iron Dust", generator.Generate(State(properties), new[] { "form:powder" }));
        Assert.Equal("Tempered Iron Tincture", generator.Generate(State(properties), new[] { "form:liquid" }));
    }

    /// <summary>What a material <i>does</i> is more interesting than what it is made of, so a
    /// reactive property takes the adjective slot even when a structural one is larger.</summary>
    [Fact]
    public void ReactivePropertiesWinTheAdjectiveSlot()
    {
        var name = Generator().Generate(
            State(new Dictionary<string, double> { ["heat"] = 30, ["hardness"] = 90, ["mass"] = 80 }),
            new[] { "form:metal" });

        Assert.Equal("Emberlit Iron", name);
    }

    /// <summary>An ordinary alloy still reads as something rather than falling back to a bare
    /// root, which is what would otherwise make every iron product "Iron".</summary>
    [Fact]
    public void StructuralPropertiesNameAMundaneResult()
    {
        var name = Generator().Generate(
            State(new Dictionary<string, double> { ["hardness"] = 80, ["mass"] = 60 }),
            new[] { "form:metal" });

        Assert.Equal("Adamant Iron", name);
    }

    /// <summary>"Stormglass Glass" would be worse than "Stormglass".</summary>
    [Fact]
    public void FormNounIsDropped_WhenTheRootAlreadySaysIt()
    {
        var name = Generator().Generate(
            State(new Dictionary<string, double> { ["charge"] = 40 }, root: "material.stormglass"),
            new[] { "form:glass" });

        Assert.Equal("Livewired Stormglass", name);
    }

    /// <summary>§13.3: when the dominant root shifts, so does the name.</summary>
    [Fact]
    public void RootComesFromTheDominantLineageRoot()
    {
        var generator = Generator();
        var properties = new Dictionary<string, double> { ["heat"] = 30 };

        Assert.Equal("Emberlit Iron", generator.Generate(State(properties, root: "material.iron_ingot"), new[] { "form:metal" }));
        Assert.Equal("Emberlit Oak", generator.Generate(State(properties, root: "material.oak_bark"), new[] { "form:metal" }));
    }

    // ---- §13.4 collisions -----------------------------------------------------------------

    /// <summary>Two different signatures can quantize to the same words. The tiebreak is a
    /// stable coinage — never a number, per §13.1.</summary>
    [Fact]
    public void CollisionsResolveToAStableCoinage()
    {
        var generator = Generator();
        var state = State(new Dictionary<string, double> { ["heat"] = 35 });

        var free = generator.Generate(state, new[] { "form:metal" });
        var collided = generator.Generate(state, new[] { "form:metal" }, isTaken: n => n == free);

        Assert.NotEqual(free, collided);
        Assert.EndsWith("Iron", collided);
        Assert.DoesNotContain(collided, char.IsDigit);

        // Same signature ⇒ same coinage, every time.
        Assert.Equal(collided, generator.Generate(state, new[] { "form:metal" }, isTaken: n => n == free));
    }

    [Fact]
    public void DifferentSignaturesGetDifferentCoinages()
    {
        var coinages = Enumerable.Range(0, 50)
            .Select(i => NameGenerator.Coinage($"emergent.{i:x8}"))
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(coinages.Count > 30, $"only {coinages.Count} distinct coinages from 50 signatures.");
    }

    // ---- The grammar's hard constraints, across the whole library ---------------------------

    /// <summary>
    /// §13.1's constraints, exercised over every material in the library crossed with a range
    /// of states — because a grammar that only holds for the examples is not a grammar.
    /// </summary>
    [Fact]
    public void EveryGeneratedNameObeysTheGrammar()
    {
        var materials = Materials();
        var generator = Generator(materials);
        var checkedNames = 0;

        foreach (var material in materials.GetAll())
        foreach (var heat in new[] { 0.0, 8.0, 40.0, 90.0 })
        {
            var properties = new Dictionary<string, double>(material.BaseProperties.AsDictionary());
            if (heat > 0)
                properties["heat"] = heat;

            var name = generator.Generate(
                State(properties, root: material.Id, signature: $"emergent.{material.Id.GetHashCode():x8}"),
                material.Tags);

            Assert.False(string.IsNullOrWhiteSpace(name), $"{material.Id} produced an empty name.");
            Assert.True(name.Split(' ').Length <= 3, $"'{name}' exceeds three words.");
            Assert.DoesNotContain(" of ", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(name, char.IsDigit);

            foreach (var word in name.Split(' '))
                Assert.DoesNotContain(word, ContentValidator.ForbiddenNameWords);

            checkedNames++;
        }

        Assert.True(checkedNames > 1500, "the sweep should have covered the whole library.");
    }

    /// <summary>Naming is a pure function of state (§13.1) — the same material always reads
    /// the same, for every player, forever.</summary>
    [Fact]
    public void NamingIsPure()
    {
        var state = State(new Dictionary<string, double> { ["heat"] = 35, ["hardness"] = 62 });

        Assert.Equal(
            Generator().Generate(state, new[] { "form:metal", "state:alloy" }),
            Generator().Generate(state, new[] { "state:alloy", "form:metal" }));
    }

    /// <summary>
    /// The practical test of a naming grammar: does a realistic spread of results read as
    /// distinct materials, or as the same word over and over? A grammar that collapses
    /// everything to "Iron" would push every craft into the coinage fallback.
    /// </summary>
    [Fact]
    public void ARangeOfResultsProducesDistinctReadableNames()
    {
        var generator = Generator();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var root in new[] { "material.iron_ingot", "material.oak_bark", "material.granite" })
        foreach (var form in new[] { "form:metal", "form:powder", "form:liquid" })
        foreach (var (property, value) in new[] { ("heat", 30.0), ("heat", 80.0), ("toxicity", 45.0), ("charge", 70.0) })
        {
            names.Add(generator.Generate(
                State(new Dictionary<string, double> { [property] = value, ["hardness"] = 50 }, root),
                new[] { form }));
        }

        Assert.Equal(36, names.Count); // 3 roots × 3 forms × 4 states, all distinct
    }
}
