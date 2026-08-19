using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Professions;

/// <summary>
/// Mastery, now that it does something.
///
/// <para>Before Phase 8 the GDD could say mastery was "a number that goes up and does nothing"
/// while four hardcoded constants were in fact reading it. The gap was that none of it could be
/// <em>rebalanced</em> and none of the Melvor layers the design promised — preservation,
/// doubling, unlocks — existed at all. These tests hold both halves: the numbers live in content,
/// and the two new benefits actually fire.</para>
/// </summary>
public class MasteryTests
{
    private static readonly MasteryBenefits Ladder = TestPaths.ShippedMasteryLadder();

    private const string Forestry = "profession.forestry";

    private static ProfessionActionDefinition ChopOak(params ItemStack[] inputs) => new()
    {
        Id = "action.chop_oak",
        ProfessionId = Forestry,
        Name = "Chop Oak",
        BaseIntervalTicks = 100,
        Experience = 10,
        Inputs = inputs,
        Outputs = new[] { new ItemStack("material.oak_log", 1) },
    };

    /// <summary>Rolls a fixed value, so a chance test is a statement rather than a sample.</summary>
    private sealed class FixedRandom : IRandomSource
    {
        private readonly double _value;
        public FixedRandom(double value) => _value = value;
        public double NextDouble() => _value;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }

    // --- Levels --------------------------------------------------------------

    /// <summary>
    /// One completion, one level, to a ceiling of 99. The linearity is the decision that kept
    /// Phase 8 from being a balance pass in disguise: today's per-point numbers are exactly
    /// today's per-level numbers, so nothing about how an action feels moved.
    /// </summary>
    [Fact]
    public void MasteryLevelIsCompletionsUpToTheCeiling()
    {
        Assert.Equal(0, MasteryLeveling.LevelFor(0));
        Assert.Equal(1, MasteryLeveling.LevelFor(1));
        Assert.Equal(47, MasteryLeveling.LevelFor(47));
        Assert.Equal(MasteryLeveling.MaxLevel, MasteryLeveling.LevelFor(99));
        Assert.Equal(MasteryLeveling.MaxLevel, MasteryLeveling.LevelFor(50_000));
        Assert.True(MasteryLeveling.IsMastered(99));
        Assert.False(MasteryLeveling.IsMastered(98));
    }

    // --- The ladder is content ----------------------------------------------

    /// <summary>Every kind the code consumes must be authored, or that consumer is dead in the
    /// shipped game. This is the rule <c>ContentValidator</c> deliberately skips for empty test
    /// fixtures, so it has to be asserted somewhere against the real file.</summary>
    [Fact]
    public void TheShippedLadderAuthorsEveryBenefitKind()
    {
        foreach (var kind in Enum.GetValues<MasteryBenefitKind>())
            Assert.True(Ladder.ValueOf(kind, Forestry, MasteryLeveling.MaxLevel) > 0,
                $"{kind} is worth nothing at full mastery — the code that reads it is dead.");
    }

    [Fact]
    public void EveryRungIsWorthMoreAtHigherMasteryUntilItCaps()
    {
        foreach (var kind in Enum.GetValues<MasteryBenefitKind>())
        {
            var low = Ladder.ValueOf(kind, Forestry, 50);
            var high = Ladder.ValueOf(kind, Forestry, MasteryLeveling.MaxLevel);
            Assert.True(high >= low, $"{kind} went backwards between mastery 50 and 99.");
        }
    }

    /// <summary>With no ladder wired nothing is worth anything — the magnitudes are content, and
    /// there is no quiet fallback table hiding in the code.</summary>
    [Fact]
    public void AnEmptyLadderBuysNothing()
    {
        foreach (var kind in Enum.GetValues<MasteryBenefitKind>())
            Assert.Equal(0.0, MasteryBenefits.None.ValueOf(kind, Forestry, MasteryLeveling.MaxLevel));
    }

    /// <summary>A profession's own rung wins over the general one, so a later balance pass can
    /// differentiate Mining from Fishing without a code change.</summary>
    [Fact]
    public void AProfessionScopedRungOverridesTheGeneralOne()
    {
        var ladder = new MasteryBenefits(new[]
        {
            new MasteryBenefitDefinition { Id = "general", Kind = MasteryBenefitKind.OutputDoubling, PerLevel = 0.001, Max = 1.0 },
            new MasteryBenefitDefinition { Id = "mining", Kind = MasteryBenefitKind.OutputDoubling, ProfessionId = "profession.mining", PerLevel = 0.01, Max = 1.0 },
        });

        Assert.Equal(0.10, ladder.ValueOf(MasteryBenefitKind.OutputDoubling, "profession.mining", 10), 6);
        Assert.Equal(0.01, ladder.ValueOf(MasteryBenefitKind.OutputDoubling, Forestry, 10), 6);
    }

    [Fact]
    public void ABenefitIsWorthNothingBelowItsUnlockLevel()
    {
        var ladder = new MasteryBenefits(new[]
        {
            new MasteryBenefitDefinition
            {
                Id = "late", Kind = MasteryBenefitKind.InputPreservation, UnlockLevel = 20, PerLevel = 0.01, Max = 1.0,
            },
        });

        Assert.Equal(0.0, ladder.ValueOf(MasteryBenefitKind.InputPreservation, Forestry, 19));
        Assert.Equal(0.20, ladder.ValueOf(MasteryBenefitKind.InputPreservation, Forestry, 20), 6);
    }

    // --- Preservation: the inputs survive -----------------------------------

    [Fact]
    public void HighMasteryCanSaveTheInputs()
    {
        var inputs = new[] { new ItemStack("material.oak_log", 1) };
        var bag = new Inventory();
        bag.Add("material.oak_log", 5);

        var system = new ProfessionSystem(Store(ChopOak(inputs)), bag, new FixedRandom(0.0))
        {
            MasteryBenefits = Ladder,
        };
        system.GetProgress(Forestry).AddMastery("action.chop_oak", MasteryLeveling.MaxLevel);

        var outcome = system.Execute("action.chop_oak");

        Assert.True(outcome.InputsPreserved);
        Assert.Empty(outcome.Consumed);

        // 5 banked, none spent, and the attempt made 2 — at full mastery with an all-succeed
        // roll, doubling fires alongside preservation. Both benefits landing on one completion
        // is the point of them being separate rungs.
        Assert.Equal(1, outcome.OutputsDoubled);
        Assert.Equal(7, bag.GetQuantity("material.oak_log"));
    }

    [Fact]
    public void WithoutMasteryTheInputsAreAlwaysSpent()
    {
        var inputs = new[] { new ItemStack("material.oak_log", 1) };
        var bag = new Inventory();
        bag.Add("material.oak_log", 5);

        var system = new ProfessionSystem(Store(ChopOak(inputs)), bag, new FixedRandom(0.0))
        {
            MasteryBenefits = Ladder,
        };

        var outcome = system.Execute("action.chop_oak");

        Assert.False(outcome.InputsPreserved);
        Assert.NotEmpty(outcome.Consumed);
    }

    /// <summary>
    /// A Farming harvest paid for its seed at planting time. Preserving inputs it no longer owes
    /// would be the system handing back something it never took.
    /// </summary>
    [Fact]
    public void APrepaidCompletionNeverReportsPreservedInputs()
    {
        var inputs = new[] { new ItemStack("material.oak_log", 1) };
        var system = new ProfessionSystem(Store(ChopOak(inputs)), new Inventory(), new FixedRandom(0.0))
        {
            MasteryBenefits = Ladder,
        };
        system.GetProgress(Forestry).AddMastery("action.chop_oak", MasteryLeveling.MaxLevel);

        var outcome = system.CompletePrepaidAction("action.chop_oak");

        Assert.True(outcome.Success);
        Assert.False(outcome.InputsPreserved);
    }

    // --- Doubling: the work yields twice ------------------------------------

    [Fact]
    public void HighMasteryCanDoubleTheOutputs()
    {
        var yield = ActionResolver.Resolve(
            ChopOak(), MasteryLeveling.MaxLevel, performance: 0, new FixedRandom(0.0), masteryBenefits: Ladder);

        Assert.Equal(1, yield.OutputsDoubled);
        Assert.Equal(2, yield.Produced.Count(stack => stack.ItemId == "material.oak_log"));
    }

    [Fact]
    public void WithoutMasteryNothingDoubles()
    {
        var yield = ActionResolver.Resolve(
            ChopOak(), mastery: 0, performance: 0, new FixedRandom(0.0), masteryBenefits: Ladder);

        Assert.Equal(0, yield.OutputsDoubled);
        Assert.Single(yield.Produced);
    }

    // --- The action-specific unlock -----------------------------------------

    private static ProfessionActionDefinition ActionOfferingAt(int requiredMastery) => new()
    {
        Id = "action.pick_pocket",
        ProfessionId = "profession.thieving",
        Name = "Pick a Pocket",
        Outputs = new[] { new ItemStack("material.native_gold", 1) },
        Opportunities = new[]
        {
            new ProfessionOpportunityDefinition
            {
                Id = "opportunity.the_reliquary",
                Name = "The Reliquary",
                Prompt = "…",
                DiscoveryChance = 1.0,
                RequiredMasteryLevel = requiredMastery,
                Outputs = new[] { new ItemStack("material.soul_gem", 1) },
                Experience = 10,
            },
        },
    };

    /// <summary>
    /// <b>Not rolled at all</b> below the gate, not merely unlikely. The same structural trick
    /// the active/passive seam uses: "a novice cannot find this" is a fact about the code rather
    /// than a very small probability nobody can verify.
    /// </summary>
    [Fact]
    public void AnOpportunityAboveThePartysMasteryIsNeverOffered()
    {
        var yield = ActionResolver.Resolve(
            ActionOfferingAt(40), mastery: 39, performance: 1.0, new FixedRandom(0.0),
            isActive: true, masteryBenefits: Ladder);

        Assert.Null(yield.Discovered);
    }

    [Fact]
    public void TheSameOpportunityIsOfferedOnceTheMasteryIsThere()
    {
        var yield = ActionResolver.Resolve(
            ActionOfferingAt(40), mastery: 40, performance: 1.0, new FixedRandom(0.0),
            isActive: true, masteryBenefits: Ladder);

        Assert.NotNull(yield.Discovered);
        Assert.Equal("opportunity.the_reliquary", yield.Discovered!.Id);
    }

    /// <summary>An ungated opportunity is the norm — the gate is the exception, and it must not
    /// have quietly become the default.</summary>
    [Fact]
    public void MostShippedOpportunitiesAreOpenToAnyone()
    {
        var actions = TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions");
        var opportunities = actions.GetAll().SelectMany(action => action.Opportunities).ToList();
        var gated = opportunities.Where(offer => offer.RequiredMasteryLevel > 0).ToList();

        Assert.NotEmpty(gated); // or the field is declared and unread, which is the debt it was written to avoid
        Assert.True(gated.Count < opportunities.Count / 2,
            $"{gated.Count} of {opportunities.Count} opportunities are mastery-gated; the gate is meant to be the exception.");
    }

    private static DataStore<ProfessionActionDefinition> Store(ProfessionActionDefinition action)
    {
        var store = new DataStore<ProfessionActionDefinition>();
        store.Add(action);
        return store;
    }
}
