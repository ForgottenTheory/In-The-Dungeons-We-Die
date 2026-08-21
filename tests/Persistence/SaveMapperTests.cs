using Dungeons.Characters.Composition;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
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
        discoveries.Record("discovery.healing_salve");

        var knowledge = new Dictionary<string, int> { ["realm.dark_forest"] = 15 };

        // A minted identity item in the stash, and an equipped weapon.
        var instanceIds = new InstanceIdSource(5);
        stash.AddInstance(new ItemInstance
        {
            InstanceId = instanceIds.Next(),
            BaseDefinitionId = "equip.emergent.iabc123",
            ItemType = ItemType.Weapon,
            DisplayName = "Vital Iron Longsword",
            Provenance = new[] { "material.iron_ingot", "material.leather" },
            BaseDelivery = new ItemBaseDelivery(2.5, 4, 0),
            IdentitySentences = new[]
            {
                new ItemEffectSentence(ItemEffectCategory.Floor, "while_worn", "sustain", "vitality", 9, 1.0),
            },
            ExpressedIdentities = new[] { new IdentityStake("identity.vital", 2) },
            DormantIdentities = new[] { new IdentityStake("identity.storm", 1) },
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

        Assert.True(newDiscoveries.IsDiscovered("discovery.healing_salve"));
        Assert.Equal(15, newKnowledge["realm.dark_forest"]);
        Assert.Equal("class.wizard", loaded.Build!.BaseClassId.Value);

        // The minted item restored whole: delivery, sentences, the identity split.
        var restoredSword = Assert.Single(newStash.Instances);
        Assert.Equal("Vital Iron Longsword", restoredSword.DisplayName);
        Assert.Equal(2.5, restoredSword.BaseDelivery!.DamageBonus);
        var sentence = Assert.Single(restoredSword.IdentitySentences);
        Assert.Equal(ItemEffectCategory.Floor, sentence.Category);
        Assert.Equal("vitality", sentence.PayloadId);
        Assert.Equal("identity.vital", Assert.Single(restoredSword.ExpressedIdentities).Id);
        Assert.Equal("identity.storm", Assert.Single(restoredSword.DormantIdentities).Id);
        Assert.Contains("material.leather", restoredSword.Provenance);

        // Equipment restored.
        Assert.Equal("Iron Sword", newEquipment.InSlot(EquipmentSlot.Weapon)!.DisplayName);

        // Id counter advanced past the loaded ids (no future collisions).
        Assert.True(newInstanceIds.Next() >= instanceIds.Peek());
    }

    /// <summary>
    /// D49/D54, executed at v14: a pre-v14 save keeps every progression section and loses
    /// every item section — progression survives, items reset. There is no faithful mapping
    /// from property-shaped items to the identity model, so nothing is invented.
    /// </summary>
    [Fact]
    public void APreV14Save_KeepsProgressionAndLosesItems()
    {
        var oldSave = new SaveData
        {
            SchemaVersion = 13,
            Stash = new List<ItemStack> { new("material.oak_log", 4) },
            StashInstances = new List<ItemInstanceSave>
            {
                new() { InstanceId = 6, BaseDefinitionId = "equip.emergent.old", ItemType = ItemType.Weapon },
            },
            Gold = 120,
            Equipment = new Dictionary<string, ItemInstanceSave>
            {
                ["Weapon"] = new() { InstanceId = 7, BaseDefinitionId = "equip.iron_sword", ItemType = ItemType.Weapon },
            },
            Professions = new List<ProfessionSave>
            {
                new() { ProfessionId = "profession.forestry", Xp = 450, Mastery = new() { ["action.chop_oak"] = 8 } },
            },
            RealmKnowledge = new Dictionary<string, int> { ["realm.dark_forest"] = 15 },
            Discoveries = new List<string> { "discovery.healing_salve" },
            IdentityArchetypes = new List<IdentityArchetypeSave> { new() { Id = "emergent.i123", Name = "Old Mint" } },
            EmergentEquipment = new List<EquipmentArchetypeSave> { new() { Id = "equip.emergent.old" } },
            LearnedMoves = new List<string> { "move.cleave" },
            CharacterXp = 900,
        };

        var stash = new Inventory();
        var professions = MakeProfessions(stash);
        var discoveries = new DiscoverySystem();
        var knowledge = new Dictionary<string, int>();
        var equipment = new Equipment();
        var registry = new EmergentRegistry(new DataStore<MaterialDefinition>());
        var equipmentStore = new DataStore<Dungeons.Items.EquipmentDefinition>();

        SaveMapper.Apply(oldSave, stash, professions, discoveries, knowledge,
            equipment, emergentRegistry: registry, equipmentStore: equipmentStore);

        // Progression survives…
        Assert.Equal(450, professions.GetProgress("profession.forestry").Xp);
        Assert.Equal(15, knowledge["realm.dark_forest"]);
        Assert.True(discoveries.IsDiscovered("discovery.healing_salve"));
        Assert.Equal(120, stash.Gold);

        // …items reset.
        Assert.Empty(stash.Snapshot());
        Assert.Empty(stash.Instances);
        Assert.Null(equipment.InSlot(EquipmentSlot.Weapon));
        Assert.Equal(0, registry.Count);
        Assert.False(equipmentStore.Contains("equip.emergent.old"));
    }

    /// <summary>Pre-v4 saves predate emergent archetypes and must still load — the new fields
    /// simply arrive empty.</summary>
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
