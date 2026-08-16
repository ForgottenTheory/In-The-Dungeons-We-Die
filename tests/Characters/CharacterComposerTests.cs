using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Characters.Modifiers;
using Dungeons.Content;
using Xunit;
using static Dungeons.Tests.Characters.CharacterTestData;

namespace Dungeons.Tests.Characters;

public class CharacterComposerTests
{
    private static readonly CharacterBuild Build =
        new(new SpeciesId("species.s"), new BaseClassId("class.c"), new PrefixId("prefix.p"), new SuffixId("suffix.s"));

    private static CharacterComposer FullComposer(
        SpeciesDefinition? species = null,
        BaseClassDefinition? baseClass = null,
        PrefixDefinition? prefix = null,
        SuffixDefinition? suffix = null,
        Dungeons.Characters.Rules.RuleRegistry? rules = null) =>
        Composer(
            Store(species ?? new SpeciesDefinition { Id = "species.s", Name = "Undead" }),
            Store(baseClass ?? new BaseClassDefinition { Id = "class.c", Name = "Bastion", PrimaryResource = ResourceType.Stamina }),
            Store(prefix ?? new PrefixDefinition { Id = "prefix.p", Name = "Frenzied" }),
            Store(suffix ?? new SuffixDefinition { Id = "suffix.s", Name = "Of Testing" }),
            rules);

    [Fact]
    public void ResolvesAttributes_AddThenMultiplyAcrossComponents()
    {
        var composer = FullComposer(
            species: new SpeciesDefinition { Id = "species.s", Name = "S", Modifiers = new[] { Mod(StatId.Strength, ModifierOperation.Add, 2) } },
            prefix: new PrefixDefinition { Id = "prefix.p", Name = "P", Modifiers = new[] { Mod(StatId.Strength, ModifierOperation.Multiply, 2) } });

        var blueprint = composer.Compose(Build, AttributeSet.Uniform(5));

        Assert.Equal(14, blueprint.BaseAttributes.Strength); // (5 + 2) * 2
        Assert.Equal(5, blueprint.BaseAttributes.Luck);
    }

    [Fact]
    public void DerivesResourceMaxima_FromResolvedAttributes_ThenAppliesResourceModifiers()
    {
        var composer = FullComposer(
            baseClass: new BaseClassDefinition
            {
                Id = "class.c",
                Name = "C",
                PrimaryResource = ResourceType.Stamina,
                Modifiers = new[] { Mod(StatId.MaxStamina, ModifierOperation.Add, 10) },
            });

        var blueprint = composer.Compose(Build, AttributeSet.Uniform(5));

        // Base stamina (END5, DEX5) = 20 + 25 + 10 = 55, + 10 = 65.
        Assert.Equal(65, blueprint.MaxStamina);
        Assert.Equal(ResourceType.Stamina, blueprint.PrimaryResource);
    }

    [Fact]
    public void AggregatesTagsMovesAndBuildsDisplayName()
    {
        var composer = FullComposer(
            species: new SpeciesDefinition { Id = "species.s", Name = "Undead", Tags = new[] { "undead" } },
            baseClass: new BaseClassDefinition { Id = "class.c", Name = "Bastion", Tags = new[] { "defensive" }, Moves = new[] { new Dungeons.Combat.MoveGrantSpec { Id = "move.shield_bash" } } },
            prefix: new PrefixDefinition { Id = "prefix.p", Name = "Pyromaniac", Tags = new[] { "fire" } },
            suffix: new SuffixDefinition { Id = "suffix.s", Name = "Of The Exploding Kneecaps" });

        var blueprint = composer.Compose(Build, AttributeSet.Uniform(5));

        Assert.Equal("Undead Pyromaniac Bastion Of The Exploding Kneecaps", blueprint.DisplayName);
        Assert.Contains("undead", blueprint.Tags);
        Assert.Contains("fire", blueprint.Tags);
        Assert.Contains(blueprint.MoveGrants, g => g.Spec.Id == "move.shield_bash" && g.Source == "Bastion");
    }

    [Fact]
    public void UnknownComponentId_FailsLoudly()
    {
        var composer = FullComposer();
        var badBuild = Build with { SpeciesId = new SpeciesId("species.missing") };
        var ex = Assert.Throws<KeyNotFoundException>(() => composer.Compose(badBuild, AttributeSet.Uniform(5)));
        Assert.Contains("species", ex.Message);
    }

    [Fact]
    public void UnknownRuleId_FailsLoudly()
    {
        // Suffix references a rule id, but the registry is empty.
        var composer = FullComposer(
            suffix: new SuffixDefinition { Id = "suffix.s", Name = "Of Nonsense", RuleIds = new[] { "rule.does_not_exist" } });

        Assert.Throws<KeyNotFoundException>(() => composer.Compose(Build, AttributeSet.Uniform(5)));
    }

    [Fact]
    public void ParsesEnumsFromJson()
    {
        // Exercises the DataStore enum converters end-to-end (op, stat, resourceType).
        var classes = new DataStore<BaseClassDefinition>();
        classes.LoadOne("""
            {
              "id": "class.c",
              "name": "Hexslinger",
              "primaryResource": "Mana",
              "modifiers": [ { "stat": "MaxMana", "op": "Multiply", "value": 2.0 } ]
            }
            """);

        var composer = Composer(
            Store(new SpeciesDefinition { Id = "species.s", Name = "S" }),
            classes,
            Store(new PrefixDefinition { Id = "prefix.p", Name = "P" }),
            Store(new SuffixDefinition { Id = "suffix.s", Name = "S" }));

        var blueprint = composer.Compose(Build, AttributeSet.Uniform(5));

        Assert.Equal(ResourceType.Mana, blueprint.PrimaryResource);
        // Base mana (INT5, WIS5) = 10 + 25 + 15 = 50, * 2 = 100.
        Assert.Equal(100, blueprint.MaxMana);
    }
}
