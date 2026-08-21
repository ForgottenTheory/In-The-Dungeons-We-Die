using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Professions;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Persistence;

/// <summary>
/// Save schema v12 — identity-model emergent materials (migration Phase 2c, D42).
///
/// <para>Since v14 (Phase 7, D54) the registry is identity-model only — the property-model
/// split of the coexistence era is gone with the system that needed it.</para>
/// </summary>
public class IdentityArchetypeSaveV12Tests
{
    [Fact]
    public void ANewSaveIsWrittenAtSchemaFourteen()
    {
        // v12 added identity archetypes; v13 the identity-minted item fields; v14 settled the
        // schema on the identity model alone (Phase 7, D49/D54 — items reset on older loads).
        // Bumping this pin is the conscious act the pin exists to force.
        Assert.Equal(14, SaveData.CurrentSchemaVersion);
    }

    [Fact]
    public void TheEightFacetsSurviveASaveAndLoad()
    {
        var state = new IdentityMaterialState
        {
            Identities = new[] { new IdentityStake("identity.dense", 1), new IdentityStake("identity.vital", 2) },
            Latent = new[] { "identity.ember" },
            Capacity = 2,
            Condition = Condition.Strained,
            Quality = 70,
            IsCarrier = false,
            Roots = new[]
            {
                new ProvenanceRoot("material.iron_ingot", 0.85),
                new ProvenanceRoot("material.oak", 0.15),
            },
        };
        var minted = new MaterialDefinition
        {
            Id = "emergent.f00dcafe", Name = "Vital Oakbound Iron Ingot",
            Tags = new[] { "form:metal", "form:ingot", "state:refined" },
            Capacity = 2, IdentityState = state,
        };
        var registry = new EmergentRegistry(new DataStore<MaterialDefinition>());
        registry.GetOrRegister(minted.Id, () => minted);

        var save = RoundTrip(Capture(registry));

        var restoredRegistry = new EmergentRegistry(new DataStore<MaterialDefinition>());
        Apply(save, restoredRegistry);

        Assert.True(restoredRegistry.TryGet("emergent.f00dcafe", out var restored));
        Assert.Equal("Vital Oakbound Iron Ingot", restored.Name);
        var restoredState = restored.IdentityState!;
        Assert.Equal(
            Fingerprint.Canonical(state, minted.Tags),
            Fingerprint.Canonical(restoredState, restored.Tags));
        Assert.Equal(Condition.Strained, restoredState.Condition);
        Assert.Equal(Stability.Stable, restoredState.Stability); // derived, never stored
    }

    [Fact]
    public void AV11SaveLoadsWithNoIdentityArchetypes()
    {
        var serializer = new SaveSerializer();
        var save = serializer.Deserialize("""{ "SchemaVersion": 11 }""")!;

        Assert.NotNull(save.IdentityArchetypes);
        Assert.Empty(save.IdentityArchetypes);

        var registry = new EmergentRegistry(new DataStore<MaterialDefinition>());
        Apply(save, registry); // and applying it is a no-op, not a failure
        Assert.Equal(0, registry.Count);
    }

    // --- Harness -------------------------------------------------------------

    private static ProfessionSystem MakeProfessions(Inventory bag) =>
        new(new DataStore<ProfessionActionDefinition>(), bag, new SeededRandom(1));

    private static SaveData RoundTrip(SaveData save)
    {
        var serializer = new SaveSerializer();
        return serializer.Deserialize(serializer.Serialize(save))!;
    }

    private static SaveData Capture(EmergentRegistry registry)
    {
        var stash = new Inventory();
        return SaveMapper.Capture(
            null, stash, MakeProfessions(stash), new DiscoverySystem(),
            new Dictionary<string, int>(), savedAtTick: 1, emergentRegistry: registry);
    }

    private static void Apply(SaveData save, EmergentRegistry registry)
    {
        var stash = new Inventory();
        SaveMapper.Apply(save, stash, MakeProfessions(stash), new DiscoverySystem(),
            new Dictionary<string, int>(), emergentRegistry: registry);
    }
}
