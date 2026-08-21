using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The identity forge end to end (migration Phase 3, D50/D51): the mint consumes its
/// components, the derived definition stacks across identical mints, the instance carries
/// the three effect categories and the base delivery, the equipment-resolver seam consumes
/// it without combat changing, and the whole thing survives a save round-trip (v13).
/// </summary>
public class IdentityFabricationTests
{
    private const string Vital = "identity.vital";
    private const string Dense = "identity.dense";

    // --- The mint ------------------------------------------------------------

    [Fact]
    public void AMintConsumesComponentsAndDepositsTheInstance()
    {
        var forge = Forge();
        forge.Inventory.Add("material.iron_ingot", 2);
        forge.Inventory.Add("material.leather", 1);

        var result = forge.Engine.Fabricate(PlainLongsword());

        Assert.Null(result.GateFailure);
        Assert.Equal(IdentityCompositionFailure.None, result.CompositionFailure);
        Assert.NotNull(result.Item);
        Assert.True(result.FirstOfItsKind);
        Assert.False(forge.Inventory.Contains("material.iron_ingot", 1));
        Assert.False(forge.Inventory.Contains("material.leather", 1));
        Assert.Contains(forge.Inventory.Instances, instance => instance.InstanceId == result.Item!.InstanceId);
        // The noun is the deterministic variant pick (D34, carried across in Phase 7) — this
        // exact component set on a longsword reads as a Spatha, every time.
        Assert.Equal("Iron Spatha", result.Item!.DisplayName);
    }

    [Fact]
    public void IdenticalMintsShareOneDerivedDefinition()
    {
        var forge = Forge();
        forge.Inventory.Add("material.iron_ingot", 4);
        forge.Inventory.Add("material.leather", 2);

        var first = forge.Engine.Fabricate(PlainLongsword());
        var second = forge.Engine.Fabricate(PlainLongsword());

        Assert.Equal(first.Item!.BaseDefinitionId, second.Item!.BaseDefinitionId);
        Assert.True(first.FirstOfItsKind);
        Assert.False(second.FirstOfItsKind);
        Assert.NotEqual(first.Item.InstanceId, second.Item.InstanceId);
        Assert.StartsWith("equip.emergent.", first.Item.BaseDefinitionId, StringComparison.Ordinal);
    }

    [Fact]
    public void AMintedItemCarriesItsFloorAndItsIdentitySplit()
    {
        var forge = Forge();
        // Register an emergent Dense+Vital iron the way the verb bench would, then forge it.
        var denseVitalIron = ForgeableDenseVitalIron(forge.Content);
        forge.Inventory.Add(denseVitalIron, 2);
        forge.Inventory.Add("material.leather", 1);

        var result = forge.Engine.Fabricate(new IdentityFabricationInvocation(
            "form.longsword",
            new Dictionary<string, string>
            {
                ["edge"] = denseVitalIron, ["core"] = denseVitalIron, ["binding"] = "material.leather",
            }));

        var item = result.Item!;
        Assert.Equal(new[] { Dense, Vital }, item.ExpressedIdentities.Select(stake => stake.Id).OrderBy(id => id));
        Assert.Empty(item.DormantIdentities);
        Assert.Contains(item.IdentitySentences, s => s.Category == ItemEffectCategory.Floor && s.PayloadId == "impact");
        Assert.Contains(item.IdentitySentences, s => s.Category == ItemEffectCategory.Floor && s.PayloadId == "vitality");
        Assert.NotNull(item.BaseDelivery);
    }

    // --- Gates ---------------------------------------------------------------

    [Fact]
    public void TheForgeRefusesWhatIsNotOnHand()
    {
        var forge = Forge(); // an empty bag

        var result = forge.Engine.Fabricate(PlainLongsword());

        Assert.Equal(IdentityFabricationGateFailure.ComponentNotOnHand, result.GateFailure);
        Assert.Null(result.Item);
    }

    [Fact]
    public void TheForgeRefusesAnUnmigratedComponent()
    {
        // Since D52 every shipped material is migrated, so the coexistence seam is pinned
        // with an in-memory relic — the refusal must survive for modded or hand-added stock.
        var forge = Forge();
        forge.Content.Materials.Add(new MaterialDefinition
        {
            Id = "material.test_relic_ingot", Name = "Relic Ingot", Tags = new[] { "form:metal", "form:ingot" },
        });
        forge.Inventory.Add("material.iron_ingot", 2);
        forge.Inventory.Add("material.test_relic_ingot", 1);

        var result = forge.Engine.Fabricate(new IdentityFabricationInvocation(
            "form.longsword",
            new Dictionary<string, string>
            {
                ["edge"] = "material.iron_ingot", ["core"] = "material.test_relic_ingot", ["binding"] = "material.leather",
            }));

        Assert.Equal(IdentityFabricationGateFailure.ComponentNotMigrated, result.GateFailure);
    }

    // --- The equipment-resolver seam (combat unchanged) -----------------------

    [Fact]
    public void AMintedLongswordSwingsLikeTheAuthoredIronSword()
    {
        // The D46 calibration, proven through the live seam: the identity mint's resolved
        // moves match the authored Iron Sword's — same damage, same windup — so the mundane
        // floor really does carry the early game.
        var forge = Forge();
        forge.Inventory.Add("material.iron_ingot", 2);
        forge.Inventory.Add("material.leather", 1);
        var minted = forge.Engine.Fabricate(PlainLongsword()).Item!;

        var mintedDefinition = forge.Content.Equipment.GetById(minted.BaseDefinitionId);
        var authored = forge.Content.Equipment.GetById("equip.iron_sword");

        var mintedMoves = EquipmentResolver.ResolveWeaponMoves(mintedDefinition, minted, forge.Content.Moves);
        var authoredMoves = EquipmentResolver.ResolveWeaponMoves(authored, null, forge.Content.Moves);

        Assert.Equal(authoredMoves.Select(move => move.Id), mintedMoves.Select(move => move.Id));
        foreach (var (mintedMove, authoredMove) in mintedMoves.Zip(authoredMoves))
        {
            Assert.Equal(authoredMove.Packets.Sum(p => p.Amount), mintedMove.Packets.Sum(p => p.Amount), 1);
            Assert.Equal(authoredMove.Timing.WindupTicks, mintedMove.Timing.WindupTicks);
        }
    }

    [Fact]
    public void AMintedBucklerDeliversItsArmorThroughTheSeam()
    {
        var forge = Forge();
        forge.Inventory.Add("material.iron_ingot", 1);

        var minted = forge.Engine.Fabricate(new IdentityFabricationInvocation(
            "form.buckler", new Dictionary<string, string> { ["face"] = "material.iron_ingot" })).Item!;
        var definition = forge.Content.Equipment.GetById(minted.BaseDefinitionId);

        var profile = EquipmentResolver.ResolveArmor(definition, minted);

        Assert.Equal(minted.BaseDelivery!.Armor, profile.Armor);
        Assert.True(profile.Armor > 0);
    }

    // --- Persistence (v13) ---------------------------------------------------

    [Fact]
    public void AMintedItemSurvivesASaveRoundTrip()
    {
        var forge = Forge();
        var denseVitalIron = ForgeableDenseVitalIron(forge.Content);
        forge.Inventory.Add(denseVitalIron, 2);
        forge.Inventory.Add("material.leather", 1);
        var minted = forge.Engine.Fabricate(new IdentityFabricationInvocation(
            "form.longsword",
            new Dictionary<string, string>
            {
                ["edge"] = denseVitalIron, ["core"] = denseVitalIron, ["binding"] = "material.leather",
            })).Item!;
        Assert.NotEmpty(minted.IdentitySentences);

        var stash = new Dungeons.Items.Inventory();
        stash.AddInstance(minted);
        var serializer = new SaveSerializer();
        var save = serializer.Deserialize(serializer.Serialize(SaveMapper.Capture(
            null, stash,
            new Dungeons.Professions.ProfessionSystem(
                new DataStore<Dungeons.Professions.ProfessionActionDefinition>(), stash, new SeededRandom(1)),
            new DiscoverySystem(),
            new Dictionary<string, int>(), savedAtTick: 1,
            emergentEquipment: forge.Content.Equipment.GetAll()
                .Where(e => e.Id.StartsWith("equip.emergent.", StringComparison.Ordinal)))))!;

        var reloadedStash = new Dungeons.Items.Inventory();
        var equipmentStore = new DataStore<EquipmentDefinition>();
        SaveMapper.Apply(
            save, reloadedStash,
            new Dungeons.Professions.ProfessionSystem(
                new DataStore<Dungeons.Professions.ProfessionActionDefinition>(), reloadedStash, new SeededRandom(1)),
            new DiscoverySystem(),
            new Dictionary<string, int>(), equipmentStore: equipmentStore);

        var reloaded = Assert.Single(reloadedStash.Instances);
        Assert.Equal(minted.IdentitySentences, reloaded.IdentitySentences);
        Assert.Equal(minted.BaseDelivery, reloaded.BaseDelivery);
        Assert.Equal(minted.ExpressedIdentities, reloaded.ExpressedIdentities);
        Assert.Equal(minted.DormantIdentities, reloaded.DormantIdentities);
        Assert.True(equipmentStore.Contains(minted.BaseDefinitionId),
            "the derived definition must ride the emergent-equipment list, or the instance dangles after load.");
    }

    // --- Harness -------------------------------------------------------------

    private sealed record ForgeHarness(
        ContentBundle Content, Dungeons.Items.Inventory Inventory, IdentityFabricationEngine Engine);

    private static ForgeHarness Forge()
    {
        var content = new ContentBundle
        {
            Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
            Forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms"),
            Identities = TestPaths.LoadStore<IdentityDefinition>("identities"),
            SignatureTriggers = TestPaths.LoadStore<SignatureTriggerDefinition>("signature_triggers"),
            SignatureBehaviors = TestPaths.LoadStore<SignatureBehaviorDefinition>("signature_behaviors"),
            SignatureThemes = TestPaths.LoadStore<SignatureThemeDefinition>("signature_themes"),
            SignaturePayloads = TestPaths.LoadStore<SignaturePayloadDefinition>("signature_payloads"),
            Statuses = TestPaths.LoadStore<StatusDefinition>("statuses"),
            ModifierKeys = TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
            Moves = TestPaths.LoadStore<MoveDefinition>("moves"),
            Equipment = TestPaths.LoadStore<EquipmentDefinition>("equipment"),
            Professions = TestPaths.LoadStore<Dungeons.Professions.ProfessionDefinition>("professions"),
        };
        var inventory = new Dungeons.Items.Inventory();
        var engine = new IdentityFabricationEngine(
            content, () => inventory, new InstanceIdSource(), new SeededRandom(7));
        return new ForgeHarness(content, inventory, engine);
    }

    private static IdentityFabricationInvocation PlainLongsword() => new(
        "form.longsword",
        new Dictionary<string, string>
        {
            ["edge"] = "material.iron_ingot", ["core"] = "material.iron_ingot", ["binding"] = "material.leather",
        });

    /// <summary>Registers a Dense+Vital iron archetype the way the verb bench would, so the
    /// forge can pull it from the bag by id.</summary>
    private static string ForgeableDenseVitalIron(ContentBundle content)
    {
        var iron = content.Materials.GetById("material.iron_ingot");
        var state = IdentityStateResolver.StateOf(iron)! with
        {
            Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 1) },
        };
        var definition = new MaterialDefinition
        {
            Id = "material.test_dense_vital_iron",
            Name = "Dense Vital Iron Ingot",
            Tags = iron.Tags,
            Capacity = state.Capacity,
            IdentityState = state,
        };
        content.Materials.Add(definition);
        return definition.Id;
    }
}
