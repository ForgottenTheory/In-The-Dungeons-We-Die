using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Loot;
using Dungeons.Randomness;
using Dungeons.Realms;
using Xunit;
using Xunit.Abstractions;

namespace Dungeons.Tests.Realms;

/// <summary>
/// The Dark Forest as a <em>place</em> rather than a systems test map (Phase 6).
///
/// <para>Every test here is a claim about the experience: that leaving is always a decision,
/// that going deeper is worth it, that knowing the realm changes what you can do. If these pass
/// and it still is not fun, the numbers are wrong — but the shape is right.</para>
/// </summary>
public class DarkForestTests
{
    private readonly ITestOutputHelper _output;

    public DarkForestTests(ITestOutputHelper output) => _output = output;

    private static RealmDefinition DarkForest() =>
        TestPaths.LoadStore<RealmDefinition>("realms").GetById("realm.dark_forest");

    private static IEnumerable<int> Depths(RealmDefinition realm) =>
        realm.Locations.Select(l => l.Depth).Distinct().OrderBy(d => d);

    // ---- The extraction question, asked at every depth -----------------------------------------

    /// <summary>
    /// <b>The core loop, as a structural guarantee.</b> Every depth must offer a way out AND a
    /// way deeper (except the last, which is the end of the road) — otherwise "extract or push
    /// on" is not a decision the player gets to make there, it is just where the realm ends.
    /// </summary>
    [Fact]
    public void EveryDepthOffersBothLeavingAndGoingDeeper()
    {
        var realm = DarkForest();
        var depths = Depths(realm).ToList();

        foreach (var depth in depths)
        {
            var here = realm.Locations.Where(l => l.Depth == depth).ToList();

            Assert.True(here.Any(l => l.Type == RealmLocationType.Extraction),
                $"depth {depth} has no way out — extraction stops being a choice there.");

            if (depth != depths[^1])
                Assert.True(here.Any(l => l.Type == RealmLocationType.Descent),
                    $"depth {depth} has no way deeper.");
        }
    }

    /// <summary>Depth 1 carries two ways out on purpose: leaving should be something the player
    /// keeps choosing, not something that happens once at the end of a run.</summary>
    [Fact]
    public void TheShallowsOfferMoreThanOneWayOut()
    {
        var exits = DarkForest().Locations
            .Count(l => l.Depth == 1 && l.Type is RealmLocationType.Extraction or RealmLocationType.Descent);

        Assert.True(exits >= 2, $"depth 1 has {exits} exit(s); leaving should be a repeated decision.");
    }

    // ---- Depth means something ------------------------------------------------------------------

    /// <summary>
    /// Going deeper must be worth it, measured as <b>rarity, not variety</b>.
    ///
    /// <para>The first cut of this test counted distinct reachable drops and failed: depth 1 has
    /// more nodes and its tables nest into the broad shared ones, so the shallows "win" on
    /// breadth while paying in bark and scrap. Breadth is not reward. What the player actually
    /// goes deeper for is the rare thing, so that is what is asserted.</para>
    /// </summary>
    [Fact]
    public void DeeperGroundPaysRarer()
    {
        var realm = DarkForest();
        var tables = TestPaths.LoadStore<LootTableDefinition>("loot_tables");
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");

        var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["rarity:common"] = 0, ["rarity:uncommon"] = 1, ["rarity:rare"] = 2,
            ["rarity:very_rare"] = 3, ["rarity:exceptional"] = 4,
        };

        double AverageRarity(int depth)
        {
            var reachable = realm.Locations
                .Where(l => l.Depth == depth && !string.IsNullOrEmpty(l.LootTableId))
                .SelectMany(l => LootReachability.ItemsReachableFrom(tables, l.LootTableId!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(materials.Contains)
                .Select(id => materials.GetById(id).Tags
                    .Select(t => ranks.TryGetValue(t, out var r) ? r : -1)
                    .FirstOrDefault(r => r >= 0))
                .ToList();

            return reachable.Count == 0 ? 0 : reachable.Average();
        }

        var shallow = AverageRarity(1);
        var deep = AverageRarity(3);

        _output.WriteLine($"average rarity of reachable drops — depth 1: {shallow:0.00}, depth 3: {deep:0.00}");
        Assert.True(deep > shallow,
            $"depth 3 averages {deep:0.00} rarity against depth 1's {shallow:0.00} — going deeper must pay better.");
    }

    /// <summary>The realm must escalate in danger too, not just in loot: an elite deep in, and a
    /// boss deeper still.</summary>
    [Fact]
    public void TheRealmEscalatesToAnEliteAndThenABoss()
    {
        var realm = DarkForest();
        var actors = TestPaths.LoadStore<ActorDefinition>("actors");

        int DepthOfRankedFight(string rank) => realm.Locations
            .Where(l => l.Type == RealmLocationType.Combat && l.ActorId is not null)
            .Where(l => actors.GetById(l.ActorId!).Tags.Contains(rank, StringComparer.OrdinalIgnoreCase))
            .Select(l => l.Depth)
            .DefaultIfEmpty(0)
            .Max();

        var elite = DepthOfRankedFight("elite");
        var boss = DepthOfRankedFight("boss");

        Assert.True(elite > 1, "the elite should not be standing in the shallows.");
        Assert.True(boss > elite, $"the boss (depth {boss}) must sit deeper than the elite (depth {elite}).");
    }

    /// <summary>
    /// The rank seam, finally occupied. `loot.shared.rank_spoils` gates on the elite/boss tags
    /// and has been carried by every family table since the loot pass without a single actor
    /// wearing one — this proves the wiring pays out rather than merely existing.
    /// </summary>
    [Fact]
    public void RankedKillsActuallyPayRankSpoils()
    {
        var content = new ContentBundle
        {
            LootTables = TestPaths.LoadStore<LootTableDefinition>("loot_tables"),
            Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
        };
        var resolver = new LootResolver(content, new SeededRandom(7));
        var actors = TestPaths.LoadStore<ActorDefinition>("actors");
        var families = TestPaths.LoadStore<EnemyFamilyDefinition>("enemy_families");
        var roles = TestPaths.LoadStore<CombatRoleDefinition>("enemy_roles");
        var profiles = TestPaths.LoadStore<AiProfileDefinition>("ai_profiles");

        foreach (var id in new[] { "actor.goblin_warlord", "actor.thornheart" })
        {
            var resolved = ActorResolver.Resolve(actors.GetById(id), families, roles, profiles);
            var haul = resolver.Roll(resolved.LootTableIds, new LootContext(depth: 3, tags: resolved.Tags));

            Assert.False(haul.IsEmpty, $"{id} rolled nothing.");
            _output.WriteLine($"{resolved.Name}: {string.Join(", ", haul.Drops.Select(d => d.ItemId))}");
        }
    }

    // ---- Realm Knowledge changes the map --------------------------------------------------------

    /// <summary>
    /// The hidden nodes are the payoff for learning the place. A party that has not earned
    /// <see cref="RealmInsight.HiddenRoutes"/> must not be able to see them <em>or</em> walk to
    /// them — one rule, so the map and the movement can never disagree.
    /// </summary>
    [Fact]
    public void HiddenNodesAreInvisibleUntilTheRoutesAreKnown()
    {
        var realm = DarkForest();
        var hidden = realm.Locations.Where(l => l.Hidden).ToList();
        Assert.True(hidden.Count >= 3, $"only {hidden.Count} hidden node(s) — knowledge has little to reveal.");

        var required = RealmKnowledgeLevels.Required[RealmInsight.HiddenRoutes];
        var novice = new RealmRun(realm, tier: 1, knowledge: required - 1);
        var veteran = new RealmRun(realm, tier: 1, knowledge: required);

        foreach (var node in hidden.Where(l => l.Depth == 1))
        {
            Assert.False(novice.IsReachable(node), $"{node.Id} is visible to a party that has not learned the routes.");
            Assert.True(veteran.IsReachable(node), $"{node.Id} stays hidden even at {required} knowledge.");
        }

        // And the destination list agrees with the reachability rule.
        Assert.DoesNotContain(novice.Destinations(), d => d.Hidden);
    }

    /// <summary>
    /// Knowledge unlocks <b>options, never damage</b> (GDD §11.4). Asserted as an ordering: the
    /// arc is learn what lives here → where it is dangerous → where it is worth working → the
    /// ways through → the ways out, and knowing every exit comes last because that is what lets
    /// a player push deep on purpose.
    /// </summary>
    [Fact]
    public void KnowledgeUnlocksInTheIntendedOrder()
    {
        // The arc of learning a place: what it is made of, then what lives here, then where it
        // hurts, then where it pays, then the ways through, then the ways out — and last, once
        // you know the doors, the right to come in through a deeper one (Phase 8, GDD §11.4).
        var order = new[]
        {
            RealmInsight.CommonResources, RealmInsight.EnemyWeaknesses, RealmInsight.Hazards,
            RealmInsight.RichNodes, RealmInsight.HiddenRoutes, RealmInsight.ExtractionRoutes,
            RealmInsight.DeepEntry,
        };

        var thresholds = order.Select(i => RealmKnowledgeLevels.Required[i]).ToList();
        Assert.Equal(thresholds.OrderBy(t => t), thresholds);

        Assert.Empty(RealmKnowledgeLevels.Unlocked(0));
        Assert.Equal(order, RealmKnowledgeLevels.Unlocked(thresholds[^1]));
        Assert.Null(RealmKnowledgeLevels.Next(thresholds[^1]));
        Assert.Equal(RealmInsight.CommonResources, RealmKnowledgeLevels.Next(0)!.Value.Insight);
    }

    /// <summary>Extraction routes stay unknown until earned — that ignorance is what the insight
    /// is worth paying for.</summary>
    [Fact]
    public void WhereTheExitsAreIsSomethingYouLearn()
    {
        var realm = DarkForest();
        var required = RealmKnowledgeLevels.Required[RealmInsight.ExtractionRoutes];

        Assert.Empty(new RealmRun(realm, tier: 1, knowledge: required - 1).KnownExtractions());
        Assert.NotEmpty(new RealmRun(realm, tier: 1, knowledge: required).KnownExtractions());
    }

    // ---- The place is furnished -----------------------------------------------------------------

    /// <summary>
    /// Phase 6's shopping list, as one assertion: the realm carries every node kind the design
    /// asks for. A realm missing its camp or its merchant is still a systems test map.
    /// </summary>
    [Fact]
    public void TheRealmCarriesEveryKindOfPlace()
    {
        var kinds = DarkForest().Locations.Select(l => l.Type).ToHashSet();

        foreach (var required in new[]
                 {
                     RealmLocationType.Entrance, RealmLocationType.Travel, RealmLocationType.Combat,
                     RealmLocationType.Gather, RealmLocationType.Event, RealmLocationType.Descent,
                     RealmLocationType.Extraction, RealmLocationType.Camp, RealmLocationType.Shrine,
                     RealmLocationType.Merchant, RealmLocationType.Hazard,
                 })
            Assert.Contains(required, kinds);
    }

    /// <summary>A hazard the party can walk around for free is not a hazard, and one that never
    /// ends is not survivable. Both halves are content mistakes the shape can catch.</summary>
    [Fact]
    public void EveryHazardCostsSomethingAndEveryCampGivesSomethingBack()
    {
        foreach (var location in DarkForest().Locations)
        {
            if (location.Type == RealmLocationType.Hazard)
                Assert.True(location.HazardDamage > 0, $"{location.Id} costs nothing to cross.");
            if (location.Type == RealmLocationType.Camp)
                Assert.InRange(location.RestoreFraction, 0.01, 1.0);
            if (location.Type == RealmLocationType.Merchant)
                Assert.True(location.Cost > 0, $"{location.Id} gives its stock away.");
        }
    }
}
