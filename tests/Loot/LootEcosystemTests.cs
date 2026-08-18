using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Loot;
using Dungeons.Professions;
using Dungeons.Randomness;
using Dungeons.Realms;
using Xunit;

namespace Dungeons.Tests.Loot;

/// <summary>
/// The shipped loot content, checked as a set of <em>design rules</em> rather than as a list of
/// tables. Every rule here is one somebody could break with a plausible-looking JSON edit and
/// never notice in play, because bad loot looks exactly like bad luck:
///
/// <list type="bullet">
///   <item><b>D28</b> — realms drop inputs. An enemy that drops a sword breaks the bench's
///   primacy, and this file is what stops it.</item>
///   <item><b>D29.3</b> — essence and coin are the Realm's export. A profession drop table that
///   reaches either has quietly made extraction optional.</item>
///   <item><b>Active beats passive</b> — structurally, not by rate. Every gathering table must
///   have entries passive play cannot reach at any odds.</item>
///   <item><b>Hunting brings back the creature; Beast Lore opens it</b> — the fence the
///   profession pass drew, held through the loot system too.</item>
///   <item><b>Nothing drops that nothing wants</b> — the loot half of "no profession is a dead
///   end", and the reason the extraction loop closes.</item>
/// </list>
/// </summary>
public class LootEcosystemTests
{
    private static DataStore<LootTableDefinition> Tables => TestPaths.LoadStore<LootTableDefinition>("loot_tables");
    private static DataStore<MaterialDefinition> Materials => TestPaths.LoadStore<MaterialDefinition>("materials");
    private static DataStore<ProfessionActionDefinition> Actions => TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions");

    /// <summary>Every item a table can ever yield, following nested tables but ignoring
    /// conditions — the "could this ever drop here?" question.</summary>
    private static IReadOnlySet<string> Reachable(string tableId) =>
        LootReachability.ItemsReachableFrom(Tables, tableId);

    /// <summary>Every item a table <em>actually</em> yields under specific circumstances, found
    /// by rolling it. Conditions only exist at roll time, so any rule about depth gates or the
    /// active/passive split has to be asked this way rather than by walking the graph.</summary>
    private static IReadOnlySet<string> RolledOver(string tableId, LootContext circumstances, int rolls = 4000)
    {
        var content = new ContentBundle { LootTables = Tables, Materials = Materials };
        var resolver = new LootResolver(content, new SeededRandom(20260817));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var roll = 0; roll < rolls; roll++)
        {
            foreach (var drop in resolver.Roll(tableId, circumstances).Drops)
                seen.Add(drop.ItemId);
        }

        return seen;
    }

    private static IEnumerable<string> GatheringTableIds() =>
        Actions.GetAll()
            .Where(action => !string.IsNullOrEmpty(action.LootTableId))
            .Select(action => action.LootTableId!)
            .Distinct(StringComparer.Ordinal);

    // --- Scale ---------------------------------------------------------------

    /// <summary>The documented shape of the library, pinned so a doc that says "34 tables"
    /// cannot quietly become a doc that is wrong.</summary>
    [Fact]
    public void TheLibraryMeetsItsStatedScale()
    {
        Assert.True(Tables.Count >= 30, $"only {Tables.Count} loot tables shipped.");
        Assert.Equal(6, GatheringTableIds().Count());

        // Shared tables exist to be nested. One that nothing reaches is a table nothing tests.
        var nested = Tables.GetAll()
            .SelectMany(LootReachability.AllEntries)
            .Select(entry => entry.TableId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.Ordinal)!;

        foreach (var shared in Tables.GetAll().Where(t => t.Id.StartsWith("loot.shared.", StringComparison.Ordinal)))
            Assert.True(nested.Contains(shared.Id), $"{shared.Id} is a shared table nothing nests.");
    }

    // --- D28: realms drop inputs ---------------------------------------------

    /// <summary>
    /// The rule the whole loot design hangs from. Fabrication is the primary source of
    /// equipment; if a table could hand out a finished weapon, every visit to the bench becomes
    /// optional. Validation already restricts entries to materials/consumables/techniques —
    /// this asserts the consequence directly, so widening that set later fails here first.
    /// </summary>
    [Fact]
    public void NoLootTableYieldsFinishedEquipment()
    {
        var equipment = TestPaths.LoadStore<Dungeons.Items.EquipmentDefinition>("equipment");
        var materials = Materials;
        var consumables = TestPaths.LoadStore<ConsumableDefinition>("consumables");
        var techniques = TestPaths.LoadStore<TechniqueDefinition>("techniques");

        foreach (var table in Tables.GetAll())
        {
            foreach (var itemId in Reachable(table.Id))
            {
                Assert.False(equipment.Contains(itemId), $"{table.Id} drops finished equipment '{itemId}' (D28).");
                Assert.True(
                    materials.Contains(itemId) || consumables.Contains(itemId) || techniques.Contains(itemId),
                    $"{table.Id} drops '{itemId}', which is not an input of any kind.");
            }
        }
    }

    /// <summary>Salvage, not gear: whatever the Brute was swinging comes back as scrap. Pinned
    /// on the role table, because that is where a future armoured enemy of any species inherits
    /// it from.</summary>
    [Fact]
    public void AnArmouredEnemyPaysInSalvage()
    {
        var reachable = Reachable("loot.role.brute");
        Assert.Contains("material.scrap_iron", reachable);
        Assert.Contains("material.salvage_pile", reachable);
    }

    // --- D29.3: essence and coin are the Realm's export -----------------------

    [Fact]
    public void NoProfessionDropTableReachesEssence()
    {
        var materials = Materials;

        foreach (var tableId in GatheringTableIds())
        {
            foreach (var itemId in Reachable(tableId))
            {
                var isEssenceBearing = materials.TryGetById(itemId, out var material) && material.Essence.Count > 0;
                Assert.False(isEssenceBearing,
                    $"{tableId} can drop essence-bearing '{itemId}' — professions may not compete with extraction for the supernatural tier (D29.3).");
            }
        }
    }

    [Fact]
    public void NoProfessionDropTablePaysCoin()
    {
        foreach (var tableId in GatheringTableIds())
            Assert.False(LootReachability.YieldsGold(Tables, tableId), $"{tableId} pays gold; coin is a Realm export.");
    }

    /// <summary>The other half of the same rule, stated positively: Realm sources <em>do</em>
    /// reach essence, or extraction has no supernatural export to be the monopoly on.</summary>
    [Fact]
    public void RealmSourcesReachEssence()
    {
        var realms = TestPaths.LoadStore<RealmDefinition>("realms");
        var materials = Materials;

        var realmTableIds = realms.GetAll()
            .SelectMany(realm => realm.Locations)
            .Select(location => location.LootTableId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToList();

        var essenceReachable = realmTableIds
            .SelectMany(Reachable)
            .Any(id => materials.TryGetById(id, out var material) && material.Essence.Count > 0);

        Assert.True(essenceReachable, "no Realm node can drop an essence-bearing material.");
    }

    // --- Active beats passive, structurally -----------------------------------

    /// <summary>
    /// The design claim this test makes true rather than merely intended: active gathering does
    /// not roll the same table at better odds, it reaches entries the passive path cannot reach
    /// at <em>any</em> odds. Rolled rather than walked, because the gate is a condition.
    /// </summary>
    [Fact]
    public void EveryGatheringTableRewardsActivePlayWithThingsPassiveCannotReach()
    {
        foreach (var tableId in GatheringTableIds())
        {
            var passive = RolledOver(tableId, new LootContext(tags: new[] { LootContextTags.Passive }));
            var active = RolledOver(tableId, new LootContext(tags: new[] { LootContextTags.Active }));

            Assert.True(passive.IsSubsetOf(active),
                $"{tableId} pays passive play something active play cannot get: {string.Join(", ", passive.Except(active))}");
            Assert.True(active.Count > passive.Count,
                $"{tableId} gives active play nothing passive play could not already reach.");
        }
    }

    // --- The Hunting / Beast Lore fence ---------------------------------------

    /// <summary>
    /// Hunting brings back the creature; Beast Lore is the only thing that opens it. A hunting
    /// drop table that yields a hide has quietly merged two professions — the same failure
    /// <c>ProfessionEcosystemTests.HuntingProducesCarcassesAndOnlyBeastLoreOpensThem</c> guards
    /// on the action's own outputs, now closed on the loot path too.
    /// </summary>
    [Fact]
    public void NoHuntingDropTableRecoversAnatomy()
    {
        var materials = Materials;
        var anatomyForms = new[] { "form:hide", "form:meat", "form:blood", "form:organ", "form:gland", "form:pelt" };

        var huntingTables = Actions.GetAll()
            .Where(action => action.ProfessionId == "profession.hunting" && !string.IsNullOrEmpty(action.LootTableId))
            .Select(action => action.LootTableId!)
            .Distinct(StringComparer.Ordinal);

        foreach (var tableId in huntingTables)
        {
            foreach (var itemId in Reachable(tableId))
            {
                if (!materials.TryGetById(itemId, out var material))
                    continue;
                var anatomy = material.Tags.FirstOrDefault(tag => anatomyForms.Contains(tag, StringComparer.OrdinalIgnoreCase));
                Assert.True(anatomy is null,
                    $"{tableId} recovers '{itemId}' ({anatomy}) — only Beast Lore may open a creature.");
            }
        }
    }

    // --- Depth has to pay -----------------------------------------------------

    /// <summary>Gating a common material behind depth 2 costs the player a trip and pays them
    /// nothing. Anything a depth gate protects has to be worth the descent.</summary>
    [Fact]
    public void EverythingHiddenBehindDepthIsWorthTheDescent()
    {
        var materials = Materials;

        foreach (var table in Tables.GetAll())
        {
            foreach (var entry in LootReachability.AllEntries(table))
            {
                if (entry.When is not { MinDepth: >= 2 } || entry.ItemId is not { Length: > 0 } itemId)
                    continue;
                if (!materials.TryGetById(itemId, out var material))
                    continue; // techniques declare rarity on the entry and are checked below

                var rarity = material.Tags
                    .Select(tag => TagFamilies.TryParse(tag, out var family, out var value) && family == "rarity" ? value : null)
                    .FirstOrDefault(value => value is not null);

                Assert.True(rarity is not "common",
                    $"{table.Id} hides common '{itemId}' behind depth {entry.When.MinDepth} — the descent has to pay.");
            }
        }
    }

    // --- Elite / boss support, before there is an elite -----------------------

    /// <summary>
    /// The seam the design brief asked for, proven working before any elite exists: giving an
    /// enemy the <c>elite</c> tag is the entire change. If this ever stops holding, the first
    /// elite ever authored ships with no spoils and nobody notices for a milestone.
    /// </summary>
    [Fact]
    public void AnEliteTagUnlocksSpoilsAnOrdinaryEnemyCannotReach()
    {
        var ordinary = RolledOver("loot.family.goblin", new LootContext(depth: 2));
        var elite = RolledOver("loot.family.goblin", new LootContext(depth: 2, tags: new[] { "elite" }));

        Assert.True(ordinary.IsSubsetOf(elite), "an elite reaches less than an ordinary enemy of the same family.");
        Assert.True(elite.Count > ordinary.Count, "the elite tag unlocks nothing.");
    }

    [Fact]
    public void ABossReachesTheChaseMaterial()
    {
        Assert.Contains("material.relic_shard", Reachable("loot.shared.boss_spoils"));
    }

    // --- Nothing drops that nothing wants -------------------------------------

    /// <summary>
    /// The loot half of "no profession is a dead end", and the reason the extraction loop
    /// closes: every source has to hand back something a <em>named</em> system wants — a
    /// profession input, a bench interaction, a fabrication slot, a crafting substrate.
    ///
    /// <para><b>Per source, not per item, and that is deliberate.</b> The transformation bench
    /// accepts any material as a reagent, so "is this item usable at all?" is true by
    /// construction and would be a test that can never fail. What can fail — and what would
    /// make extraction feel emptier the more of it you do — is a whole table that pays out
    /// nothing but generic bench fodder.</para>
    /// </summary>
    [Fact]
    public void EveryLootSourceHandsBackSomethingASystemNames()
    {
        var materials = Materials;
        var actions = Actions.GetAll();
        var interactions = TestPaths.LoadStore<CraftingInteractionDefinition>("crafting_interactions").GetAll();
        var forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms").GetAll();
        var processes = TestPaths.LoadStore<CraftingActionDefinition>("processes").GetAll();

        var consumedByProfessions = actions.SelectMany(a => a.Inputs.Select(i => i.ItemId)).ToHashSet(StringComparer.Ordinal);
        var consumedByBench = interactions.SelectMany(i => i.Inputs.Select(input => input.ItemId)).ToHashSet(StringComparer.Ordinal);
        var slotTags = forms
            .SelectMany(form => form.Slots.Values.SelectMany(slot => slot.RequiresTags))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A material some shipped crafting action will accept as a substrate is wanted too —
        // that is the whole point of an emergent bench. Only the tags a real action names count.
        var craftableTags = processes
            .SelectMany(process => process.Requires.SubstrateTags)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool SomethingNamedWants(string itemId)
        {
            if (!materials.TryGetById(itemId, out var material))
                return true; // techniques and consumables are wanted by the player directly

            return consumedByProfessions.Contains(itemId)
                || consumedByBench.Contains(itemId)
                || material.Tags.Any(slotTags.Contains)
                || material.Tags.Any(craftableTags.Contains);
        }

        foreach (var table in Tables.GetAll())
        {
            var reachable = Reachable(table.Id);
            if (reachable.Count == 0)
                continue; // a gold-only table is legitimate; EveryShippedSourcePaysOut covers it

            Assert.Contains(reachable, SomethingNamedWants);
        }
    }

    // --- Every source actually pays out ---------------------------------------

    /// <summary>An enemy or node whose tables resolve to nothing is the failure mode this whole
    /// file exists to catch: no error, no crash, just a source that has quietly gone dry.</summary>
    [Fact]
    public void EveryShippedSourcePaysOut()
    {
        var actors = TestPaths.LoadStore<ActorDefinition>("actors");
        var families = TestPaths.LoadStore<EnemyFamilyDefinition>("enemy_families");
        var roles = TestPaths.LoadStore<CombatRoleDefinition>("enemy_roles");
        var aiProfiles = TestPaths.LoadStore<AiProfileDefinition>("ai_profiles");
        var resolver = new LootResolver(
            new ContentBundle { LootTables = Tables, Materials = Materials }, new SeededRandom(99));

        foreach (var actor in actors.GetAll())
        {
            var resolved = ActorResolver.Resolve(actor, families, roles, aiProfiles);
            Assert.True(resolved.LootTableIds.Count > 0, $"{actor.Id} drops nothing at all.");

            var haul = resolver.Roll(resolved.LootTableIds, new LootContext(depth: 1, tags: resolved.Tags));
            Assert.False(haul.IsEmpty, $"{actor.Id} rolled an empty haul.");
        }

        foreach (var realm in TestPaths.LoadStore<RealmDefinition>("realms").GetAll())
        {
            foreach (var location in realm.Locations.Where(l => !string.IsNullOrEmpty(l.LootTableId)))
            {
                var reached = Reachable(location.LootTableId!);
                Assert.True(reached.Count > 0, $"{realm.Id}/{location.Id} points at a table that yields nothing.");
            }
        }
    }

    /// <summary>Loot composes the way the rest of the enemy framework does: a kill draws from
    /// its family, its role and itself. If this collapses to one table, the composition seam has
    /// been lost and every new enemy has to re-author its whole drop list.</summary>
    [Fact]
    public void AnEnemysLootComposesFromFamilyRoleAndActor()
    {
        var actors = TestPaths.LoadStore<ActorDefinition>("actors");
        var resolved = ActorResolver.Resolve(
            actors.GetById("actor.goblin_brute"),
            TestPaths.LoadStore<EnemyFamilyDefinition>("enemy_families"),
            TestPaths.LoadStore<CombatRoleDefinition>("enemy_roles"),
            TestPaths.LoadStore<AiProfileDefinition>("ai_profiles"));

        Assert.Equal(
            new[] { "loot.family.goblin", "loot.role.brute", "loot.actor.goblin_brute" },
            resolved.LootTableIds);
    }
}
