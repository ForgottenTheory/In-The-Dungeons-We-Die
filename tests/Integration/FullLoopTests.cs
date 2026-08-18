using Dungeons.Events;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Loot;
using Dungeons.Professions;
using Dungeons.Randomness;
using Dungeons.Realms;
using Dungeons.Simulation;
using Dungeons.Tests.Professions; // FakeRandom
using Xunit;
using static Dungeons.Tests.Combat.CombatTestData;

namespace Dungeons.Tests.Integration;

/// <summary>
/// End-to-end domain test of the vertical-slice loop: enter a Realm → gather (into
/// the unsecured run inventory) → fight (heal mid-combat, win, loot) → travel →
/// extract (secure to the Stash). Proves the systems compose the way GameRoot wires
/// them, without Godot.
/// </summary>
public class FullLoopTests
{
    private static RealmDefinition ForestMap() => new()
    {
        Id = "realm.dark_forest",
        Name = "The Dark Forest",
        Locations = new[]
        {
            new RealmLocationDefinition { Id = "entrance", Type = RealmLocationType.Entrance, Depth = 1, Connections = new[] { "grove" } },
            new RealmLocationDefinition { Id = "grove", Type = RealmLocationType.Gather, Depth = 1, Connections = new[] { "entrance", "camp" }, ProfessionActionId = "action.chop_oak" },
            new RealmLocationDefinition { Id = "camp", Type = RealmLocationType.Combat, Depth = 1, Connections = new[] { "grove", "extract" }, ActorId = "actor.goblin" },
            new RealmLocationDefinition { Id = "extract", Type = RealmLocationType.Extraction, Depth = 1, Connections = new[] { "camp" } },
        },
    };

    private static DataStore<LootTableDefinition> GoblinLootTable()
    {
        var tables = new DataStore<LootTableDefinition>();
        tables.Add(new LootTableDefinition
        {
            Id = "loot.actor.goblin",
            Name = "Goblin",
            AlwaysDrops = new[] { new LootEntryDefinition { ItemId = "material.goblin_scrap" } },
            Gold = new GoldDropDefinition { MinAmount = 5, MaxAmount = 5 },
        });
        return tables;
    }

    private static ProfessionActionDefinition ChopOak() => new()
    {
        Id = "action.chop_oak",
        ProfessionId = "profession.forestry",
        BaseIntervalTicks = 100,
        Experience = 10,
        Outputs = new[] { new ItemStack("material.oak_log", 1) },
    };

    [Fact]
    public void Gather_Fight_Heal_Loot_Extract_SecuresToStash()
    {
        var tick = new TickEngine();
        var stash = new Inventory();
        var run = new RealmRun(ForestMap(), tier: 1);

        // Loot flows to the run inventory while the run is active, else the Stash.
        Inventory Bag() => run.Active ? run.RunInventory : stash;
        var actions = new DataStore<ProfessionActionDefinition>();
        actions.Add(ChopOak());
        var professions = new ProfessionSystem(actions, Bag, new SeededRandom(7));

        // 1) Travel to the grove and gather twice → unsecured run loot.
        Assert.True(run.TravelTo("grove"));
        professions.Execute("action.chop_oak");
        professions.Execute("action.chop_oak");
        Assert.Equal(2, run.RunInventory.GetQuantity("material.oak_log"));
        Assert.Equal(0, stash.GetQuantity("material.oak_log")); // not yet secured

        // 2) Travel to the camp and fight.
        Assert.True(run.TravelTo("camp"));
        var slash = Move("move.goblin_slash", DamageType.Slashing, 6, 8, 8, 20);
        var encounter = new CombatEncounter(
            tick, new HitPipeline(new FakeRandom(0.99)), Moves(slash), new FakeRandom(0.99), new GameEventBus());
        // The kill pays through the real loot system, exactly as GameRoot wires it: the
        // enemy's tables, the run's circumstances, and one deposit into whichever bag is live.
        var loot = new LootResolver(new ContentBundle { LootTables = GoblinLootTable() }, new SeededRandom(11));
        encounter.Ended += outcome =>
        {
            if (outcome.Result != CombatResult.Victory)
                return;
            foreach (var enemy in outcome.DefeatedEnemies)
                loot.Roll(enemy.LootTableIds, new LootContext(depth: run.CurrentDepth, tier: run.Tier)).DepositInto(Bag());
        };

        var player = Player(hp: 60, attrs: Attrs(str: 20));
        var goblin = Enemy("Goblin", 12, Attrs(con: 2), slash, lootTables: new[] { "loot.actor.goblin" });
        encounter.Start(player, new[] { goblin });

        // Take a hit's worth of damage, then heal with a crafted salve from the run bag.
        player.Health.Reduce(30);
        run.RunInventory.Add("consumable.healing_salve", 1);
        Assert.True(run.RunInventory.TryRemove("consumable.healing_salve", 1));
        encounter.UseHealingItem("Healing Salve", 25);
        Assert.Equal(55, player.Health.Current);

        // Healing spent attack tempo, so recover before striking the goblin down.
        tick.Advance(10);
        Assert.True(encounter.Attack());
        tick.Advance(20);
        Assert.False(encounter.IsActive);
        Assert.False(goblin.IsAlive);
        Assert.True(player.IsAlive);
        Assert.Equal(1, run.RunInventory.GetQuantity("material.goblin_scrap"));
        Assert.Equal(5, run.RunInventory.Gold); // unsecured, exactly like the stacks

        // 3) Travel to extraction and secure everything.
        run.MarkCleared("camp");
        Assert.True(run.TravelTo("extract"));
        var summary = RealmExtraction.Secure(run, stash);

        Assert.True(summary.Secured);
        Assert.Equal(2, stash.GetQuantity("material.oak_log"));
        Assert.Equal(1, stash.GetQuantity("material.goblin_scrap"));
        Assert.Equal(5, summary.Gold);
        Assert.Equal(5, stash.Gold);
        Assert.Empty(run.RunInventory.Snapshot());
        Assert.Equal(0, run.RunInventory.Gold);
        Assert.False(run.Active);

        // 4) Progression persisted: gathering earned Forestry XP.
        Assert.Equal(20, professions.GetProgress("profession.forestry").Xp);
    }
}
