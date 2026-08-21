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
/// <para>Same rule as every version before it: a v11 save loads with no identity archetypes,
/// which is the state of a player who never used the new bench. No migration step. The
/// sharper pin here is the SPLIT: the registry holds both models during coexistence, and
/// capturing used to throw the moment a new-model archetype appeared in it.</para>
/// </summary>
public class IdentityArchetypeSaveV12Tests
{
    [Fact]
    public void ANewSaveIsWrittenAtSchemaTwelve()
    {
        Assert.Equal(12, SaveData.CurrentSchemaVersion);
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
    public void BothModelsShareTheRegistryAndCapturingSplitsThemCleanly()
    {
        // The coexistence pin: capturing a registry holding a new-model archetype used to
        // throw ("has no profile to save") because the old mapper assumed every archetype
        // carried a property-model state.
        var registry = new EmergentRegistry(new DataStore<MaterialDefinition>());
        registry.GetOrRegister("emergent.old00001", () => new MaterialDefinition
        {
            Id = "emergent.old00001", Name = "Emberlit Iron",
            Tags = new[] { "form:metal" },
            State = new MaterialState(
                Properties: new PropertySet(new Dictionary<string, double> { ["hardness"] = 60 }),
                MaterialStrength: 40,
                Workability: 80,
                Lineage: new Lineage(
                    new List<RootShare> { new("material.iron_ingot", 1.0) }, 1, "process.forge_infusion", new List<string>()),
                Signature: "emergent.old00001"),
        });
        registry.GetOrRegister("emergent.new00001", () => new MaterialDefinition
        {
            Id = "emergent.new00001", Name = "Dense Granite",
            Tags = new[] { "form:stone", "state:refined" },
            Capacity = 1,
            IdentityState = new IdentityMaterialState
            {
                Identities = new[] { new IdentityStake("identity.dense", 1) },
                Capacity = 1,
                Roots = new[] { new ProvenanceRoot("material.granite", 1.0) },
            },
        });

        var save = RoundTrip(Capture(registry));

        Assert.Single(save.EmergentArchetypes);
        Assert.Single(save.IdentityArchetypes);

        var restoredRegistry = new EmergentRegistry(new DataStore<MaterialDefinition>());
        Apply(save, restoredRegistry);
        Assert.True(restoredRegistry.TryGet("emergent.old00001", out var oldModel));
        Assert.NotNull(oldModel.State);
        Assert.True(restoredRegistry.TryGet("emergent.new00001", out var newModel));
        Assert.NotNull(newModel.IdentityState);
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
