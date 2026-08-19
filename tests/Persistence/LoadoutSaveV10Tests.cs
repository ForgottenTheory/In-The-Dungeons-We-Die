using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Professions;
using Dungeons.Randomness;
using Dungeons.Realms;
using Xunit;

namespace Dungeons.Tests.Persistence;

/// <summary>
/// Save schema v10 — the prepared run loadout.
///
/// <para>Same rule as v7 and v8: a v9 save loads with no loadout at all, which is the state of a
/// player who has never opened the preparation screen. No migration step, and
/// <see cref="AV9SaveLoadsWithNoLoadoutRatherThanFailing"/> is what holds that.</para>
/// </summary>
public class LoadoutSaveV10Tests
{
    private static ProfessionSystem MakeProfessions(Inventory bag) =>
        new(new DataStore<ProfessionActionDefinition>(), bag, new SeededRandom(1));

    private static SaveData RoundTrip(SaveData save)
    {
        var serializer = new SaveSerializer();
        return serializer.Deserialize(serializer.Serialize(save))!;
    }

    private static (Inventory Stash, ProfessionSystem Professions, DiscoverySystem Discoveries) FreshSession()
    {
        var stash = new Inventory();
        return (stash, MakeProfessions(stash), new DiscoverySystem());
    }

    [Fact]
    public void TheDestinationAndThePackSurviveASaveAndLoad()
    {
        var (stash, professions, discoveries) = FreshSession();
        var loadout = new RunLoadout();
        loadout.SelectRealm("realm.dark_forest");
        loadout.Pack("consumable.healing_salve", 3);
        loadout.Pack("consumable.antidote", 1);

        var save = RoundTrip(SaveMapper.Capture(
            null, stash, professions, discoveries, new Dictionary<string, int>(),
            savedAtTick: 42, loadout: loadout));

        var restored = new RunLoadout();
        var (freshStash, freshProfessions, freshDiscoveries) = FreshSession();
        SaveMapper.Apply(save, freshStash, freshProfessions, freshDiscoveries,
            new Dictionary<string, int>(), loadout: restored);

        Assert.Equal("realm.dark_forest", restored.RealmId);
        Assert.Equal(3, restored.PackedQuantity("consumable.healing_salve"));
        Assert.Equal(1, restored.PackedQuantity("consumable.antidote"));
    }

    /// <summary>
    /// The pack is a <b>declaration</b>, not a reservation: the supplies are still counted in the
    /// Stash. If the save ever moved them out, one save/load cycle would delete them.
    /// </summary>
    [Fact]
    public void PackingDoesNotRemoveTheSuppliesFromTheStash()
    {
        var (stash, professions, discoveries) = FreshSession();
        stash.Add("consumable.healing_salve", 5);
        var loadout = new RunLoadout();
        loadout.SelectRealm("realm.dark_forest");
        loadout.Pack("consumable.healing_salve", 3);

        var save = RoundTrip(SaveMapper.Capture(
            null, stash, professions, discoveries, new Dictionary<string, int>(),
            savedAtTick: 1, loadout: loadout));

        var (freshStash, freshProfessions, freshDiscoveries) = FreshSession();
        SaveMapper.Apply(save, freshStash, freshProfessions, freshDiscoveries,
            new Dictionary<string, int>(), loadout: new RunLoadout());

        Assert.Equal(5, freshStash.GetQuantity("consumable.healing_salve"));
    }

    /// <summary>A v9 save has no <c>loadout</c> key at all. It must load as "never prepared".</summary>
    [Fact]
    public void AV9SaveLoadsWithNoLoadoutRatherThanFailing()
    {
        const string legacyJson = """
        {
          "schemaVersion": 9,
          "savedAtTick": 1200,
          "stash": [ { "itemId": "material.oak_log", "quantity": 2 } ],
          "professions": [],
          "realmKnowledge": { "realm.dark_forest": 15 },
          "discoveries": []
        }
        """;

        var save = new SaveSerializer().Deserialize(legacyJson)!;
        Assert.Null(save.Loadout);

        var loadout = new RunLoadout();
        loadout.SelectRealm("realm.meadow");
        loadout.Pack("consumable.healing_salve", 2);

        var (stash, professions, discoveries) = FreshSession();
        SaveMapper.Apply(save, stash, professions, discoveries, new Dictionary<string, int>(), loadout: loadout);

        Assert.Null(loadout.RealmId);
        Assert.Empty(loadout.PackedConsumables);
        Assert.Equal(2, stash.GetQuantity("material.oak_log"));
    }

    /// <summary>Every other caller passes no loadout, and that must stay legal — the parameter is
    /// optional exactly like farming plots and the training course.</summary>
    [Fact]
    public void CapturingWithoutALoadoutWritesNone()
    {
        var (stash, professions, discoveries) = FreshSession();

        var save = SaveMapper.Capture(null, stash, professions, discoveries,
            new Dictionary<string, int>(), savedAtTick: 0);

        Assert.Null(save.Loadout);
    }
}
