using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Loot;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Loot;

/// <summary>
/// The resolver's mechanics, one rule at a time. Loot is the system where a silent failure is
/// hardest to notice in play — a table that has quietly stopped paying out looks exactly like
/// bad luck — so every drop rule gets a test that pins it against a controlled roll.
/// </summary>
public class LootResolverTests
{
    /// <summary>A random source that walks a fixed sequence, so "the weighted pick landed on
    /// the second entry" is a statement about the table, not about luck.</summary>
    private sealed class ScriptedRandom : IRandomSource
    {
        private readonly Queue<double> _doubles;
        private readonly double _fallbackDouble;
        private readonly bool _rollHigh;

        public ScriptedRandom(double fallbackDouble = 0.0, bool rollHighQuantities = false, params double[] doubles)
        {
            _fallbackDouble = fallbackDouble;
            _rollHigh = rollHighQuantities;
            _doubles = new Queue<double>(doubles);
        }

        public double NextDouble() => _doubles.Count > 0 ? _doubles.Dequeue() : _fallbackDouble;

        public int NextInt(int minInclusive, int maxExclusive) =>
            _rollHigh ? maxExclusive - 1 : minInclusive;
    }

    private static LootResolver Resolver(IRandomSource random, params LootTableDefinition[] tables) =>
        Resolver(random, new DataStore<MaterialDefinition>(), tables);

    private static LootResolver Resolver(
        IRandomSource random, DataStore<MaterialDefinition> materials, params LootTableDefinition[] tables)
    {
        var store = new DataStore<LootTableDefinition>();
        foreach (var table in tables)
            store.Add(table);
        return new LootResolver(new ContentBundle { LootTables = store, Materials = materials }, random);
    }

    private static LootEntryDefinition Item(string id, int min = 1, int max = 1) =>
        new() { ItemId = id, MinQuantity = min, MaxQuantity = max };

    // --- The three drop rules -----------------------------------------------

    [Fact]
    public void GuaranteedEntriesAlwaysDrop()
    {
        var resolver = Resolver(
            new ScriptedRandom(fallbackDouble: 0.999), // every chance roll fails
            new LootTableDefinition
            {
                Id = "loot.t", Name = "T",
                AlwaysDrops = new[] { Item("material.bone"), Item("material.hide") },
            });

        var result = resolver.Roll("loot.t", new LootContext());

        Assert.Equal(2, result.Drops.Count);
        Assert.Contains(result.Drops, drop => drop.ItemId == "material.bone");
        Assert.Contains(result.Drops, drop => drop.ItemId == "material.hide");
    }

    [Fact]
    public void ChanceDropsRespectTheirOwnChance()
    {
        var table = new LootTableDefinition
        {
            Id = "loot.t", Name = "T",
            ChanceDrops = new[]
            {
                new LootEntryDefinition { ItemId = "material.likely", Chance = 0.9 },
                new LootEntryDefinition { ItemId = "material.unlikely", Chance = 0.1 },
            },
        };

        // A roll of 0.5 clears 0.9 and fails 0.1 — exactly one of the two drops.
        var result = Resolver(new ScriptedRandom(fallbackDouble: 0.5), table).Roll("loot.t", new LootContext());

        Assert.Equal("material.likely", Assert.Single(result.Drops).ItemId);
    }

    [Fact]
    public void AWeightedDrawPicksByWeight()
    {
        var table = new LootTableDefinition
        {
            Id = "loot.t", Name = "T",
            WeightedDraws = new[]
            {
                new LootDrawDefinition
                {
                    Picks = 1,
                    Entries = new[]
                    {
                        new LootEntryDefinition { ItemId = "material.first", Weight = 30 },
                        new LootEntryDefinition { ItemId = "material.second", Weight = 70 },
                    },
                },
            },
        };

        // Total weight 100. A roll of 0.5 lands at 50, past the first entry's 30.
        var second = Resolver(new ScriptedRandom(fallbackDouble: 0.5), table).Roll("loot.t", new LootContext());
        Assert.Equal("material.second", Assert.Single(second.Drops).ItemId);

        // A roll of 0.1 lands at 10, inside the first entry's band.
        var first = Resolver(new ScriptedRandom(fallbackDouble: 0.1), table).Roll("loot.t", new LootContext());
        Assert.Equal("material.first", Assert.Single(first.Drops).ItemId);
    }

    [Fact]
    public void ADrawCanLandOnNothing()
    {
        var table = new LootTableDefinition
        {
            Id = "loot.t", Name = "T",
            WeightedDraws = new[]
            {
                new LootDrawDefinition
                {
                    Picks = 1,
                    Entries = new[]
                    {
                        new LootEntryDefinition { ItemId = "material.prize", Weight = 10 },
                        new LootEntryDefinition { DropsNothing = true, Weight = 90 },
                    },
                },
            },
        };

        var result = Resolver(new ScriptedRandom(fallbackDouble: 0.5), table).Roll("loot.t", new LootContext());
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void QuantityRollsInsideItsRange()
    {
        var table = new LootTableDefinition
        {
            Id = "loot.t", Name = "T",
            AlwaysDrops = new[] { Item("material.ore", min: 2, max: 5) },
        };

        var low = Resolver(new ScriptedRandom(), table).Roll("loot.t", new LootContext());
        Assert.Equal(2, Assert.Single(low.Drops).Quantity);

        var high = Resolver(new ScriptedRandom(rollHighQuantities: true), table).Roll("loot.t", new LootContext());
        Assert.Equal(5, Assert.Single(high.Drops).Quantity);
    }

    // --- Composition --------------------------------------------------------

    [Fact]
    public void ANestedTableIsRolledInFull()
    {
        var resolver = Resolver(
            new ScriptedRandom(),
            new LootTableDefinition
            {
                Id = "loot.outer", Name = "Outer",
                AlwaysDrops = new[] { new LootEntryDefinition { TableId = "loot.inner" } },
            },
            new LootTableDefinition
            {
                Id = "loot.inner", Name = "Inner",
                AlwaysDrops = new[] { Item("material.bone"), Item("material.hide") },
            });

        var result = resolver.Roll("loot.outer", new LootContext());
        Assert.Equal(2, result.Drops.Count);
    }

    [Fact]
    public void TheSameItemFromSeveralEntriesArrivesAsOneMergedDrop()
    {
        var resolver = Resolver(
            new ScriptedRandom(),
            new LootTableDefinition
            {
                Id = "loot.t", Name = "T",
                AlwaysDrops = new[] { Item("material.scrap", 2, 2), Item("material.scrap", 3, 3) },
            });

        var drop = Assert.Single(resolver.Roll("loot.t", new LootContext()).Drops);
        Assert.Equal("material.scrap", drop.ItemId);
        Assert.Equal(5, drop.Quantity);
    }

    [Fact]
    public void SeveralTablesRollIntoOneHaul()
    {
        var resolver = Resolver(
            new ScriptedRandom(),
            new LootTableDefinition { Id = "loot.family", Name = "F", AlwaysDrops = new[] { Item("material.hide") } },
            new LootTableDefinition { Id = "loot.role", Name = "R", AlwaysDrops = new[] { Item("material.scrap") } },
            new LootTableDefinition
            {
                Id = "loot.actor", Name = "A",
                AlwaysDrops = new[] { Item("material.trinket") },
                Gold = new GoldDropDefinition { MinAmount = 7, MaxAmount = 7 },
            });

        var result = resolver.Roll(new[] { "loot.family", "loot.role", "loot.actor" }, new LootContext());

        Assert.Equal(3, result.Drops.Count);
        Assert.Equal(7, result.Gold);
    }

    /// <summary>Validation rejects cycles, so this only fires on content that got past it. It
    /// must degrade into missing loot, never into a stack overflow mid-fight.</summary>
    [Fact]
    public void ACyclicTableTerminatesInsteadOfRecursingForever()
    {
        var resolver = Resolver(
            new ScriptedRandom(),
            new LootTableDefinition
            {
                Id = "loot.a", Name = "A",
                AlwaysDrops = new[] { Item("material.bone"), new LootEntryDefinition { TableId = "loot.b" } },
            },
            new LootTableDefinition
            {
                Id = "loot.b", Name = "B",
                AlwaysDrops = new[] { new LootEntryDefinition { TableId = "loot.a" } },
            });

        var result = resolver.Roll("loot.a", new LootContext());

        // Bone once per level of descent, capped — the point is that it returns at all.
        Assert.True(result.Drops.Single().Quantity <= LootTuning.MaxNestingDepth);
    }

    [Fact]
    public void AnUnknownTableYieldsNothingInsteadOfThrowing()
    {
        var result = Resolver(new ScriptedRandom()).Roll("loot.imaginary", new LootContext());
        Assert.True(result.IsEmpty);
    }

    // --- Conditions ---------------------------------------------------------

    [Fact]
    public void DepthGatesEntriesInBothDirections()
    {
        var table = new LootTableDefinition
        {
            Id = "loot.t", Name = "T",
            AlwaysDrops = new[]
            {
                new LootEntryDefinition { ItemId = "material.deep", When = new LootCondition { MinDepth = 2 } },
                new LootEntryDefinition { ItemId = "material.shallow", When = new LootCondition { MaxDepth = 1 } },
            },
        };
        var resolver = Resolver(new ScriptedRandom(), table);

        Assert.Equal("material.shallow", Assert.Single(resolver.Roll("loot.t", new LootContext(depth: 1)).Drops).ItemId);
        Assert.Equal("material.deep", Assert.Single(resolver.Roll("loot.t", new LootContext(depth: 2)).Drops).ItemId);
    }

    [Fact]
    public void TagsGateEntries()
    {
        var table = new LootTableDefinition
        {
            Id = "loot.t", Name = "T",
            AlwaysDrops = new[]
            {
                new LootEntryDefinition
                {
                    ItemId = "material.elite_prize",
                    When = new LootCondition { RequiresTags = new[] { "elite" } },
                },
                new LootEntryDefinition
                {
                    ItemId = "material.trash",
                    When = new LootCondition { ExcludesTags = new[] { "elite" } },
                },
            },
        };
        var resolver = Resolver(new ScriptedRandom(), table);

        Assert.Equal("material.trash", Assert.Single(resolver.Roll("loot.t", new LootContext()).Drops).ItemId);
        Assert.Equal(
            "material.elite_prize",
            Assert.Single(resolver.Roll("loot.t", new LootContext(tags: new[] { "elite" })).Drops).ItemId);
    }

    /// <summary>Source identity: a table's own tags reach everything nested below it. This is
    /// what lets one shared anatomy table serve creatures that must not all drop the same
    /// parts, without a table per creature.</summary>
    [Fact]
    public void ATablesOwnTagsReachItsNestedTables()
    {
        var resolver = Resolver(
            new ScriptedRandom(),
            new LootTableDefinition
            {
                Id = "loot.beast", Name = "Beast",
                Tags = new[] { "source:beast" },
                AlwaysDrops = new[] { new LootEntryDefinition { TableId = "loot.shared" } },
            },
            new LootTableDefinition
            {
                Id = "loot.construct", Name = "Construct",
                Tags = new[] { "source:construct" },
                AlwaysDrops = new[] { new LootEntryDefinition { TableId = "loot.shared" } },
            },
            new LootTableDefinition
            {
                Id = "loot.shared", Name = "Shared",
                AlwaysDrops = new[]
                {
                    new LootEntryDefinition
                    {
                        ItemId = "material.gland",
                        When = new LootCondition { RequiresTags = new[] { "source:beast" } },
                    },
                },
            });

        Assert.Equal("material.gland", Assert.Single(resolver.Roll("loot.beast", new LootContext()).Drops).ItemId);
        Assert.True(resolver.Roll("loot.construct", new LootContext()).IsEmpty);
    }

    // --- Rarity: read, never authored twice ---------------------------------

    [Fact]
    public void RarityComesFromTheMaterialsOwnTag()
    {
        var materials = new DataStore<MaterialDefinition>();
        materials.Add(new MaterialDefinition
        {
            Id = "material.gem", Name = "Gem",
            Tags = new[] { "origin:mineral", "comp:inorganic", "form:gem", "state:raw", "rarity:very_rare" },
        });

        var resolver = Resolver(
            new ScriptedRandom(), materials,
            new LootTableDefinition
            {
                Id = "loot.t", Name = "T",
                // The entry claims nothing; the tag is authoritative.
                AlwaysDrops = new[] { Item("material.gem") },
            });

        var drop = Assert.Single(resolver.Roll("loot.t", new LootContext()).Drops);
        Assert.Equal(LootRarity.VeryRare, drop.Rarity);
    }

    [Fact]
    public void AnEntryMayDeclareRarityForSomethingThatCarriesNoTag()
    {
        var resolver = Resolver(
            new ScriptedRandom(),
            new LootTableDefinition
            {
                Id = "loot.t", Name = "T",
                AlwaysDrops = new[] { new LootEntryDefinition { ItemId = "technique.manual", Rarity = LootRarity.Rare } },
            });

        Assert.Equal(LootRarity.Rare, Assert.Single(resolver.Roll("loot.t", new LootContext()).Drops).Rarity);
    }

    // --- Gold ---------------------------------------------------------------

    [Fact]
    public void GoldRollsItsChanceAndItsRange()
    {
        var table = new LootTableDefinition
        {
            Id = "loot.t", Name = "T",
            Gold = new GoldDropDefinition { MinAmount = 5, MaxAmount = 9, Chance = 0.5 },
        };

        var paid = Resolver(new ScriptedRandom(fallbackDouble: 0.1, rollHighQuantities: true), table)
            .Roll("loot.t", new LootContext());
        Assert.Equal(9, paid.Gold);

        var stiffed = Resolver(new ScriptedRandom(fallbackDouble: 0.9), table).Roll("loot.t", new LootContext());
        Assert.Equal(0, stiffed.Gold);
    }

    [Fact]
    public void DepositingBanksStacksAndCoinIntoTheSameBag()
    {
        var resolver = Resolver(
            new ScriptedRandom(),
            new LootTableDefinition
            {
                Id = "loot.t", Name = "T",
                AlwaysDrops = new[] { Item("material.ore", 3, 3) },
                Gold = new GoldDropDefinition { MinAmount = 11, MaxAmount = 11 },
            });

        var bag = new Inventory();
        resolver.Roll("loot.t", new LootContext()).DepositInto(bag);

        Assert.Equal(3, bag.GetQuantity("material.ore"));
        Assert.Equal(11, bag.Gold);
    }

    // --- Determinism --------------------------------------------------------

    [Fact]
    public void TheSameSeedProducesTheSameHaul()
    {
        LootTableDefinition Table() => new()
        {
            Id = "loot.t", Name = "T",
            ChanceDrops = new[] { new LootEntryDefinition { ItemId = "material.a", Chance = 0.5 } },
            WeightedDraws = new[]
            {
                new LootDrawDefinition
                {
                    Picks = 3,
                    Entries = new[]
                    {
                        new LootEntryDefinition { ItemId = "material.b", Weight = 40 },
                        new LootEntryDefinition { ItemId = "material.c", Weight = 30 },
                        new LootEntryDefinition { DropsNothing = true, Weight = 30 },
                    },
                },
            },
            Gold = new GoldDropDefinition { MinAmount = 1, MaxAmount = 20, Chance = 0.8 },
        };

        string Describe(int seed)
        {
            var result = Resolver(new SeededRandom(seed), Table()).Roll("loot.t", new LootContext());
            return string.Join(",", result.Drops.Select(d => $"{d.ItemId}x{d.Quantity}")) + $"|{result.Gold}";
        }

        Assert.Equal(Describe(4242), Describe(4242));
    }
}
