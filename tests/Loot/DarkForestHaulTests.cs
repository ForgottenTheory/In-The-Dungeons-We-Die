using System.Text;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Loot;
using Dungeons.Randomness;
using Dungeons.Realms;
using Xunit;
using Xunit.Abstractions;

namespace Dungeons.Tests.Loot;

/// <summary>
/// The Phase 3 definition of done, as a test: a full Dark Forest run has to produce enough
/// different rewards that "push deeper or extract" is a real question.
///
/// <para>It is written to be <b>read</b> as much as run. <see cref="RenderAFullRun"/> prints the
/// haul source by source, so anyone tuning a table can see what the change actually did instead
/// of inferring it from a pass/fail — the method that has caught more real defects in this
/// codebase than any other.</para>
/// </summary>
public class DarkForestHaulTests
{
    private readonly ITestOutputHelper _output;

    public DarkForestHaulTests(ITestOutputHelper output) => _output = output;

    private static ContentBundle Content() => new()
    {
        LootTables = TestPaths.LoadStore<LootTableDefinition>("loot_tables"),
        Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
    };

    private static RealmDefinition DarkForest() =>
        TestPaths.LoadStore<RealmDefinition>("realms").GetById("realm.dark_forest");

    /// <summary>One pass through every source in the realm, at each depth, exactly as GameRoot
    /// would roll them: enemy tables composed family+role+actor, node tables on top of the
    /// profession action, event tables as the node itself.</summary>
    private static IReadOnlyList<(string Source, LootResult Haul)> WalkTheRealm(int seed)
    {
        var content = Content();
        var resolver = new LootResolver(content, new SeededRandom(seed));
        var realm = DarkForest();
        var actors = TestPaths.LoadStore<ActorDefinition>("actors");
        var families = TestPaths.LoadStore<EnemyFamilyDefinition>("enemy_families");
        var roles = TestPaths.LoadStore<CombatRoleDefinition>("enemy_roles");
        var aiProfiles = TestPaths.LoadStore<AiProfileDefinition>("ai_profiles");

        var hauls = new List<(string, LootResult)>();

        foreach (var location in realm.Locations.OrderBy(l => l.Depth))
        {
            var circumstances = new LootContext(
                depth: location.Depth, tier: 1,
                tags: new[] { LootContextTags.InRealm, realm.Id }.Concat(realm.Tags));

            if (location.Type == RealmLocationType.Combat && location.ActorId is { Length: > 0 } actorId)
            {
                var actor = ActorResolver.Resolve(actors.GetById(actorId), families, roles, aiProfiles);
                hauls.Add((actor.Name, resolver.Roll(
                    actor.LootTableIds, circumstances.With(actor.Tags))));
            }
            else if (location.LootTableId is { Length: > 0 } tableId)
            {
                hauls.Add((location.Name, resolver.Roll(tableId, circumstances)));
            }
        }

        return hauls;
    }

    [Fact]
    public void RenderAFullRun()
    {
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");
        var techniques = TestPaths.LoadStore<TechniqueDefinition>("techniques");

        string Name(string id) =>
            materials.TryGetById(id, out var material) ? material.Name
            : techniques.TryGetById(id, out var technique) ? technique.Name
            : id;

        var page = new StringBuilder();
        foreach (var seed in new[] { 11, 2027, 90210 })
        {
            page.AppendLine($"───── A run through the Dark Forest (seed {seed}) ─────");
            long gold = 0;
            var distinct = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (source, haul) in WalkTheRealm(seed))
            {
                gold += haul.Gold;
                foreach (var drop in haul.Drops)
                    distinct.Add(drop.ItemId);

                var line = haul.IsEmpty
                    ? "(nothing)"
                    : string.Join(", ", haul.Drops.Select(d =>
                        $"{Name(d.ItemId)} ×{d.Quantity}{(d.Rarity >= LootRarity.Rare ? $" [{d.Rarity}]" : string.Empty)}"))
                      + (haul.Gold > 0 ? $"  +{haul.Gold}g" : string.Empty);

                page.AppendLine($"  {source,-24} {line}");
            }

            page.AppendLine($"  → {distinct.Count} distinct materials, {gold} gold");
            page.AppendLine();
        }

        _output.WriteLine(page.ToString());
    }

    /// <summary>
    /// The bar Phase 3 set itself: one run has to come back with enough different things that
    /// deciding whether to push deeper is interesting. Three seeds, so this is a statement about
    /// the tables rather than about one lucky roll.
    /// </summary>
    [Theory]
    [InlineData(11)]
    [InlineData(2027)]
    [InlineData(90210)]
    public void OneRunComesBackWithAVariedHaul(int seed)
    {
        var hauls = WalkTheRealm(seed);
        var distinct = hauls.SelectMany(h => h.Haul.Drops).Select(d => d.ItemId).Distinct().ToList();

        Assert.True(distinct.Count >= 12,
            $"a full run brought back only {distinct.Count} different things: {string.Join(", ", distinct)}");
        Assert.True(hauls.Sum(h => h.Haul.Gold) > 0, "a full run brought back no coin at all.");

        // A gathering node paying nothing extra is fine and even wanted — its profession action
        // has already paid, and a node that always adds something makes the times it does add
        // something unremarkable. What would be wrong is most of the realm coming up dry.
        Assert.True(hauls.Count(h => !h.Haul.IsEmpty) * 2 > hauls.Count,
            $"only {hauls.Count(h => !h.Haul.IsEmpty)} of {hauls.Count} sources paid anything.");
    }

    /// <summary>Depth has to be worth it. Over a run's worth of rolls, the deep half of the
    /// realm must reach materials the shallow half cannot — otherwise "go deeper" is a pure
    /// risk with no reward, and the extraction decision is not a decision.</summary>
    [Fact]
    public void TheDeepHalfOfTheRealmPaysBetterThanTheShallowHalf()
    {
        var content = Content();
        var realm = DarkForest();

        IReadOnlyDictionary<LootRarity, int> RarityMixAtDepth(int depth)
        {
            var resolver = new LootResolver(content, new SeededRandom(4242));
            var counts = new Dictionary<LootRarity, int>();
            var tables = realm.Locations
                .Where(l => l.Depth == depth && !string.IsNullOrEmpty(l.LootTableId))
                .Select(l => l.LootTableId!)
                .ToList();

            for (var roll = 0; roll < 500; roll++)
            {
                foreach (var drop in resolver.Roll(tables, new LootContext(depth: depth, tier: 1)).Drops)
                    counts[drop.Rarity] = counts.GetValueOrDefault(drop.Rarity) + drop.Quantity;
            }

            return counts;
        }

        var shallow = RarityMixAtDepth(1);
        var deep = RarityMixAtDepth(2);

        var shallowRare = shallow.Where(pair => pair.Key >= LootRarity.Rare).Sum(pair => pair.Value);
        var deepRare = deep.Where(pair => pair.Key >= LootRarity.Rare).Sum(pair => pair.Value);

        Assert.True(deepRare > shallowRare,
            $"depth 2 returned {deepRare} rare-or-better against depth 1's {shallowRare} — the descent has to pay.");
    }
}
