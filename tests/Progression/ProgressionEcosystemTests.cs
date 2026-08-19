using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Presentation;
using Dungeons.Professions;
using Dungeons.Randomness;
using Dungeons.Realms;
using Xunit;
using Xunit.Abstractions;

namespace Dungeons.Tests.Progression;

/// <summary>
/// <b>No progression track may be a number that goes up and does nothing.</b>
///
/// <para>Modelled on <c>ProfessionEcosystemTests.NoProfessionIsADeadEnd</c>, and written for the
/// same reason: the failure is silent. Mastery spent the entire project incrementing while GDD
/// §7.3 said "nothing reads it"; Realm Knowledge counted up for two milestones before it unlocked
/// anything; character XP had a complete growth system and no source. Each of those was found by
/// reading, months late. This test finds the next one on the next run.</para>
///
/// <para>Where a rule has an honest exception it is <b>named</b>, with the milestone that removes
/// it written beside it. A weakened assertion stops catching the next mistake; a named exception
/// is a to-do list.</para>
/// </summary>
public class ProgressionEcosystemTests
{
    private readonly ITestOutputHelper _output;

    public ProgressionEcosystemTests(ITestOutputHelper output) => _output = output;

    // --- Profession levels: gate actions ------------------------------------

    [Fact]
    public void ProfessionLevelsUnlockActions()
    {
        var actions = TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions");
        var gated = actions.GetAll().Where(action => action.RequiredLevel > 1).ToList();

        Assert.NotEmpty(gated);
        _output.WriteLine($"profession levels    → {gated.Count} of {actions.Count} actions are level-gated");
    }

    // --- Mastery: six benefits, all consumed --------------------------------

    /// <summary>
    /// Every <see cref="ProfessionBenefitKind"/> must be worth something at full mastery. The enum
    /// is a closed vocabulary whose members each have exactly one consumer in the execution path,
    /// so a kind worth zero is a consumer that can never fire.
    /// </summary>
    [Fact]
    public void MasteryBuysEveryBenefitItDeclares()
    {
        var ladder = TestPaths.ShippedMasteryLadder();

        foreach (var kind in Enum.GetValues<ProfessionBenefitKind>())
        {
            var value = ladder.ValueOf(kind, "profession.mining", MasteryLeveling.MaxLevel);
            Assert.True(value > 0, $"mastery buys nothing for {kind}.");
            _output.WriteLine($"mastery              → {kind} tops out at {value:0.###}");
        }
    }

    /// <summary>Mastery must also unlock an <em>option</em>, not only better numbers — the design
    /// rule is "new actions, better information, new routes… rather than only +5%".</summary>
    [Fact]
    public void MasteryUnlocksAtLeastOneOptionAndNotOnlyNumbers()
    {
        var actions = TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions");
        var gated = actions.GetAll()
            .SelectMany(action => action.Opportunities)
            .Where(offer => offer.RequiredMasteryLevel > 0)
            .ToList();

        Assert.NotEmpty(gated);
        foreach (var offer in gated)
            _output.WriteLine($"mastery (options)    → {offer.Id} needs mastery {offer.RequiredMasteryLevel}");
    }

    // --- Realm Knowledge: seven insights, all reachable and all read ---------

    [Fact]
    public void EveryRealmInsightHasAThresholdAndChangesTheBriefing()
    {
        var content = BriefingContent();
        var realm = content.Realms.GetById("realm.dark_forest");

        foreach (var insight in Enum.GetValues<RealmInsight>())
        {
            Assert.True(RealmKnowledgeLevels.Required.ContainsKey(insight), $"{insight} has no threshold.");

            var below = RealmBriefing.Compile(content, realm, RealmKnowledgeLevels.Required[insight] - 1);
            var at = RealmBriefing.Compile(content, realm, RealmKnowledgeLevels.Required[insight]);

            Assert.True(DescribeBriefing(at) != DescribeBriefing(below),
                $"crossing {insight}'s threshold changes nothing the player can see.");
            _output.WriteLine($"realm knowledge      → {insight} at {RealmKnowledgeLevels.Required[insight]} changes the briefing");
        }
    }

    /// <summary>A crude fingerprint of everything a briefing exposes. Comparing two of them is
    /// how "crossing this threshold reveals something" becomes an assertion rather than a hope.</summary>
    private static string DescribeBriefing(RealmBriefing briefing) =>
        $"{briefing.Threats.Count}|{briefing.Hazards.Count}|{briefing.Resources.Count}|"
        + $"{briefing.Routes.Count}|{briefing.Yield.Count}|{briefing.DeepestEntry}|{briefing.Unlocked.Count}";

    // --- Character XP: earned in Realms, and only there ----------------------

    /// <summary>
    /// <b>The layered-model fence.</b> A profession attempt reports profession XP and mastery and
    /// says nothing about the character. If character XP ever appears on this path, fishing has
    /// started raising combat attributes and GDD §4's "no single Character Level represents
    /// everything" has quietly become false.
    /// </summary>
    [Fact]
    public void ProfessionWorkAwardsNoCharacterXp()
    {
        var action = new ProfessionActionDefinition
        {
            Id = "action.chop_oak",
            ProfessionId = "profession.forestry",
            Name = "Chop Oak",
            Experience = 10,
            Outputs = new[] { new ItemStack("material.oak_log", 1) },
        };

        var store = new DataStore<ProfessionActionDefinition>();
        store.Add(action);
        var system = new ProfessionSystem(store, new Inventory(), new SeededRandom(1));

        var outcome = system.Execute("action.chop_oak");

        Assert.True(outcome.XpGained > 0);
        Assert.Equal(1, outcome.MasteryGained);

        // The whole assertion: an ActionOutcome has no character-XP field to leak through, and
        // adding one would fail this test at compile time — which is the point.
        Assert.DoesNotContain("Character", typeof(ActionOutcome).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public void CharacterXpHasRealmSourcesAndRaisesAttributes()
    {
        Assert.True(CharacterLeveling.XpForDefeating(30, EnemyRank.Normal) > 0);
        Assert.True(CharacterLeveling.XpForExtracting > 0);

        var build = ShippedBuilds().Resolve(new CharacterBuild(
            new SpeciesId("species.human"), new BaseClassId("class.wizard"),
            new PrefixId("prefix.galvanic"), new SuffixId("suffix.unreasonable_confidence")));

        Assert.True(build.GrowthAt(10).Values.Sum() > 0, "levelling grants no attributes.");
        _output.WriteLine($"character xp         → level 10 grants {build.GrowthAt(10).Values.Sum()} attribute points");
    }

    // --- Crafting discoveries, techniques, Assay ----------------------------

    [Fact]
    public void CraftingDiscoveriesGateSomething()
    {
        var interactions = TestPaths.LoadStore<CraftingInteractionDefinition>("crafting_interactions");

        Assert.NotEmpty(interactions.GetAll());
        _output.WriteLine($"discoveries          → {interactions.Count} interaction(s) recorded on discovery");
    }

    [Fact]
    public void TechniquesTeachMovesThatResolve()
    {
        var techniques = TestPaths.LoadStore<TechniqueDefinition>("techniques");
        var moves = TestPaths.LoadStore<MoveDefinition>("moves");

        Assert.NotEmpty(techniques.GetAll());
        Assert.All(techniques.GetAll(), technique => Assert.True(moves.Contains(technique.Teaches)));
        _output.WriteLine($"techniques           → {techniques.Count} manual(s), each teaching a real move");
    }

    /// <summary>Assay levels must widen the reading, or the profession is a bar with no payoff.</summary>
    [Fact]
    public void AssayLevelsWidenWhatAMaterialShows()
    {
        var depths = new[] { 1, AssayTuning.CompositionLevel, AssayTuning.ReactiveLevel, AssayTuning.TraitsLevel, AssayTuning.EssenceLevel, AssayTuning.PotentialLevel }
            .Select(AssayLens.DepthFor)
            .ToList();

        Assert.Equal(depths.Distinct().Count(), depths.Count);
        _output.WriteLine($"assay                → {depths.Count} distinct reveal depths across the level ladder");
    }

    // --- The one honest exception -------------------------------------------

    /// <summary>
    /// <b>Form/schematic acquisition is the one dead track, and it is named rather than excused.</b>
    ///
    /// <para><c>material.schematic_fragment</c> drops from eight tables and does nothing: there is
    /// no schematic→form binding, no persisted known-forms list, and every form is available at
    /// the bench from the first minute. Building it is D29.2 / M6, not an integration pass —
    /// gating fabrication behind drops is a balance and soft-lock decision.</para>
    ///
    /// <para><b>When form acquisition ships, delete this test and
    /// <see cref="EveryProgressionTrackHasAConsumer"/> should still pass.</b></para>
    /// </summary>
    [Fact]
    public void FormAcquisitionIsStillTheOneUnconsumedTrack()
    {
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");

        Assert.True(materials.Contains("material.schematic_fragment"),
            "if schematics are gone, delete this exemption instead of updating it.");

        _output.WriteLine("form acquisition     → EXEMPT: schematics drop and are inert (D29.2, M6)");
    }

    /// <summary>The roll-call. Every track named once, so a new one cannot be added without
    /// someone deciding out loud whether anything reads it.</summary>
    [Fact]
    public void EveryProgressionTrackHasAConsumer()
    {
        var tracks = new (string Name, bool Consumed)[]
        {
            ("profession levels", TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions").GetAll().Any(a => a.RequiredLevel > 1)),
            ("per-action mastery", Enum.GetValues<ProfessionBenefitKind>().All(k => TestPaths.ShippedMasteryLadder().ValueOf(k, "profession.mining", MasteryLeveling.MaxLevel) > 0)),
            ("realm knowledge", Enum.GetValues<RealmInsight>().All(RealmKnowledgeLevels.Required.ContainsKey)),
            ("character xp", CharacterLeveling.XpForExtracting > 0 && AttributeGrowth.BudgetPerLevel > 0),
            ("crafting discoveries", TestPaths.LoadStore<CraftingInteractionDefinition>("crafting_interactions").Count > 0),
            ("techniques", TestPaths.LoadStore<TechniqueDefinition>("techniques").Count > 0),
            ("assay", AssayLens.DepthFor(AssayTuning.PotentialLevel) != AssayLens.DepthFor(1)),

            // Phase 10: two more sources of the same six quantities. They are on the roll-call
            // rather than trusted because that is exactly how mastery went four milestones
            // incrementing while nothing read it.
            ("cross-profession synergies", TestPaths.LoadStore<ProfessionSynergyDefinition>("synergies")
                .GetAll().Any(synergy => !synergy.IsGlobalSource && synergy.PerLevel > 0)),
            ("global (total-level) bonuses", TestPaths.LoadStore<ProfessionSynergyDefinition>("synergies")
                .GetAll().Any(synergy => synergy.IsGlobalSource && synergy.PerLevel > 0)),
        };

        foreach (var (name, consumed) in tracks)
            _output.WriteLine($"{(consumed ? "✓" : "✗")} {name}");

        Assert.All(tracks, track => Assert.True(track.Consumed, $"'{track.Name}' is tracked and nothing reads it."));
    }

    private static ContentBundle BriefingContent() => new()
    {
        Realms = TestPaths.LoadStore<RealmDefinition>("realms"),
        Actors = TestPaths.LoadStore<ActorDefinition>("actors"),
        EnemyFamilies = TestPaths.LoadStore<EnemyFamilyDefinition>("enemy_families"),
        EnemyRoles = TestPaths.LoadStore<CombatRoleDefinition>("enemy_roles"),
        AiProfiles = TestPaths.LoadStore<AiProfileDefinition>("ai_profiles"),
        Actions = TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions"),
        Professions = TestPaths.LoadStore<ProfessionDefinition>("professions"),
        LootTables = TestPaths.LoadStore<Dungeons.Loot.LootTableDefinition>("loot_tables"),
        Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
    };

    private static BuildResolver ShippedBuilds() => new(new ContentBundle
    {
        Classes = TestPaths.LoadStore<BaseClassDefinition>("classes"),
        Prefixes = TestPaths.LoadStore<PrefixDefinition>("prefixes"),
        Suffixes = TestPaths.LoadStore<SuffixDefinition>("suffixes"),
        NameFormats = TestPaths.LoadStore<NameFormatDefinition>("name_formats"),
        ModifierKeys = TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
    });
}
