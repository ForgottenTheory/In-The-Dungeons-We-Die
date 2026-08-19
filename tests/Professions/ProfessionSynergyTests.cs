using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Randomness;
using Xunit;
using static Dungeons.Tests.Professions.ProfessionsTestData;

namespace Dungeons.Tests.Professions;

/// <summary>
/// Cross-profession and global bonuses (Phase 10) — the automation hooks the design has carried
/// as "Planned" since the first commit.
///
/// <para>The claim these tests hold is not "the numbers are right" (they are placeholders like
/// every other profession number). It is <b>structural</b>: a second source of the six benefit
/// quantities reaches the real execution path without <see cref="ActionResolver"/> or
/// <see cref="ProfessionSystem"/> learning a second vocabulary — which is what makes E6's worn
/// tools a third source and no change downstream.</para>
/// </summary>
public class ProfessionSynergyTests
{
    private const string Mining = "profession.mining";
    private const string Smithing = "profession.smithing";

    private static ProfessionActionDefinition Smelt => new()
    {
        Id = "action.smelt_iron",
        ProfessionId = Smithing,
        Name = "Smelt Iron",
        BaseIntervalTicks = 100,
        Experience = 5,
        Inputs = new[] { Amount("material.iron_ore", 2) },
        Outputs = new[] { Amount("material.iron_ingot") },
    };

    /// <summary>Rolls a fixed value, so a chance test is a statement rather than a sample.</summary>
    private sealed class FixedRandom : IRandomSource
    {
        private readonly double _value;
        public FixedRandom(double value) => _value = value;
        public double NextDouble() => _value;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }

    private static ProfessionBenefits BenefitsWith(
        ProfessionSynergyDefinition synergy,
        Func<string, int> levelOf,
        Func<int>? totalLevel = null) =>
        new(MasteryBenefits.None,
            new ProfessionSynergies(new[] { synergy }),
            levelOf,
            totalLevel ?? (() => 0));

    // --- The cross-profession hook -------------------------------------------

    [Fact]
    public void AnotherProfessionsLevelPaysIntoThisOnesWork()
    {
        var benefits = BenefitsWith(
            new ProfessionSynergyDefinition
            {
                Id = "synergy.mining_smithing",
                Kind = ProfessionBenefitKind.InputPreservation,
                SourceProfession = Mining,
                TargetProfession = Smithing,
                UnlockLevel = 10,
                PerLevel = 0.001,
                Max = 0.099,
            },
            levelOf: id => id == Mining ? 50 : 1);

        Assert.Equal(0.05, benefits.ValueOf(ProfessionBenefitKind.InputPreservation, Smithing, 0), 6);
    }

    [Fact]
    public void ASynergyPaysNothingToAProfessionItDoesNotName()
    {
        var benefits = BenefitsWith(
            new ProfessionSynergyDefinition
            {
                Id = "synergy.mining_smithing",
                Kind = ProfessionBenefitKind.InputPreservation,
                SourceProfession = Mining,
                TargetProfession = Smithing,
                PerLevel = 0.001,
                Max = 0.099,
            },
            levelOf: _ => 50);

        Assert.Equal(0.0, benefits.ValueOf(ProfessionBenefitKind.InputPreservation, "profession.fishing", 0));
    }

    [Fact]
    public void BelowItsUnlockLevelASynergyIsWorthNothing()
    {
        var benefits = BenefitsWith(
            new ProfessionSynergyDefinition
            {
                Id = "synergy.late",
                Kind = ProfessionBenefitKind.OutputDoubling,
                SourceProfession = Mining,
                TargetProfession = Smithing,
                UnlockLevel = 40,
                PerLevel = 0.001,
                Max = 0.099,
            },
            levelOf: _ => 39);

        Assert.Equal(0.0, benefits.ValueOf(ProfessionBenefitKind.OutputDoubling, Smithing, 0));
    }

    // --- The global hook ------------------------------------------------------

    /// <summary>
    /// A synergy with no source reads the player's <em>total</em> level. That is what makes a
    /// global bonus something earned across the whole roster rather than a constant hidden in a
    /// tuning class — and it is why one content type covers both hooks Phase 10 asked for.
    /// </summary>
    [Fact]
    public void AGlobalSynergyReadsTheTotalOfEveryProfession()
    {
        var benefits = BenefitsWith(
            new ProfessionSynergyDefinition
            {
                Id = "synergy.practised_hand",
                Kind = ProfessionBenefitKind.BonusOutputChance,
                SourceProfession = null,
                TargetProfession = null,
                UnlockLevel = 200,
                PerLevel = 0.00005,
                Max = 0.099,
            },
            levelOf: _ => 1,
            totalLevel: () => 600);

        // Reaches every profession, not just one.
        Assert.Equal(0.03, benefits.ValueOf(ProfessionBenefitKind.BonusOutputChance, Smithing, 0), 6);
        Assert.Equal(0.03, benefits.ValueOf(ProfessionBenefitKind.BonusOutputChance, "profession.fishing", 0), 6);
    }

    // --- Composition ----------------------------------------------------------

    /// <summary>Sources add. Two answers to "how much preservation" would be two balance models.</summary>
    [Fact]
    public void MasteryAndSynergyAddRatherThanOneWinning()
    {
        var mastery = new MasteryBenefits(new[]
        {
            new MasteryBenefitDefinition
            {
                Id = "mastery.preservation",
                Kind = ProfessionBenefitKind.InputPreservation,
                UnlockLevel = 1,
                PerLevel = 0.002,
                Max = 0.2,
                Description = "x",
            },
        });

        var benefits = new ProfessionBenefits(
            mastery,
            new ProfessionSynergies(new[]
            {
                new ProfessionSynergyDefinition
                {
                    Id = "synergy.mining_smithing",
                    Kind = ProfessionBenefitKind.InputPreservation,
                    SourceProfession = Mining,
                    TargetProfession = Smithing,
                    PerLevel = 0.001,
                    Max = 0.099,
                },
            }),
            levelOf: id => id == Mining ? 30 : 1,
            totalLevel: () => 0);

        // 50 mastery × 0.002 = 0.100, plus Mining 30 × 0.001 = 0.030.
        Assert.Equal(0.13, benefits.ValueOf(ProfessionBenefitKind.InputPreservation, Smithing, 50), 6);
    }

    /// <summary>
    /// The structural claim: a synergy changes what actually happens at the bench, through the
    /// one <see cref="ProfessionSystem.Execute"/> path — not through a parallel calculation the
    /// offline and online paths would then have to agree about.
    /// </summary>
    [Fact]
    public void ASynergyChangesTheOutcomeOfARealCompletion()
    {
        var inventory = new Inventory();
        inventory.Add("material.iron_ore", 2);

        // Rolls 0.05: below a 0.099 preservation chance, above nothing at all.
        var system = new ProfessionSystem(Store(Smelt), inventory, new FixedRandom(0.05))
        {
            Benefits = new ProfessionBenefits(
                MasteryBenefits.None,
                new ProfessionSynergies(new[]
                {
                    new ProfessionSynergyDefinition
                    {
                        Id = "synergy.mining_smithing",
                        Kind = ProfessionBenefitKind.InputPreservation,
                        SourceProfession = Mining,
                        TargetProfession = Smithing,
                        PerLevel = 0.001,
                        Max = 0.099,
                    },
                }),
                levelOf: id => id == Mining ? 99 : 1,
                totalLevel: () => 0),
        };

        var outcome = system.Execute("action.smelt_iron");

        Assert.True(outcome.InputsPreserved);
        Assert.Equal(2, inventory.GetQuantity("material.iron_ore")); // the ore survived
        Assert.Equal(1, inventory.GetQuantity("material.iron_ingot"));
    }

    /// <summary>Interval reduction is read by <see cref="ProfessionSystem.EffectiveIntervalTicks"/>,
    /// which is what both the live runner and the offline payout re-read every completion.</summary>
    [Fact]
    public void ASynergyShortensTheInterval()
    {
        var system = new ProfessionSystem(Store(Smelt), new Inventory(), new FakeRandom())
        {
            Benefits = new ProfessionBenefits(
                MasteryBenefits.None,
                new ProfessionSynergies(new[]
                {
                    new ProfessionSynergyDefinition
                    {
                        Id = "synergy.economy_of_motion",
                        Kind = ProfessionBenefitKind.IntervalReduction,
                        SourceProfession = null,
                        TargetProfession = null,
                        UnlockLevel = 1,
                        PerLevel = 0.0001,
                        Max = 0.1,
                    },
                }),
                levelOf: _ => 1,
                totalLevel: () => 1000),
        };

        Assert.Equal(90, system.EffectiveIntervalTicks("action.smelt_iron")); // 100 × (1 − 0.1)
    }

    // --- The shipped table ----------------------------------------------------

    /// <summary>
    /// Every shipped synergy follows a chain the professions actually have. A synergy between
    /// two professions that never touch would be a number pretending to be a relationship — the
    /// player would learn to read the table instead of the game.
    /// </summary>
    [Fact]
    public void EveryShippedSynergyNamesRealProfessions()
    {
        var professions = TestPaths.LoadStore<ProfessionDefinition>("professions");
        var synergies = TestPaths.LoadStore<ProfessionSynergyDefinition>("synergies");

        Assert.NotEmpty(synergies.GetAll());

        foreach (var synergy in synergies.GetAll())
        {
            if (synergy.SourceProfession is { } source)
                Assert.True(professions.Contains(source), $"{synergy.Id} names unknown source {source}");
            if (synergy.TargetProfession is { } target)
                Assert.True(professions.Contains(target), $"{synergy.Id} names unknown target {target}");
        }
    }

    /// <summary>
    /// The shipped table carries both hooks Phase 10 asked for. A table of only cross-profession
    /// rows would leave "global passive bonuses" as an unexercised code path, which is the
    /// condition every dead progression track in this project started in.
    /// </summary>
    [Fact]
    public void TheShippedTableCarriesBothCrossProfessionAndGlobalRows()
    {
        var synergies = TestPaths.LoadStore<ProfessionSynergyDefinition>("synergies").GetAll();

        Assert.Contains(synergies, synergy => !synergy.IsGlobalSource);
        Assert.Contains(synergies, synergy => synergy.IsGlobalSource);
    }
}
