using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Loot;
using Dungeons.Professions;
using Dungeons.Randomness;
using Dungeons.Tests.Professions;
using Xunit;

namespace Dungeons.Tests.Loot;

/// <summary>
/// The seam between professions and loot: an action's <c>loot_table</c> is rolled by the same
/// <see cref="ProfessionSystem.Execute"/> path passive, active and offline play all share, and
/// the circumstances of the roll come from the host rather than from Core.
///
/// <para>The rule worth protecting here is the one about <b>a missed attempt</b>: Hunting and
/// Thieving can fail, and a failed attempt must turn up nothing. Otherwise "the prey bolted"
/// still pays, and the success chance stops meaning anything.</para>
/// </summary>
public class ProfessionDropTableTests
{
    private const string ChopOak = "action.chop_oak";

    private static ProfessionActionDefinition Action(double successChance = 1.0) => new()
    {
        Id = ChopOak,
        ProfessionId = "profession.forestry",
        Name = "Chop Oak",
        Experience = 10,
        SuccessChance = successChance,
        Outputs = new[] { new ItemStack("material.oak_log", 1) },
        LootTableId = "loot.gather.forestry",
    };

    private static LootResolver Resolver()
    {
        var tables = new DataStore<LootTableDefinition>();
        tables.Add(new LootTableDefinition
        {
            Id = "loot.gather.forestry",
            Name = "What the Tree Gives Up",
            AlwaysDrops = new[] { new LootEntryDefinition { ItemId = "material.pinecone" } },
            WeightedDraws = new[]
            {
                new LootDrawDefinition
                {
                    Picks = 1,
                    When = new LootCondition { RequiresTags = new[] { LootContextTags.Active } },
                    Entries = new[] { new LootEntryDefinition { ItemId = "material.ironbark_seed" } },
                },
            },
        });

        return new LootResolver(new ContentBundle { LootTables = tables }, new SeededRandom(3));
    }

    private static (ProfessionSystem Professions, Inventory Bag) Session(
        double successChance = 1.0, double rolls = 0.0)
    {
        var actions = new DataStore<ProfessionActionDefinition>();
        actions.Add(Action(successChance));
        var bag = new Inventory();
        var resolver = Resolver();

        var professions = new ProfessionSystem(
            actions, () => bag, new FakeRandom(rolls),
            rollDropTable: (tableId, wasActive) => resolver.Roll(
                tableId,
                new LootContext(tags: new[] { wasActive ? LootContextTags.Active : LootContextTags.Passive })));

        return (professions, bag);
    }

    [Fact]
    public void ALandedAttemptRollsItsDropTableOnTopOfItsOutputs()
    {
        var (professions, bag) = Session();

        var outcome = professions.Execute(ChopOak);

        Assert.Equal(1, bag.GetQuantity("material.oak_log"));   // the work's product
        Assert.Equal(1, bag.GetQuantity("material.pinecone"));  // what the work turned up
        Assert.Contains(outcome.Produced, stack => stack.ItemId == "material.pinecone");
    }

    /// <summary>The whole point of the active/passive tag: passive play cannot reach the entry
    /// at any rate, because the condition is not a probability.</summary>
    [Fact]
    public void OnlyActivePlayReachesTheActiveGatedEntries()
    {
        var (passiveSession, passiveBag) = Session();
        passiveSession.Execute(ChopOak);
        Assert.Equal(0, passiveBag.GetQuantity("material.ironbark_seed"));

        var (activeSession, activeBag) = Session();
        activeSession.Execute(ChopOak, performance: 1.0, isActive: true);
        Assert.Equal(1, activeBag.GetQuantity("material.ironbark_seed"));
    }

    [Fact]
    public void AMissedAttemptTurnsUpNothing()
    {
        // A roll of 0.99 fails a 0.5 success chance: the prey bolted.
        var (professions, bag) = Session(successChance: 0.5, rolls: 0.99);

        var outcome = professions.Execute(ChopOak);

        Assert.True(outcome.AttemptMissed);
        Assert.Equal(0, bag.GetQuantity("material.pinecone"));
        Assert.Empty(outcome.Produced);
    }

    /// <summary>Every existing profession suite constructs a system with no loot wired, and must
    /// keep working: an action simply yields what it authors.</summary>
    [Fact]
    public void AnActionWithNoLootSystemWiredStillYieldsItsOutputs()
    {
        var actions = new DataStore<ProfessionActionDefinition>();
        actions.Add(Action());
        var bag = new Inventory();
        var professions = new ProfessionSystem(actions, bag, new FakeRandom(0.99));

        professions.Execute(ChopOak);

        Assert.Equal(1, bag.GetQuantity("material.oak_log"));
        Assert.Equal(0, bag.GetQuantity("material.pinecone"));
    }
}
