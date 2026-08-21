using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Professions;
using Xunit;

namespace Dungeons.Tests.Professions;

/// <summary>
/// The 20-profession pass, checked as an <em>ecosystem</em> rather than a roster. The stated
/// goal was "one ecosystem rather than 20 isolated XP bars", so the load-bearing test here is
/// <see cref="EveryProcessingProfessionEatsSomebodyElsesOutput"/>: a profession that consumes
/// nothing and feeds nothing is a bar with a number on it, and this file fails when one
/// appears (docs/professions.md §8).
/// </summary>
public class ProfessionEcosystemTests
{
    private static DataStore<ProfessionDefinition> Professions => TestPaths.LoadStore<ProfessionDefinition>("professions");
    private static DataStore<ProfessionActionDefinition> Actions => TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions");

    [Fact]
    public void TheRosterIsTwentyProfessions()
    {
        Assert.Equal(20, Professions.Count);
    }

    /// <summary>
    /// The counts quoted in <c>docs/professions.md §6</c>. Pinned so the documented scale cannot
    /// quietly drift from the shipped content — a doc that is wrong about how much exists is
    /// worse than no doc.
    /// </summary>
    [Fact]
    public void TheRosterMeetsItsStatedScale()
    {
        Assert.Equal(348, Actions.Count);
        // 36 → 39 with D52: three new opportunities took over the active-identity payouts
        // the Phase 4 acquisition fence evicted from passive rolls (ley confluence,
        // earthheart seam, breathing pocket).
        Assert.Equal(39, Actions.GetAll().Sum(a => a.Opportunities.Count));
        Assert.Equal(12, TestPaths.LoadStore<TrainingObstacleDefinition>("training_obstacles").Count);
        Assert.Equal(15, TestPaths.LoadStore<ProfessionSynergyDefinition>("synergies").Count);
        Assert.Equal(1448, TestPaths.LoadStore<MaterialDefinition>("materials").Count);
    }

    [Fact]
    public void EveryProfessionHasALadderToClimb()
    {
        var actions = Actions.GetAll();

        foreach (var profession in Professions.GetAll())
        {
            var own = actions.Where(a => a.ProfessionId == profession.Id).ToList();
            Assert.True(own.Count >= 4, $"{profession.Id} has only {own.Count} action(s).");
            Assert.True(own.Select(a => a.RequiredLevel).Distinct().Count() >= 3,
                $"{profession.Id} gates everything at the same level — that is a list, not a ladder.");
        }
    }

    /// <summary>
    /// A fresh character must be able to start every profession. For nineteen of them that
    /// means a level-1 action; Agility is the exception by design — its level-1 entry point is
    /// the training course, and its actions are reach-gated on purpose (you should not be
    /// scaling cliffs on day one).
    /// </summary>
    [Fact]
    public void EveryProfessionCanBeStartedAtLevelOne()
    {
        var actions = Actions.GetAll();

        foreach (var profession in Professions.GetAll().Where(p => p.Id != TrainingCourse.AgilityProfessionId))
        {
            Assert.Contains(actions, a => a.ProfessionId == profession.Id && a.RequiredLevel == 1);
        }

        var obstacles = TestPaths.LoadStore<TrainingObstacleDefinition>("training_obstacles").GetAll();
        Assert.Contains(obstacles, o => o.RequiredLevel == 1);
    }

    /// <summary>The course has to be buildable from level 1 and stay worth revisiting: every
    /// slot needs something fittable, and the ladder must reach into the late game.</summary>
    [Fact]
    public void TheTrainingCourseCoversEverySlotAndKeepsGrowing()
    {
        var obstacles = TestPaths.LoadStore<TrainingObstacleDefinition>("training_obstacles").GetAll();

        foreach (var slot in Enum.GetValues<TrainingSlot>())
            Assert.Contains(obstacles, o => o.Slot == slot);

        Assert.Contains(obstacles, o => o.RequiredLevel >= 60);
        Assert.All(obstacles, o => Assert.NotEmpty(o.Bonuses));
    }

    [Fact]
    public void EveryProfessionHasAnIdentityLineAndACategory()
    {
        foreach (var profession in Professions.GetAll())
        {
            Assert.False(string.IsNullOrWhiteSpace(profession.Name), $"{profession.Id} has no name.");
            Assert.False(string.IsNullOrWhiteSpace(profession.Description), $"{profession.Id} has no description.");
            Assert.NotEmpty(profession.PrimaryAttributes);
        }

        // All three shelves are populated — the roster is not secretly one category.
        foreach (var category in Enum.GetValues<ProfessionCategory>())
            Assert.Contains(Professions.GetAll(), p => p.Category == category);
    }

    /// <summary>
    /// Gather → Process → Manufacture. Every Processing profession must consume something
    /// another profession produces; otherwise it is conjuring its own inputs.
    /// </summary>
    [Fact]
    public void EveryProcessingProfessionEatsSomebodyElsesOutput()
    {
        var actions = Actions.GetAll();
        var producers = ProducersByItem(actions);

        foreach (var profession in Professions.GetAll().Where(p => p.Category == ProfessionCategory.Processing))
        {
            var eatsForeign = actions
                .Where(a => a.ProfessionId == profession.Id)
                .SelectMany(a => a.Inputs)
                .Any(input => producers[input.ItemId].Any(producer => producer != profession.Id));

            Assert.True(eatsForeign, $"{profession.Id} consumes nothing another profession makes.");
        }
    }

    /// <summary>
    /// The other direction: nothing may be a dead end. Every profession's output has to matter
    /// to something — another profession's action, the crafting bench, or a fabrication slot.
    ///
    /// <para><b>Cooking is the one documented exception.</b> A meal's consumer is the player,
    /// through the consumable forms that have not shipped yet, so today its output genuinely
    /// lands nowhere. That is real debt rather than a design choice, and naming it here keeps
    /// it visible instead of quietly weakening the rule for the other nineteen. When
    /// consumables land, delete the exception and this test should still pass.</para>
    ///
    /// <para>This is a floor, not a ceiling: the material transformation bench accepts
    /// arbitrary reagents, so plenty of outputs are usable there without appearing in any
    /// authored input list. What the test catches is a profession whose output nothing
    /// <em>named</em> wants.</para>
    /// </summary>
    [Fact]
    public void NoProfessionIsADeadEnd()
    {
        var actions = Actions.GetAll();
        var interactions = TestPaths.LoadStore<CraftingInteractionDefinition>("crafting_interactions").GetAll();
        var forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms").GetAll();

        var consumedByOthers = actions
            .SelectMany(a => a.Inputs.Select(i => (a.ProfessionId, i.ItemId)))
            .ToLookup(pair => pair.ItemId, pair => pair.ProfessionId);
        var consumedByBench = interactions.SelectMany(i => i.Inputs.Select(input => input.ItemId)).ToHashSet(StringComparer.Ordinal);

        // Runecrafting and Fletching hand their output to fabrication rather than to another
        // action, so a slot-tag match counts as being consumed too.
        var slotTags = forms
            .SelectMany(f => f.Slots.Values.SelectMany(slot => slot.RequiresTags))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");

        bool FabricationAccepts(string itemId) =>
            materials.TryGetById(itemId, out var material) && material.Tags.Any(slotTags.Contains);

        const string awaitingConsumables = "profession.cooking";

        foreach (var profession in Professions.GetAll().Where(p => p.Id != awaitingConsumables))
        {
            var outputs = actions
                .Where(a => a.ProfessionId == profession.Id)
                .SelectMany(a => a.Outputs.Select(o => o.ItemId).Concat(a.BonusOutputs.Select(o => o.ItemId)))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var landsSomewhere = outputs.Any(id =>
                consumedByOthers[id].Any(consumer => consumer != profession.Id)
                || consumedByBench.Contains(id)
                || FabricationAccepts(id));

            Assert.True(landsSomewhere, $"nothing {profession.Id} produces is used anywhere else.");
        }
    }

    /// <summary>
    /// The Hunting/Beast Lore split, pinned. Hunting brings back the creature; Beast Lore is the
    /// only thing that turns it into parts. If a hunting action ever starts dropping hides
    /// directly, the two professions have quietly merged.
    /// </summary>
    [Fact]
    public void HuntingProducesCarcasses_AndOnlyBeastLoreOpensThem()
    {
        var actions = Actions.GetAll();
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");

        bool IsCarcass(string id) =>
            materials.TryGetById(id, out var m) && m.Tags.Contains("form:carcass", StringComparer.OrdinalIgnoreCase);

        var hunting = actions.Where(a => a.ProfessionId == "profession.hunting").ToList();
        Assert.NotEmpty(hunting);
        Assert.All(hunting, a => Assert.Contains(a.Outputs, o => IsCarcass(o.ItemId)));

        var carcassConsumers = actions
            .Where(a => a.Inputs.Any(i => IsCarcass(i.ItemId)))
            .Select(a => a.ProfessionId)
            .Distinct()
            .ToList();

        Assert.Equal(new[] { "profession.beast_lore" }, carcassConsumers);
    }

    /// <summary>
    /// Beast Lore's quick/full pairs are the Realm decision — a fast dressing or a long full
    /// harvest on the same carcass. The full version must actually cost more time and return
    /// more, or there is no decision.
    /// </summary>
    [Fact]
    public void BeastLoreOffersAFastAndAThoroughOptionOnTheSameCarcass()
    {
        var actions = Actions.GetAll().Where(a => a.ProfessionId == "profession.beast_lore").ToList();

        var pairs = actions
            .SelectMany(a => a.Inputs.Select(i => (Carcass: i.ItemId, Action: a)))
            .GroupBy(pair => pair.Carcass)
            .Where(group => group.Count() > 1)
            .ToList();

        Assert.NotEmpty(pairs);

        foreach (var group in pairs)
        {
            var ordered = group.Select(g => g.Action).OrderBy(a => a.BaseIntervalTicks).ToList();
            var quick = ordered.First();
            var thorough = ordered.Last();

            Assert.True(thorough.BaseIntervalTicks > quick.BaseIntervalTicks);
            Assert.True(
                thorough.Outputs.Count + thorough.BonusOutputs.Count > quick.Outputs.Count + quick.BonusOutputs.Count,
                $"{thorough.Id} takes longer than {quick.Id} but does not recover more.");
            Assert.True(thorough.RequiredLevel > quick.RequiredLevel);
        }
    }

    /// <summary>Assay must pay in comprehension, not power — so its dossiers have to be wanted
    /// by the deep crafts, or the profession is a dead end with extra steps.</summary>
    [Fact]
    public void AssayDossiersGateTheDeepestCraftingActions()
    {
        var consumers = Actions.GetAll()
            .Where(a => a.Inputs.Any(i => i.ItemId == "material.property_dossier"))
            .Select(a => a.ProfessionId)
            .Distinct()
            .ToList();

        Assert.True(consumers.Count >= 3, $"only {consumers.Count} profession(s) need an Assay dossier.");
        Assert.DoesNotContain("profession.assay", consumers); // never merely feeding itself
    }

    /// <summary>Cartography feeds Realm Knowledge rather than a competing progression.</summary>
    [Fact]
    public void OnlyCartographyTeachesRealmKnowledge()
    {
        var teachers = Actions.GetAll()
            .Where(a => a.RealmKnowledgeGain is not null)
            .ToList();

        Assert.NotEmpty(teachers);
        Assert.All(teachers, a => Assert.Equal("profession.cartography", a.ProfessionId));
        Assert.All(teachers, a => Assert.True(a.RealmKnowledgeGain!.Amount > 0));
    }

    /// <summary>A success roll is a deliberate identity choice for the two professions whose
    /// fiction is an attempt. Everywhere else, a swung pickaxe always produces ore.</summary>
    [Fact]
    public void OnlyHuntingAndThievingCanMiss()
    {
        var chancy = Actions.GetAll()
            .Where(a => a.SuccessChance < 1.0)
            .Select(a => a.ProfessionId)
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "profession.hunting", "profession.thieving" }, chancy);
    }

    /// <summary>Opportunities are the active layer, so they must be spread across the roster
    /// rather than bolted onto one flagship profession.</summary>
    [Fact]
    public void OpportunitiesReachAcrossTheRoster()
    {
        var withOpportunities = Actions.GetAll()
            .Where(a => a.Opportunities.Count > 0)
            .Select(a => a.ProfessionId)
            .Distinct()
            .ToList();

        Assert.True(withOpportunities.Count >= 12,
            $"only {withOpportunities.Count} profession(s) have an active opportunity.");
    }

    /// <summary>Every opportunity has to be worth more than the action that surfaced it —
    /// otherwise "pursue" is never the right answer and the decision is fake.</summary>
    [Fact]
    public void EveryOpportunityOutPaysItsOwnAction()
    {
        foreach (var action in Actions.GetAll())
        {
            foreach (var opportunity in action.Opportunities)
            {
                Assert.True(opportunity.Experience > action.Experience,
                    $"{opportunity.Id} pays {opportunity.Experience} XP against {action.Id}'s {action.Experience}.");
                Assert.False(string.IsNullOrWhiteSpace(opportunity.Prompt), $"{opportunity.Id} has no prompt.");
                Assert.True(opportunity.ExtraIntervalTicks > 0, $"{opportunity.Id} costs no time.");
            }
        }
    }

    /// <summary>Farming's beds must reseed themselves, or an established plot quietly starves.</summary>
    [Fact]
    public void EveryFarmingBedReturnsItsOwnSeed()
    {
        var beds = Actions.GetAll()
            .Where(a => a.ProfessionId == FarmingPlots.FarmingProfessionId && a.Inputs.Count > 0)
            .ToList();

        Assert.NotEmpty(beds);

        foreach (var bed in beds)
        {
            var seed = Assert.Single(bed.Inputs).ItemId;
            var returned = bed.Outputs.Concat(bed.BonusOutputs.Select(b => b.Stack))
                .Where(o => o.ItemId == seed)
                .Sum(o => o.Quantity);

            Assert.True(returned >= 1, $"{bed.Id} consumes {seed} and never returns one.");
        }
    }

    /// <summary>Every seed a bed needs must be obtainable in the wild first.</summary>
    [Fact]
    public void EveryPlantableSeedHasAWildSource()
    {
        var actions = Actions.GetAll();
        var beds = actions.Where(a => a.ProfessionId == FarmingPlots.FarmingProfessionId && a.Inputs.Count > 0);
        var wildSources = actions
            .Where(a => a.Inputs.Count == 0)
            .SelectMany(a => a.Outputs.Select(o => o.ItemId)
                .Concat(a.BonusOutputs.Select(o => o.ItemId))
                .Concat(a.Opportunities.SelectMany(op => op.BonusOutputs.Select(o => o.ItemId))))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var bed in beds)
        {
            var seed = Assert.Single(bed.Inputs).ItemId;
            Assert.True(wildSources.Contains(seed), $"{seed} is plantable but cannot be found in the wild.");
        }
    }

    private static ILookup<string, string> ProducersByItem(IEnumerable<ProfessionActionDefinition> actions) =>
        actions
            .SelectMany(a => a.Outputs.Select(o => o.ItemId)
                .Concat(a.BonusOutputs.Select(o => o.ItemId))
                .Select(itemId => (a.ProfessionId, ItemId: itemId)))
            .ToLookup(pair => pair.ItemId, pair => pair.ProfessionId);
}
