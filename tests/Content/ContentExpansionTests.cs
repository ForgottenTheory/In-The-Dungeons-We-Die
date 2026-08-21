using Dungeons.Content;
using Dungeons.Realms;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// The plant / ore / realm expansion, checked as design rules rather than as counts.
///
/// <para>These entries were generated from name lists, which makes two failure modes cheap and
/// invisible: a library that is <em>tiered</em> (fancier name, bigger numbers) and a realm you
/// cannot walk. Both are asserted here.</para>
/// </summary>
public class ContentExpansionTests
{
    private static DataStore<MaterialDefinition> Materials() =>
        TestPaths.LoadStore<MaterialDefinition>("materials");

    private static DataStore<RealmDefinition> Realms() =>
        TestPaths.LoadStore<RealmDefinition>("realms");

    // ---- The library is a spread of profiles, not a ladder -------------------------------------

    /// <summary>
    /// <b>The anti-tiering rule, identity edition.</b> A name is a claim about what a material
    /// IS: the frost family must keep the Frost identity reachable somewhere in its ranks, the
    /// storm family Storm — or the fancy names have decayed into rungs of one ladder.
    /// </summary>
    [Theory]
    [InlineData("frost", "identity.frost")]
    [InlineData("ember", "identity.ember")]
    [InlineData("storm", "identity.storm")]
    [InlineData("grave", "identity.blighted")]
    [InlineData("venom", "identity.venomous")]
    public void AMaterialFamilysNameKeepsItsIdentityReachable(string namePrefix, string identityId)
    {
        var matching = Materials().GetAll()
            .Where(m => m.Name.Contains(namePrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(matching.Count >= 5, $"only {matching.Count} '{namePrefix}' materials — too few to be a claim.");

        Assert.True(matching.Any(m =>
                m.Identities.Any(grant => grant.Id == identityId)
                || m.Latent.Contains(identityId, StringComparer.Ordinal)),
            $"no '{namePrefix}' material carries {identityId}, active or latent — the name has stopped being a claim.");
    }

    /// <summary>
    /// The mundane majority is what makes the strange things legible. Real-world plants and
    /// minerals must stay ordinary — no magical identity, active or latent — or "Sage" and
    /// "Voidleaf" stop being different kinds of thing and become two rungs of one ladder.
    /// </summary>
    [Fact]
    public void TheRealWorldHalfOfTheLibraryStaysMundane()
    {
        var supernatural = new[] { "identity.arcane", "identity.resonant" };
        foreach (var id in new[]
                 {
                     "material.sage", "material.rosemary", "material.thyme", "material.carrot",
                     "material.barley", "material.elderberry", "material.black_pepper",
                     "material.limestone", "material.granite_x", "material.hematite", "material.galena",
                 })
        {
            if (!Materials().TryGetById(id, out var material))
                continue; // a few of these already existed under other ids

            foreach (var identityId in supernatural)
            {
                Assert.DoesNotContain(material.Identities, grant => grant.Id == identityId);
                Assert.DoesNotContain(identityId, material.Latent);
            }
        }
    }

    /// <summary>Rarity must be a spread, not a badge every new thing wears. If the library is
    /// mostly rare then rarity has stopped meaning anything.</summary>
    [Fact]
    public void MostOfTheLibraryIsStillCommon()
    {
        var all = Materials().GetAll();
        var common = all.Count(m => m.Tags.Contains("rarity:common", StringComparer.OrdinalIgnoreCase));
        var exceptional = all.Count(m => m.Tags.Contains("rarity:exceptional", StringComparer.OrdinalIgnoreCase));

        Assert.True(common * 3 >= all.Count, $"only {common} of {all.Count} materials are common — the library has tiered.");
        Assert.True(exceptional * 20 <= all.Count, $"{exceptional} exceptional materials is too many to be exceptional.");
    }

    // ---- Everything the expansion added can actually be obtained -------------------------------

    /// <summary>Everything a profession action, a loot table or a byproduct can hand the player.</summary>
    private static HashSet<string> Obtainable()
    {
        var obtainable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in TestPaths.LoadStore<Dungeons.Professions.ProfessionActionDefinition>("profession_actions").GetAll())
        {
            foreach (var output in action.Outputs) obtainable.Add(output.ItemId);
            foreach (var bonus in action.BonusOutputs) obtainable.Add(bonus.ItemId);
            foreach (var opportunity in action.Opportunities)
            {
                foreach (var output in opportunity.Outputs) obtainable.Add(output.ItemId);
                foreach (var bonus in opportunity.BonusOutputs) obtainable.Add(bonus.ItemId);
            }
        }

        foreach (var table in TestPaths.LoadStore<Dungeons.Loot.LootTableDefinition>("loot_tables").GetAll())
        foreach (var entry in table.AlwaysDrops.Concat(table.ChanceDrops)
                     .Concat(table.WeightedDraws.SelectMany(draw => draw.Entries)))
            if (entry.ItemId is { Length: > 0 } id)
                obtainable.Add(id);

        foreach (var byproduct in TestPaths.LoadStore<ByproductDefinition>("byproducts").GetAll())
            obtainable.Add(byproduct.Id);

        return obtainable;
    }

    /// <summary>
    /// <b>Every raw material in the library can be obtained.</b> Not a ratchet any more: the
    /// count is zero and the assertion is exact, so a material authored without a source fails
    /// the build rather than joining a backlog nobody re-reads.
    ///
    /// <para>"Obtained" means a profession action hands it over, a loot table drops it, or it is
    /// a byproduct. Where a material comes from is a design decision the test does not make —
    /// anatomy belongs on the creature it came off, and <b>essence must come out of a Realm</b>,
    /// because a profession that hands it over at the Hideout removes the reason to go.</para>
    /// </summary>
    [Fact]
    public void EveryRawMaterialHasASource()
    {
        var obtainable = Obtainable();

        var stranded = Materials().GetAll()
            .Where(m => m.Tags.Contains("state:raw", StringComparer.OrdinalIgnoreCase))
            .Where(m => !obtainable.Contains(m.Id))
            .Select(m => m.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.True(stranded.Count == 0,
            $"{stranded.Count} raw materials have no source: {string.Join(", ", stranded.Take(15))}");
    }

    // ---- Every realm is a place you can actually walk ------------------------------------------

    /// <summary>
    /// <c>RealmRun</c> starts at <c>EntranceForDepth(1)</c>, so a realm without one cannot be
    /// entered at all — it would load cleanly and fail the moment a player picked it.
    /// </summary>
    [Fact]
    public void EveryRealmCanBeEntered()
    {
        foreach (var realm in Realms().GetAll())
            Assert.True(realm.EntranceForDepth(1) is not null,
                $"{realm.Id} has no depth-1 entrance — it can be listed but never run.");
    }

    /// <summary>
    /// …and a way out. Extraction is the whole game's central decision; a realm you can enter
    /// and not leave is a trap rather than a run.
    /// </summary>
    [Fact]
    public void EveryRealmHasAWayOut()
    {
        foreach (var realm in Realms().GetAll())
            Assert.Contains(realm.Locations, l => l.Type == RealmLocationType.Extraction);
    }

    /// <summary>
    /// Every node must be reachable from its own depth's entrance.
    ///
    /// <para><b>Per depth, not per realm.</b> Each depth is a separate subgraph — descending
    /// does not traverse an edge, it drops you at <c>EntranceForDepth(depth + 1)</c> — so a
    /// whole-realm walk would report every deep node as stranded. The validator already proves
    /// edges are symmetric and point at real locations; symmetric edges still permit an island,
    /// and an island is content nobody can ever see.</para>
    /// </summary>
    [Fact]
    public void EveryLocationIsReachableFromItsOwnDepthsEntrance()
    {
        foreach (var realm in Realms().GetAll())
        foreach (var depth in realm.Locations.Select(l => l.Depth).Distinct())
        {
            var entrance = realm.EntranceForDepth(depth);
            Assert.True(entrance is not null, $"{realm.Id} has locations at depth {depth} and no entrance to them.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entrance!.Id };
            var queue = new Queue<string>();
            queue.Enqueue(entrance.Id);

            while (queue.Count > 0)
                foreach (var next in realm.GetLocation(queue.Dequeue()).Connections)
                    if (seen.Add(next))
                        queue.Enqueue(next);

            var stranded = realm.Locations
                .Where(l => l.Depth == depth && !seen.Contains(l.Id))
                .Select(l => l.Id)
                .ToList();

            Assert.True(stranded.Count == 0,
                $"{realm.Id} depth {depth} strands {string.Join(", ", stranded)}.");
        }
    }

    /// <summary>A depth you can reach must be a depth you can leave — otherwise "go deeper" is
    /// a one-way door and the extraction decision stops existing below the surface.</summary>
    [Fact]
    public void EveryDepthHasItsOwnWayOut()
    {
        foreach (var realm in Realms().GetAll())
        foreach (var depth in realm.Locations.Select(l => l.Depth).Distinct())
            Assert.Contains(realm.Locations,
                l => l.Depth == depth
                     && l.Type is RealmLocationType.Extraction or RealmLocationType.Descent);
    }

    /// <summary>The roster spans the whole tier range, so there is somewhere to go at every
    /// stage rather than a wall of level-one meadows.</summary>
    [Fact]
    public void TheRosterSpansEveryTier()
    {
        var covered = Realms().GetAll().SelectMany(r => r.SupportedTiers).ToHashSet();

        foreach (var tier in new[] { 1, 2, 3, 4, 5 })
            Assert.Contains(tier, covered);
    }
}
