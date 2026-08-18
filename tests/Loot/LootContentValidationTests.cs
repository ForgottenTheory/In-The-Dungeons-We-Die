using Dungeons.Content;
using Dungeons.Loot;
using Xunit;

namespace Dungeons.Tests.Loot;

/// <summary>
/// Per-rule failing content for <c>ValidateLootTables</c>. The shipped tables exercise the happy
/// path; this file proves each rule actually <em>fires</em>, which is the half that rots
/// silently — a validation rule that never rejects anything is indistinguishable from no rule.
/// </summary>
public class LootContentValidationTests
{
    private static IReadOnlyList<ContentProblem> Problems(params LootTableDefinition[] tables)
    {
        var store = new DataStore<LootTableDefinition>();
        foreach (var table in tables)
            store.Add(table);

        var materials = new DataStore<MaterialDefinition>();
        materials.Add(new MaterialDefinition
        {
            Id = "material.oak_log", Name = "Oak Log",
            Tags = new[] { "origin:flora", "comp:organic", "form:wood", "state:raw", "rarity:common" },
        });

        var content = new ContentBundle { LootTables = store, Materials = materials };
        return ContentValidator.Validate(content).Where(p => p.Category == "loot_tables").ToList();
    }

    private static void AssertFlagged(string fragment, params LootTableDefinition[] tables) =>
        Assert.Contains(Problems(tables), problem => problem.Message.Contains(fragment));

    private static LootTableDefinition Table(string id = "loot.t", params LootEntryDefinition[] always) => new()
    {
        Id = id, Name = "Table", AlwaysDrops = always,
    };

    [Fact]
    public void AValidTableProducesNoProblems() =>
        Assert.Empty(Problems(Table(always: new LootEntryDefinition { ItemId = "material.oak_log" })));

    [Fact]
    public void AnUnnamedTableIsFlagged() =>
        AssertFlagged("has no name", new LootTableDefinition
        {
            Id = "loot.t",
            AlwaysDrops = new[] { new LootEntryDefinition { ItemId = "material.oak_log" } },
        });

    [Fact]
    public void AnEmptyTableIsFlagged() =>
        AssertFlagged("no entries and no gold", new LootTableDefinition { Id = "loot.t", Name = "Table" });

    [Fact]
    public void AnEntryNamingAnUnknownItemIsFlagged() =>
        AssertFlagged("material.ghost", Table(always: new LootEntryDefinition { ItemId = "material.ghost" }));

    [Fact]
    public void AnEntryNestingAnUnknownTableIsFlagged() =>
        AssertFlagged("loot.ghost", Table(always: new LootEntryDefinition { TableId = "loot.ghost" }));

    [Fact]
    public void AnEntryNamingNothingAtAllIsFlagged() =>
        AssertFlagged("exactly one of", Table(always: new LootEntryDefinition()));

    [Fact]
    public void AnEntryNamingBothAnItemAndATableIsFlagged() =>
        AssertFlagged("exactly one of", Table(
            always: new LootEntryDefinition { ItemId = "material.oak_log", TableId = "loot.t" }));

    [Fact]
    public void AnInvertedQuantityRangeIsFlagged() =>
        AssertFlagged("inverted quantity range", Table(
            always: new LootEntryDefinition { ItemId = "material.oak_log", MinQuantity = 5, MaxQuantity = 2 }));

    /// <summary>The single-source-of-truth rule for rarity. A material states its own; an entry
    /// that restates it is a second place to be wrong.</summary>
    [Fact]
    public void AnEntryRestatingAMaterialsRarityIsFlagged() =>
        AssertFlagged("the tag is authoritative", Table(
            always: new LootEntryDefinition { ItemId = "material.oak_log", Rarity = LootRarity.Rare }));

    [Fact]
    public void ACyclicNestingIsFlagged() =>
        AssertFlagged("reaches itself",
            Table("loot.a", new LootEntryDefinition { TableId = "loot.b" }),
            Table("loot.b", new LootEntryDefinition { TableId = "loot.a" }));

    [Fact]
    public void AZeroWeightInsideADrawIsFlagged() =>
        AssertFlagged("could never be picked", new LootTableDefinition
        {
            Id = "loot.t", Name = "Table",
            WeightedDraws = new[]
            {
                new LootDrawDefinition
                {
                    Picks = 1,
                    Entries = new[] { new LootEntryDefinition { ItemId = "material.oak_log", Weight = 0 } },
                },
            },
        });

    [Fact]
    public void ADrawThatIsNothingButMissesIsFlagged() =>
        AssertFlagged("nothing but misses", new LootTableDefinition
        {
            Id = "loot.t", Name = "Table",
            WeightedDraws = new[]
            {
                new LootDrawDefinition
                {
                    Picks = 1,
                    Entries = new[] { new LootEntryDefinition { DropsNothing = true } },
                },
            },
        });

    [Fact]
    public void ADrawWithNoPicksIsFlagged() =>
        AssertFlagged("would never fire", new LootTableDefinition
        {
            Id = "loot.t", Name = "Table",
            WeightedDraws = new[]
            {
                new LootDrawDefinition
                {
                    Picks = 0,
                    Entries = new[] { new LootEntryDefinition { ItemId = "material.oak_log" } },
                },
            },
        });

    [Fact]
    public void AnOutOfRangeChanceIsFlagged() =>
        AssertFlagged("outside (0, 1]", new LootTableDefinition
        {
            Id = "loot.t", Name = "Table",
            ChanceDrops = new[] { new LootEntryDefinition { ItemId = "material.oak_log", Chance = 1.5 } },
        });

    [Fact]
    public void AnInvertedGoldRangeIsFlagged() =>
        AssertFlagged("inverted", new LootTableDefinition
        {
            Id = "loot.t", Name = "Table",
            Gold = new GoldDropDefinition { MinAmount = 40, MaxAmount = 10 },
        });

    [Fact]
    public void AConditionThatContradictsItselfIsFlagged() =>
        AssertFlagged("it can never drop", Table(always: new LootEntryDefinition
        {
            ItemId = "material.oak_log",
            When = new LootCondition { RequiresTags = new[] { "elite" }, ExcludesTags = new[] { "elite" } },
        }));

    [Fact]
    public void AnInvertedDepthRangeIsFlagged() =>
        AssertFlagged("inverted depth range", Table(always: new LootEntryDefinition
        {
            ItemId = "material.oak_log",
            When = new LootCondition { MinDepth = 4, MaxDepth = 2 },
        }));
}
