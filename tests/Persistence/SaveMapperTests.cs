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
        var build = new CharacterBuild("species.human", "class.hexslinger", "prefix.ironbound", "suffix.unreasonable_confidence");
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

        // --- Capture → serialize → deserialize → apply to a FRESH session ---
        var save = SaveMapper.Capture(build, stash, professions, discoveries, knowledge, savedAtTick: 999);
        var json = new SaveSerializer().Serialize(save);
        var loaded = new SaveSerializer().Deserialize(json);

        var newStash = new Inventory();
        var newProfessions = MakeProfessions(newStash);
        var newDiscoveries = new DiscoverySystem();
        var newKnowledge = new Dictionary<string, int>();

        SaveMapper.Apply(loaded, newStash, newProfessions, newDiscoveries, newKnowledge);

        // --- Assert the fresh session matches ---
        Assert.Equal(4, newStash.GetQuantity("material.oak_log"));
        Assert.Equal(10, newStash.GetQuantity("material.iron_ore"));

        var restoredForestry = newProfessions.GetProgress("profession.forestry");
        Assert.Equal(450, restoredForestry.Xp);
        Assert.Equal(8, restoredForestry.GetMastery("action.chop_oak"));

        Assert.True(newDiscoveries.IsDiscovered("discovery.barkbound_iron"));
        Assert.Equal(15, newKnowledge["realm.dark_forest"]);
        Assert.Equal("class.hexslinger", loaded.Build!.BaseClassId);
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
