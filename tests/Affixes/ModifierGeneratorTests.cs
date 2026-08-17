using Dungeons.Affixes;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Affixes;

/// <summary>
/// The §4 rolling pipeline and the three genetic levers (docs/affixes.md §3.3), against the
/// real shipped affix content. Deterministic given the seed — the casino is engineered first,
/// random second.
/// </summary>
public class ModifierGeneratorTests
{
    private static readonly DataStore<AffixDefinition> Affixes =
        TestPaths.LoadStore<AffixDefinition>("affixes");

    private static ItemPotential Weapon(
        double hardness = 0, double mass = 0, double heat = 0, double flexibility = 0,
        int materialStrength = 50, IReadOnlyDictionary<string, double>? essence = null) => new(
        "form.longsword",
        new Dictionary<string, double>
        {
            ["hardness"] = hardness, ["mass"] = mass, ["heat"] = heat, ["flexibility"] = flexibility,
        },
        essence ?? new Dictionary<string, double>(),
        Array.Empty<TraitInstance>(), Array.Empty<TraitInstance>(),
        new[] { "weapon", "sword", "metal" },
        materialStrength, 1, Array.Empty<string>());

    // ---- The three levers ---------------------------------------------------------------------

    [Fact]
    public void EligibilityIsAHardGate()
    {
        var brutal = Affixes.GetById("affix.brutal"); // requires mass ≥ 30 on a weapon

        Assert.True(ModifierGenerator.IsAvailableFor(brutal, Weapon(mass: 45), Array.Empty<string>()));
        Assert.False(ModifierGenerator.IsAvailableFor(brutal, Weapon(mass: 20), Array.Empty<string>()));
    }

    [Fact]
    public void WeightScalesWithPressure()
    {
        var brutal = Affixes.GetById("affix.brutal");

        var low = ModifierGenerator.ChanceWeightFor(brutal, Weapon(mass: 30));
        var high = ModifierGenerator.ChanceWeightFor(brutal, Weapon(mass: 80));

        Assert.True(high > low, $"weight should scale with materialInfluence ({low} → {high})");
    }

    [Fact]
    public void TheTierCeilingFollowsTheGenome()
    {
        var brutal = Affixes.GetById("affix.brutal");

        Assert.Equal(4, ModifierGenerator.MaximumModifierTier(brutal, Weapon(mass: 35))!.Tier);
        Assert.Equal(2, ModifierGenerator.MaximumModifierTier(brutal, Weapon(mass: 70))!.Tier);
        Assert.Equal(1, ModifierGenerator.MaximumModifierTier(brutal, Weapon(mass: 90))!.Tier);
    }

    [Fact]
    public void PotencyPositionsTheRollInsideTheTier()
    {
        // §3.3's fourth lever: same tier, higher material strength, higher value — no variance on innates.
        var keenEdge = Affixes.GetById("affix.innate_keen_edge");

        var weak = ModifierGenerator.Innates(Weapon(hardness: 60, materialStrength: 10), new[] { keenEdge }).Single();
        var strong = ModifierGenerator.Innates(Weapon(hardness: 60, materialStrength: 95), new[] { keenEdge }).Single();

        Assert.Equal(weak.Tier, strong.Tier);
        Assert.True(strong.Roll > weak.Roll, $"materialStrength should raise the roll ({weak.Roll} → {strong.Roll})");
    }

    // ---- Innates (D-21) -------------------------------------------------------------------------

    [Fact]
    public void InnatesAreDeterministicAndCapped()
    {
        var itemPotential = Weapon(hardness: 90, mass: 90, flexibility: 90, materialStrength: 80);

        var first = ModifierGenerator.Innates(itemPotential, Affixes.GetAll());
        var second = ModifierGenerator.Innates(itemPotential, Affixes.GetAll());

        Assert.Equal(first, second); // no randomness anywhere in the innate path
        Assert.True(first.Count is >= 1 and <= AffixTuning.MaxInnates);
    }

    [Fact]
    public void TracePressureEarnsNoInnate() =>
        Assert.Empty(ModifierGenerator.Innates(Weapon(hardness: 10, mass: 10), Affixes.GetAll()));

    // ---- Rolling (§4) ----------------------------------------------------------------------------

    [Fact]
    public void RollingIsDeterministicGivenTheSeed()
    {
        var itemPotential = Weapon(hardness: 70, mass: 60, materialStrength: 60);

        var first = ModifierGenerator.Roll(itemPotential, "prefix", Affixes.GetAll(), new SeededRandom(42));
        var second = ModifierGenerator.Roll(itemPotential, "prefix", Affixes.GetAll(), new SeededRandom(42));

        Assert.Equal(first, second);
    }

    [Fact]
    public void OneAffixPerFamilyPerItem()
    {
        var itemPotential = Weapon(hardness: 90, mass: 90, heat: 60, flexibility: 70, materialStrength: 70);

        for (var seed = 0; seed < 50; seed++)
        {
            var rolled = ModifierGenerator.Roll(itemPotential, "prefix", Affixes.GetAll(), new SeededRandom(seed));
            var families = rolled
                .Select(r => Affixes.GetById(r.AffixId).Family)
                .ToList();

            Assert.Equal(families.Count, families.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void AnIneligibleGenomeRollsNothingFromThatFamily()
    {
        // No heat materialInfluence → Searing (burn on hit) can never appear, at any seed.
        var itemPotential = Weapon(hardness: 90, mass: 90, materialStrength: 70);

        for (var seed = 0; seed < 80; seed++)
        {
            var rolled = ModifierGenerator.Roll(itemPotential, "prefix", Affixes.GetAll(), new SeededRandom(seed));
            Assert.DoesNotContain(rolled, r => r.AffixId == "affix.searing");
        }
    }

    /// <summary>Seeded distribution check (§8): higher-weight candidates appear more often
    /// across many rolls, and tier ceilings are never exceeded by any item potential.</summary>
    [Fact]
    public void DistributionFollowsWeightsAndCeilingsHold()
    {
        var itemPotential = Weapon(hardness: 88, mass: 40, materialStrength: 60); // keen-favoured, brutal-light
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var rng = new SeededRandom(7);

        for (var i = 0; i < 20_000; i++)
        {
            foreach (var rolled in ModifierGenerator.Roll(itemPotential, "prefix", Affixes.GetAll(), rng))
            {
                counts[rolled.AffixId] = counts.GetValueOrDefault(rolled.AffixId) + 1;

                var definition = Affixes.GetById(rolled.AffixId);
                var ceiling = ModifierGenerator.MaximumModifierTier(definition, itemPotential)!.Tier;
                Assert.True(rolled.Tier >= ceiling, $"{rolled.AffixId} rolled T{rolled.Tier} past ceiling T{ceiling}");

                var tier = definition.Tiers.Single(t => t.Tier == rolled.Tier);
                Assert.InRange(rolled.Roll, Math.Min(tier.Range[0], tier.Range[1]), Math.Max(tier.Range[0], tier.Range[1]));
            }
        }

        // The item potential shapes the pool: the heaviest eligible candidate by computed weight must
        // out-appear the lightest — expectation derived from ChanceWeightFor itself, never hardcoded.
        var eligible = Affixes.GetAll()
            .Where(d => d.Slot == "prefix" && ModifierGenerator.IsAvailableFor(d, itemPotential, Array.Empty<string>()))
            .Select(d => (d.Id, Weight: ModifierGenerator.ChanceWeightFor(d, itemPotential)))
            .Where(x => x.Weight > 0)
            .OrderByDescending(x => x.Weight)
            .ToList();
        Assert.True(eligible.Count >= 2, "the test itemPotential should support at least two prefixes");
        Assert.True(
            counts.GetValueOrDefault(eligible[0].Id) > counts.GetValueOrDefault(eligible[^1].Id),
            $"{eligible[0].Id} ({counts.GetValueOrDefault(eligible[0].Id)}) should out-appear "
            + $"{eligible[^1].Id} ({counts.GetValueOrDefault(eligible[^1].Id)})");
    }

    // ---- Grants ----------------------------------------------------------------------------------

    [Fact]
    public void StatGrantsBecomeContributionsAndDescriptionsSubstituteTheRoll()
    {
        var keen = Affixes.GetById("affix.keen");
        var rolled = new RolledAffix("affix.keen", 2, 0.07);

        var contribution = ModifierGrants.Contributions(rolled, keen, "test").Single();
        Assert.Equal("combat.crit.chance", contribution.Key);
        Assert.Equal(0.07, contribution.Value);

        Assert.Equal("7% critical chance.", ModifierGrants.Describe(rolled, keen));
    }

    [Fact]
    public void RuleGrantsSubstituteTheRollIntoChance()
    {
        var serrated = Affixes.GetById("affix.serrated");
        var rolled = new RolledAffix("affix.serrated", 3, 0.08);

        var rule = ModifierGrants.Rules(rolled, serrated).Single();
        Assert.Equal("DamageDealt", rule.Event);
        Assert.Equal(0.08, rule.Chance);
        Assert.Equal("applyStatus", rule.Effect.Kind);
        Assert.Equal("status.bleed", rule.Effect.Text);
    }
}
