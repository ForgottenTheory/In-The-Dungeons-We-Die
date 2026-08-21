using Dungeons.Content;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// The Phase 4 library migration (D52): the whole 1,448-material library carries identity
/// fields, the D46 structural discipline holds everywhere, essence-bearers carry their
/// D44-mapped identity, and the hand-authored starter six were never overwritten by the
/// derivation. These are the durable form of the migration rules — the tool that applied
/// them was deliberately thrown away.
/// </summary>
public class MaterialLibraryMigrationTests
{
    // --- Library-wide fences -------------------------------------------------

    [Fact]
    public void EveryShippedMaterialIsMigrated()
    {
        // D52: no cull, full coverage — a material without capacity is a material the
        // identity bench refuses, and after Phase 4 nothing shipped may be refused.
        var unmigrated = Materials().GetAll()
            .Where(material => material.Capacity is null)
            .Select(material => material.Id)
            .ToList();

        Assert.True(unmigrated.Count == 0,
            $"{unmigrated.Count} materials have no identity model: {string.Join(", ", unmigrated.Take(10))}…");
    }

    [Fact]
    public void TheHandAuthoredStarterSixWereNeverOverwritten()
    {
        // The derivation tool's first law: a material that already authors identity fields
        // is never touched. The Phase 2/3 starter set is the proof.
        var materials = Materials();

        void Pin(string id, int capacity, string[] latents, (int Heft, int Bite, int Toughness, int Give)? baseStats)
        {
            var material = materials.GetById(id);
            Assert.Equal(capacity, material.Capacity);
            Assert.Equal(latents, material.Latent);
            if (baseStats is { } expected)
            {
                Assert.NotNull(material.Base);
                Assert.Equal(expected.Heft, material.Base!.Heft);
                Assert.Equal(expected.Bite, material.Base.Bite);
                Assert.Equal(expected.Toughness, material.Base.Toughness);
                Assert.Equal(expected.Give, material.Base.Give);
            }
        }

        Pin("material.iron_ore", 2, Array.Empty<string>(), (6, 0, 4, 0));
        Pin("material.iron_ingot", 2, Array.Empty<string>(), (5, 6, 6, 0));
        Pin("material.granite", 1, new[] { "identity.dense" }, (7, 0, 6, 0));
        Pin("material.oak", 2, new[] { "identity.vital" }, (4, 0, 4, 6));
        Pin("material.sageleaf", 1, new[] { "identity.vital" }, null);
        Pin("material.leather", 1, Array.Empty<string>(), (1, 0, 2, 7));
    }

    [Fact]
    public void CapacityFollowsRarityWithTheMagicalBump()
    {
        // The D52 derivation rule, held library-wide as a ceiling-and-floor sanity band
        // rather than an exact equation — hand-tier work may deepen a material on purpose,
        // but nothing common holds four identities and nothing sits at zero.
        foreach (var material in Materials().GetAll())
        {
            Assert.NotNull(material.Capacity);
            var rarity = material.Tags.FirstOrDefault(tag => tag.StartsWith("rarity:", StringComparison.Ordinal));
            var isMagical = material.Tags.Contains("class:magical", StringComparer.Ordinal);
            var ceiling = rarity switch
            {
                "rarity:common" => isMagical ? 2 : 2,       // starter-set precedent: oak/iron sit at 2
                "rarity:uncommon" => isMagical ? 3 : 2,
                "rarity:rare" => isMagical ? 3 : 3,
                "rarity:very_rare" => 4,
                "rarity:exceptional" => 4,
                _ => 4,
            };
            Assert.InRange(material.Capacity!.Value, 1, ceiling);
        }
    }

    [Fact]
    public void EveryIdentityHasAtLeastOneNaturalSource()
    {
        // The roster is only real if the world carries it: all 24 identities must be
        // reachable through some shipped material, active or latent — otherwise a family is
        // dead content wearing a name. The hand tier (D52) filled the last gaps: Earthen in
        // elemental earth, Pure in the alchemical salts, Balanced in true-swinging shafts,
        // Charmed in folk-luck stock.
        var identities = TestPaths.LoadStore<IdentityDefinition>("identities").GetAll().Select(i => i.Id);
        var sourced = Materials().GetAll()
            .SelectMany(material => material.Identities.Select(grant => grant.Id).Concat(material.Latent))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var identityId in identities)
            Assert.True(sourced.Contains(identityId), $"{identityId} has no shipped material source.");
    }

    [Fact]
    public void EveryIdentityOwnsItsGuaranteedFloor()
    {
        // With the library migrated, any identity can reach an item — so every identity's
        // guaranteed expression must be authored (D50 category 1, forced library-wide by
        // D52). The payload validator holds exactly-one-per-owner; this holds at-least-one
        // for the whole roster.
        var identities = TestPaths.LoadStore<IdentityDefinition>("identities").GetAll().Select(i => i.Id);
        var floorOwners = TestPaths.LoadStore<SignaturePayloadDefinition>("signature_payloads").GetAll()
            .Where(payload => payload.Floor is not null)
            .SelectMany(payload => payload.Families.Select(family => family.Identity))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var identityId in identities)
            Assert.True(floorOwners.Contains(identityId), $"{identityId} has no authored floor expression.");
    }

    [Fact]
    public void ActiveIdentityStockComesOnlyFromChosenExposure()
    {
        // D29.3 translated to the identity model (D52): a GATHERING action's passive yield —
        // what completes while nobody watches — may carry latents (revealing one is bench
        // work), but never a material with ACTIVE identities. Active-identity stock is
        // earned through opportunities (active play), Realm loot (extraction risk), or
        // processing chains whose inputs already paid the price. Processing actions are
        // conversions, not faucets, and are exempt on purpose.
        var actions = TestPaths.LoadStore<Dungeons.Professions.ProfessionActionDefinition>("profession_actions").GetAll();
        var materials = Materials();
        var lootTables = TestPaths.LoadStore<Dungeons.Loot.LootTableDefinition>("loot_tables");

        bool BearsActiveIdentity(string id) =>
            materials.TryGetById(id, out var material) && material.Identities.Count > 0;

        foreach (var action in actions.Where(action => action.Inputs.Count == 0))
        {
            var passiveYield = action.Outputs.Select(output => output.ItemId)
                .Concat(action.BonusOutputs.Select(bonus => bonus.ItemId))
                .Concat(action.LootTableId is { Length: > 0 } table
                    ? Dungeons.Loot.LootReachability.ItemsReachableFrom(lootTables, table)
                    : Enumerable.Empty<string>());

            foreach (var itemId in passiveYield)
                Assert.False(BearsActiveIdentity(itemId),
                    $"{action.Id} passively yields '{itemId}', which carries an active identity — " +
                    "identity-rich stock is earned through chosen exposure, never banked while idle (D29.3/D52).");
        }
    }

    // --- The structural discipline, each rule proven to fire ------------------

    [Fact]
    public void AMigratedStructuralMaterialWithoutBaseStatsFails()
    {
        var bundle = new ContentBundle();
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test", Name = "Test", Tags = new[] { "form:metal" }, Capacity = 1,
        });

        AssertProblem(bundle, "authors no base stats");
    }

    [Fact]
    public void BaseStatsOnANonStructuralMaterialFail()
    {
        var bundle = new ContentBundle();
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test", Name = "Test", Tags = new[] { "form:fruit" },
            Capacity = 1, Base = new MaterialBaseStats { Heft = 2 },
        });

        AssertProblem(bundle, "not gear stock");
    }

    [Fact]
    public void BiteOnAMaterialThatCannotTakeAnEdgeFails()
    {
        var bundle = new ContentBundle();
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test", Name = "Test", Tags = new[] { "form:hide" },
            Capacity = 1, Base = new MaterialBaseStats { Toughness = 2, Bite = 3 },
        });

        AssertProblem(bundle, "cannot cut");
    }

    [Fact]
    public void AnUnmigratedStructuralMaterialIsExemptFromTheDiscipline()
    {
        // The coexistence seam: the rule binds migrated materials only — the old world's
        // stock is refused at the identity surfaces, not at load.
        var bundle = new ContentBundle();
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test", Name = "Test", Tags = new[] { "form:metal" },
        });

        var problems = ContentValidator.Validate(bundle);
        Assert.DoesNotContain(problems, p => p.Category == "material_identity");
    }

    // --- Harness -------------------------------------------------------------

    private static DataStore<MaterialDefinition> Materials() =>
        TestPaths.LoadStore<MaterialDefinition>("materials");

    private static void AssertProblem(ContentBundle bundle, string messageFragment)
    {
        var problems = ContentValidator.Validate(bundle);
        Assert.Contains(problems, p => p.Category == "material_identity" && p.Message.Contains(messageFragment));
    }
}
