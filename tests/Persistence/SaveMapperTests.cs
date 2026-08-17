using Dungeons.Characters.Composition;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Professions;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Persistence;

public class SaveMapperTests
{
    private static ProfessionSystem MakeProfessions(Inventory bag) =>
        new(new DataStore<ProfessionActionDefinition>(), bag, new SeededRandom(1));

    [Fact]
    public void CaptureThenApply_RestoresAllPersistentState()
    {
        // --- Build a populated "session" ---
        var build = new CharacterBuild(
            new SpeciesId("species.human"), new BaseClassId("class.wizard"),
            new PrefixId("prefix.crystalline"), new SuffixId("suffix.unreasonable_confidence"));
        var stash = new Inventory();
        stash.Add("material.oak_log", 4);
        stash.Add("material.iron_ore", 10);

        var professions = MakeProfessions(stash);
        var forestry = professions.GetProgress("profession.forestry");
        forestry.AddXp(450);
        forestry.AddMastery("action.chop_oak", 8);

        var discoveries = new DiscoverySystem();
        discoveries.Record("discovery.barkbound_iron");

        var knowledge = new Dictionary<string, int> { ["realm.dark_forest"] = 15 };

        // A crafted material instance in the stash, and an equipped weapon.
        var instanceIds = new InstanceIdSource(5);
        stash.AddInstance(new ItemInstance
        {
            InstanceId = instanceIds.Next(),
            BaseDefinitionId = "material.barkbound_iron",
            ItemType = ItemType.Material,
            DisplayName = "Barkbound Iron",
            Properties = new PropertySet(new Dictionary<string, double> { ["toxin_resistance"] = 0.05 }),
            Provenance = new[] { "material.iron_ingot", "material.oak_bark" },
        });
        var equipment = new Equipment();
        equipment.Equip(EquipmentSlot.Weapon, new ItemInstance
        {
            InstanceId = instanceIds.Next(),
            BaseDefinitionId = "equip.iron_sword",
            ItemType = ItemType.Weapon,
            DisplayName = "Iron Sword",
        });

        // --- Capture → serialize → deserialize → apply to a FRESH session ---
        var save = SaveMapper.Capture(build, stash, professions, discoveries, knowledge, savedAtTick: 999, equipment, instanceIds);
        var json = new SaveSerializer().Serialize(save);
        var loaded = new SaveSerializer().Deserialize(json);

        var newStash = new Inventory();
        var newProfessions = MakeProfessions(newStash);
        var newDiscoveries = new DiscoverySystem();
        var newKnowledge = new Dictionary<string, int>();
        var newEquipment = new Equipment();
        var newInstanceIds = new InstanceIdSource();

        SaveMapper.Apply(loaded, newStash, newProfessions, newDiscoveries, newKnowledge, newEquipment, newInstanceIds);

        // --- Assert the fresh session matches ---
        Assert.Equal(4, newStash.GetQuantity("material.oak_log"));
        Assert.Equal(10, newStash.GetQuantity("material.iron_ore"));

        var restoredForestry = newProfessions.GetProgress("profession.forestry");
        Assert.Equal(450, restoredForestry.Xp);
        Assert.Equal(8, restoredForestry.GetMastery("action.chop_oak"));

        Assert.True(newDiscoveries.IsDiscovered("discovery.barkbound_iron"));
        Assert.Equal(15, newKnowledge["realm.dark_forest"]);
        Assert.Equal("class.wizard", loaded.Build!.BaseClassId.Value);

        // Crafted instance restored with its derived properties + provenance.
        var restoredBark = Assert.Single(newStash.Instances);
        Assert.Equal("Barkbound Iron", restoredBark.DisplayName);
        Assert.Equal(0.05, restoredBark.Properties.Get("toxin_resistance"));
        Assert.Contains("material.oak_bark", restoredBark.Provenance);

        // Equipment restored.
        Assert.Equal("Iron Sword", newEquipment.InSlot(EquipmentSlot.Weapon)!.DisplayName);

        // Id counter advanced past the loaded ids (no future collisions).
        Assert.True(newInstanceIds.Next() >= instanceIds.Peek());
    }

    /// <summary>
    /// An emergent archetype is the one definition-shaped thing a save holds, because there is
    /// no authored definition to point back at (docs/emergent-item-system.md §12.4). Round-trip
    /// it, then check the thing that actually matters: a stash stack referring to it resolves
    /// again in a fresh session.
    /// </summary>
    [Fact]
    public void CaptureThenApply_RestoresEmergentArchetypesAndTheStacksThatUseThem()
    {
        const string signature = "emergent.7f3a91c4";

        var materials = new DataStore<MaterialDefinition>();
        var registry = new EmergentRegistry(materials);
        registry.GetOrRegister(signature, () => new MaterialDefinition
        {
            Id = signature,
            Name = "Emberveined Iron",
            Tags = new[] { "form:metal", "state:alloy" },
            Properties = new Dictionary<string, double> { ["heat"] = 35, ["hardness"] = 62 },
            State = new MaterialState(
                new PropertySet(new Dictionary<string, double> { ["heat"] = 35, ["hardness"] = 62 }),
                MaterialStrength: 49,
                Workability: 72,
                Lineage: new Lineage(
                    new[] { new RootShare("material.iron_ingot", 1.0) },
                    Generation: 2,
                    CraftingActionId: "process.forge_infusion",
                    ParentSignatures: new[] { "material.iron_ingot" }),
                Signature: signature),
        });

        var stash = new Inventory();
        stash.Add(signature, 40); // emergent materials stack like any other (§0 Decision 3)
        var professions = MakeProfessions(stash);

        var save = SaveMapper.Capture(
            null, stash, professions, new DiscoverySystem(), new Dictionary<string, int>(),
            savedAtTick: 1, equipment: null, instanceIds: null, emergentRegistry: registry);

        var loaded = new SaveSerializer().Deserialize(new SaveSerializer().Serialize(save));

        // --- A fresh session that has never seen this material ---
        var freshMaterials = new DataStore<MaterialDefinition>();
        var freshRegistry = new EmergentRegistry(freshMaterials);
        var freshStash = new Inventory();

        SaveMapper.Apply(
            loaded, freshStash, MakeProfessions(freshStash), new DiscoverySystem(),
            new Dictionary<string, int>(), equipment: null, instanceIds: null,
            emergentRegistry: freshRegistry);

        Assert.Equal(40, freshStash.GetQuantity(signature));
        Assert.True(freshMaterials.Contains(signature), "the stack must resolve to a material again.");

        var restored = freshMaterials.GetById(signature);
        Assert.Equal("Emberveined Iron", restored.Name);
        Assert.Equal(49, restored.State!.MaterialStrength);
        Assert.Equal(72, restored.State.Workability);
        Assert.Equal(2, restored.State.Generation);
        Assert.Equal(35, restored.State.Properties.Get("heat"));
        Assert.Equal("process.forge_infusion", restored.State.Lineage.CraftingActionId);
        Assert.Equal("material.iron_ingot", restored.State.Lineage.DominantRoot?.RootId);
        Assert.Contains("material.iron_ingot", restored.State.Lineage.ParentSignatures);
    }

    /// <summary>v3 saves predate emergent archetypes and must still load — the new field simply
    /// arrives empty, so no migration step is needed (SaveData v4).</summary>
    [Fact]
    public void ASaveWithoutEmergentArchetypes_StillLoads()
    {
        var stash = new Inventory();
        var registry = new EmergentRegistry(new DataStore<MaterialDefinition>());

        SaveMapper.Apply(
            new SaveData(), stash, MakeProfessions(stash), new DiscoverySystem(),
            new Dictionary<string, int>(), equipment: null, instanceIds: null, emergentRegistry: registry);

        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void Apply_ReplacesExistingState()
    {
        var stash = new Inventory();
        stash.Add("material.stale", 99);
        var professions = MakeProfessions(stash);
        professions.GetProgress("profession.forestry").AddXp(1000);
        var discoveries = new DiscoverySystem();
        var knowledge = new Dictionary<string, int> { ["realm.old"] = 5 };

        var empty = new SaveData(); // fresh save with nothing
        SaveMapper.Apply(empty, stash, professions, discoveries, knowledge);

        Assert.Empty(stash.Snapshot());
        Assert.Empty(professions.AllProgress);
        Assert.Empty(knowledge);
    }
}
