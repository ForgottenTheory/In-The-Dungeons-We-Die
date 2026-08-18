using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Professions;
using Dungeons.Randomness;
using Dungeons.Realms;
using Xunit;

namespace Dungeons.Tests.Loot;

/// <summary>
/// Gold, and the one thing that actually matters about it right now: it obeys the extraction
/// risk model. Nothing spends coin yet by design (there is no economy), so the interesting
/// assertions are all about <em>where it lives</em> — coin picked up inside a Realm is
/// unsecured and lost on death, coin in the Stash is safe, and it survives a save.
///
/// <para>That comes free from putting gold on <see cref="Inventory"/> rather than in a separate
/// purse: it rides the same bag the rest of the loop already routes through. These tests are
/// what stop a future "just track gold separately" refactor from quietly making coin safe.</para>
/// </summary>
public class GoldTests
{
    private static RealmDefinition OneRoomRealm() => new()
    {
        Id = "realm.test",
        Locations = new[]
        {
            new RealmLocationDefinition { Id = "entrance", Type = RealmLocationType.Entrance, Depth = 1 },
        },
    };

    [Fact]
    public void ANewBagHasNoCoin() => Assert.Equal(0, new Inventory().Gold);

    [Fact]
    public void SpendingRequiresEnough()
    {
        var bag = new Inventory();
        bag.AddGold(10);

        Assert.False(bag.TrySpendGold(11));
        Assert.Equal(10, bag.Gold); // a failed spend changes nothing

        Assert.True(bag.TrySpendGold(10));
        Assert.Equal(0, bag.Gold);
    }

    [Fact]
    public void AddingOrSpendingNothingIsAProgrammingError()
    {
        var bag = new Inventory();
        Assert.Throws<ArgumentOutOfRangeException>(() => bag.AddGold(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => bag.TrySpendGold(-5));
    }

    [Fact]
    public void CoinRaisesTheSameChangedEventEverythingElseDoes()
    {
        var bag = new Inventory();
        var changes = 0;
        bag.Changed += () => changes++;

        bag.AddGold(5);
        bag.TrySpendGold(2);

        Assert.Equal(2, changes);
    }

    // --- The risk model -----------------------------------------------------

    [Fact]
    public void CoinCarriedIntoARealmIsSecuredByExtraction()
    {
        var run = new RealmRun(OneRoomRealm(), tier: 1);
        var stash = new Inventory();
        stash.AddGold(100);
        run.RunInventory.AddGold(40);

        var summary = RealmExtraction.Secure(run, stash);

        Assert.Equal(40, summary.Gold);
        Assert.Equal(140, stash.Gold);
        Assert.Equal(0, run.RunInventory.Gold);
    }

    [Fact]
    public void CoinCarriedIntoARealmIsLostOnDeath()
    {
        var run = new RealmRun(OneRoomRealm(), tier: 1);
        var stash = new Inventory();
        stash.AddGold(100);
        run.RunInventory.AddGold(40);

        var summary = RealmExtraction.Forfeit(run);

        Assert.Equal(40, summary.Gold);
        Assert.False(summary.Secured);
        Assert.Equal(100, stash.Gold); // the Stash never notices
        Assert.Equal(0, run.RunInventory.Gold);
    }

    // --- Persistence --------------------------------------------------------

    [Fact]
    public void CoinSurvivesACaptureAndApplyRoundTrip()
    {
        var stash = new Inventory();
        stash.AddGold(1234);
        stash.Add("material.iron_ore", 3);

        var saved = SaveMapper.Capture(
            build: null, stash, NewProfessions(), new DiscoverySystem(),
            new Dictionary<string, int>(), savedAtTick: 0);

        Assert.Equal(1234, saved.Gold);

        var restored = new Inventory();
        SaveMapper.Apply(saved, restored, NewProfessions(), new DiscoverySystem(), new Dictionary<string, int>());

        Assert.Equal(1234, restored.Gold);
        Assert.Equal(3, restored.GetQuantity("material.iron_ore"));
    }

    /// <summary>A save written before gold existed loads with none — the state a character who
    /// has never been paid is already in, which is why v8 needed no migration step.</summary>
    [Fact]
    public void AProfessionEraSaveLoadsWithNoCoinAndNoMigration()
    {
        var beforeGold = new SaveData { SchemaVersion = 7 };

        var stash = new Inventory();
        stash.AddGold(50); // whatever was in memory must not survive the load
        SaveMapper.Apply(beforeGold, stash, NewProfessions(), new DiscoverySystem(), new Dictionary<string, int>());

        Assert.Equal(0, stash.Gold);
    }

    private static ProfessionSystem NewProfessions() =>
        new(new DataStore<ProfessionActionDefinition>(), new Inventory(), new SeededRandom(1));
}
