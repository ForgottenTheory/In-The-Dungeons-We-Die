using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Presentation;
using Dungeons.Professions;
using Dungeons.Realms;
using Xunit;
using Xunit.Abstractions;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// The pre-run briefing, run against the <b>real</b> Dark Forest.
///
/// <para>These tests express a design rule rather than a code path: <b>Realm Knowledge unlocks
/// information, never power</b> (GDD §11.4), and the preparation screen must show exactly what
/// has been earned and nothing else. A briefing that leaks a hidden node hands the player a
/// destination the run then refuses to walk to; one that hides an earned insight makes the whole
/// track feel inert.</para>
///
/// <para>Thresholds are read from <see cref="RealmKnowledgeLevels"/> rather than written down
/// here, so retuning the ladder cannot break these tests — the <em>order</em> is the design, and
/// that is what they hold.</para>
/// </summary>
public class RealmBriefingTests
{
    private readonly ITestOutputHelper _output;

    public RealmBriefingTests(ITestOutputHelper output) => _output = output;

    /// <summary>Only what a briefing actually reads — enemies, professions and the realm graph.
    /// Naming the six stores keeps it obvious what this read-model can and cannot reach.</summary>
    private static ContentBundle BriefingContent() => new()
    {
        Realms = TestPaths.LoadStore<RealmDefinition>("realms"),
        Actors = TestPaths.LoadStore<ActorDefinition>("actors"),
        EnemyFamilies = TestPaths.LoadStore<EnemyFamilyDefinition>("enemy_families"),
        EnemyRoles = TestPaths.LoadStore<CombatRoleDefinition>("enemy_roles"),
        AiProfiles = TestPaths.LoadStore<AiProfileDefinition>("ai_profiles"),
        Actions = TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions"),
        Professions = TestPaths.LoadStore<ProfessionDefinition>("professions"),

        // Phase 8: "what this place yields" is walked out of the realm's own loot tables, so
        // omitting these would leave that whole section silently empty and untested.
        LootTables = TestPaths.LoadStore<Dungeons.Loot.LootTableDefinition>("loot_tables"),
        Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
    };

    private static RealmBriefing DarkForestAt(int knowledge)
    {
        var content = BriefingContent();
        return RealmBriefing.Compile(content, content.Realms.GetById("realm.dark_forest"), knowledge);
    }

    private static int Needs(RealmInsight insight) => RealmKnowledgeLevels.Required[insight];

    /// <summary>One point below an insight's threshold — the exact edge each gate is tested on.</summary>
    private static RealmBriefing JustBelow(RealmInsight insight) => DarkForestAt(Needs(insight) - 1);

    // --- Nothing is free ----------------------------------------------------

    /// <summary>
    /// A party that has never been here knows the place exists and nothing else. This is the
    /// whole premise: the first run is walked blind, and every later one is walked better.
    /// </summary>
    [Fact]
    public void AtZeroKnowledgeTheRealmGivesUpNothingButItsName()
    {
        var briefing = DarkForestAt(0);

        Assert.Equal("The Dark Forest", briefing.RealmName);
        Assert.Equal(3, briefing.MaxDepth);
        Assert.Empty(briefing.Unlocked);

        Assert.Empty(briefing.Threats);
        Assert.Empty(briefing.Hazards);
        Assert.Empty(briefing.Resources);
        Assert.Empty(briefing.Routes);
    }

    [Fact]
    public void AtZeroKnowledgeTheNextInsightIsTheFirstRungOfTheLadder()
    {
        var next = DarkForestAt(0).NextInsight;

        Assert.NotNull(next);
        Assert.Equal(RealmInsight.CommonResources, next!.Value.Insight);
        Assert.Equal(Needs(RealmInsight.CommonResources), next.Value.Required);
    }

    [Fact]
    public void AtFullKnowledgeEveryInsightIsEarnedAndThereIsNothingLeftToLearn()
    {
        var briefing = DarkForestAt(Needs(RealmInsight.DeepEntry));

        Assert.Equal(RealmKnowledgeLevels.Required.Count, briefing.Unlocked.Count);
        Assert.Null(briefing.NextInsight);
    }

    // --- Each gate opens on its own threshold, and only its own -------------

    /// <summary>
    /// The cheapest rung, and the reason it exists: a first expedition should come home knowing
    /// <em>something</em>. Walked out of the node and creature loot tables the realm already
    /// points at — zero new content, so it cannot go stale against the tables.
    /// </summary>
    [Fact]
    public void WhatThePlaceYieldsAppearsExactlyAtTheCommonResourceThreshold()
    {
        Assert.Empty(JustBelow(RealmInsight.CommonResources).Yield);

        var known = DarkForestAt(Needs(RealmInsight.CommonResources)).Yield;

        Assert.NotEmpty(known);
        Assert.All(known, entry => Assert.False(string.IsNullOrWhiteSpace(entry.MaterialName)));
    }

    /// <summary>Ordinary first, so the rare end is the part the screen can afford to cut.</summary>
    [Fact]
    public void TheYieldListIsOrderedByHowOrdinaryThingsAre()
    {
        var yield = DarkForestAt(Needs(RealmInsight.CommonResources)).Yield;
        var rarities = yield.Select(entry => entry.Rarity).ToList();

        Assert.Equal(rarities.OrderBy(rarity => rarity), rarities);
    }

    /// <summary>The whole track's premise: the twentieth run knows more than the first. Later
    /// rungs reveal hidden nodes, so the yield list can only grow.</summary>
    [Fact]
    public void KnowingMoreNeverYieldsLess()
    {
        var early = DarkForestAt(Needs(RealmInsight.CommonResources)).Yield.Count;
        var late = DarkForestAt(Needs(RealmInsight.DeepEntry)).Yield.Count;

        Assert.True(late >= early);
    }

    /// <summary>Deep entry is the last rung, and the briefing is where the preparation screen
    /// reads it.</summary>
    [Fact]
    public void TheDeepestEntryOpensOnlyAtTheLastRung()
    {
        Assert.Equal(1, JustBelow(RealmInsight.DeepEntry).DeepestEntry);
        Assert.Equal(3, DarkForestAt(Needs(RealmInsight.DeepEntry)).DeepestEntry);
    }

    [Fact]
    public void ThreatsAppearExactlyAtTheEnemyWeaknessThreshold()
    {
        Assert.Empty(JustBelow(RealmInsight.EnemyWeaknesses).Threats);
        Assert.NotEmpty(DarkForestAt(Needs(RealmInsight.EnemyWeaknesses)).Threats);
    }

    [Fact]
    public void HazardsAppearExactlyAtTheHazardThreshold()
    {
        Assert.Empty(JustBelow(RealmInsight.Hazards).Hazards);
        Assert.NotEmpty(DarkForestAt(Needs(RealmInsight.Hazards)).Hazards);
    }

    [Fact]
    public void RichWorkingsAppearExactlyAtTheRichNodeThreshold()
    {
        Assert.Empty(JustBelow(RealmInsight.RichNodes).Resources);
        Assert.NotEmpty(DarkForestAt(Needs(RealmInsight.RichNodes)).Resources);
    }

    /// <summary>Knowing where the enemies are does not tell you where the exits are. Each rung
    /// buys its own thing — that is what makes the ladder an arc rather than one switch.</summary>
    [Fact]
    public void KnowingWhatLivesHereRevealsNothingElse()
    {
        var briefing = DarkForestAt(Needs(RealmInsight.EnemyWeaknesses));

        Assert.NotEmpty(briefing.Threats);
        Assert.Empty(briefing.Hazards);
        Assert.Empty(briefing.Resources);
        Assert.Empty(briefing.Routes);
    }

    // --- Hidden nodes -------------------------------------------------------

    /// <summary>
    /// The Dark Forest hides three nodes, and they must not leak through <em>any</em> section.
    /// A hidden combat node showing up in Known Threats at 30 knowledge would spoil the reward
    /// the whole HiddenRoutes rung exists to sell.
    /// </summary>
    [Fact]
    public void HiddenNodesLeakThroughNoSectionBeforeTheRoutesAreLearned()
    {
        var realm = BriefingContent().Realms.GetById("realm.dark_forest");
        var hiddenNames = realm.Locations.Where(location => location.Hidden)
            .Select(location => location.Name).ToHashSet();

        Assert.NotEmpty(hiddenNames);

        // Everything short of HiddenRoutes, so every other gate is wide open.
        var briefing = JustBelow(RealmInsight.HiddenRoutes);

        Assert.DoesNotContain(briefing.Threats, threat => hiddenNames.Contains(threat.LocationName));
        Assert.DoesNotContain(briefing.Hazards, hazard => hiddenNames.Contains(hazard.Name));
        Assert.DoesNotContain(briefing.Resources, resource => hiddenNames.Contains(resource.LocationName));
        Assert.DoesNotContain(briefing.Routes, route => hiddenNames.Contains(route.Name));
    }

    [Fact]
    public void HiddenNodesAppearAsRoutesOnceTheRoutesAreLearned()
    {
        var briefing = DarkForestAt(Needs(RealmInsight.HiddenRoutes));

        Assert.NotEmpty(briefing.Routes);
        Assert.All(briefing.Routes, route => Assert.True(route.Hidden));
    }

    /// <summary>
    /// The <b>marked</b> exits and stairs are what the ExtractionRoutes rung buys.
    ///
    /// <para>A <em>hidden</em> exit is a different question and arrives earlier: the Dark Forest's
    /// Split Trunk is a hidden Extraction node, and finding a node reveals what it is — the same
    /// rule the run itself uses, where standing on it lets you extract regardless of what you
    /// have learned. So this asserts the rule on the nodes the insight is actually about.</para>
    /// </summary>
    [Fact]
    public void MarkedExitsAndStairsAppearOnlyAtTheExtractionThreshold()
    {
        Assert.DoesNotContain(JustBelow(RealmInsight.ExtractionRoutes).Routes,
            route => !route.Hidden && route.Type is RealmLocationType.Extraction or RealmLocationType.Descent);

        Assert.Contains(DarkForestAt(Needs(RealmInsight.ExtractionRoutes)).Routes,
            route => !route.Hidden && route.Type == RealmLocationType.Extraction);
    }

    // --- What a threat reading actually says --------------------------------

    /// <summary>The elite and the boss are tagged rather than typed (D26), and the briefing has
    /// to say so — walking into Thornheart unaware is the one thing knowledge should prevent.</summary>
    [Fact]
    public void TheEliteAndTheBossAreBothFlagged()
    {
        var threats = DarkForestAt(Needs(RealmInsight.EnemyWeaknesses)).Threats;

        Assert.Contains(threats, threat => threat.Rank == EnemyRank.Elite);
        Assert.Contains(threats, threat => threat.Rank == EnemyRank.Boss);
        Assert.Contains(threats, threat => threat.Rank == EnemyRank.Normal);
    }

    /// <summary>
    /// A negative resistance is a real weakness — it is how a troll burns — and the in-run intel
    /// drops it on the floor by listing only what a creature shrugs off. The briefing splits on
    /// the sign, and this pins that at least one Dark Forest creature reads as burnable.
    /// </summary>
    [Fact]
    public void ALaneACreatureBurnsToIsReportedAsExposedNotOmitted()
    {
        var threats = DarkForestAt(Needs(RealmInsight.EnemyWeaknesses)).Threats;

        Assert.Contains(threats, threat => threat.ExposedLanes.Count > 0);
        Assert.All(threats, threat =>
        {
            Assert.All(threat.ExposedLanes, lane => Assert.True(lane.Fraction < 0));
            Assert.All(threat.ResistedLanes, lane => Assert.True(lane.Fraction > 0));
        });
    }

    /// <summary>No lane can be both, or the screen would tell the player two opposite things.</summary>
    [Fact]
    public void NoLaneIsBothResistedAndExposed()
    {
        foreach (var threat in DarkForestAt(Needs(RealmInsight.EnemyWeaknesses)).Threats)
        {
            var resisted = threat.ResistedLanes.Select(lane => lane.Lane).ToHashSet();
            Assert.DoesNotContain(threat.ExposedLanes, lane => resisted.Contains(lane.Lane));
        }
    }

    [Fact]
    public void EveryThreatNamesWhereItIsAndHowDeep()
    {
        foreach (var threat in DarkForestAt(Needs(RealmInsight.EnemyWeaknesses)).Threats)
        {
            Assert.False(string.IsNullOrWhiteSpace(threat.Name));
            Assert.False(string.IsNullOrWhiteSpace(threat.LocationName));
            Assert.InRange(threat.Depth, 1, 3);
        }
    }

    // --- Resources ----------------------------------------------------------

    /// <summary>A working the player cannot yet work is still worth knowing about — that is what
    /// makes it a goal. So the level is reported, never used to filter.</summary>
    [Fact]
    public void EveryRichWorkingNamesItsTradeAndTheLevelItAsksFor()
    {
        var resources = DarkForestAt(Needs(RealmInsight.RichNodes)).Resources;

        Assert.NotEmpty(resources);
        Assert.All(resources, resource =>
        {
            Assert.False(string.IsNullOrWhiteSpace(resource.ProfessionName));
            Assert.False(string.IsNullOrWhiteSpace(resource.ActionName));
            Assert.True(resource.RequiredLevel >= 1);
        });
    }

    // --- Every realm compiles ------------------------------------------------

    /// <summary>
    /// 164 realms ship, and the preparation screen can point at any of them. A briefing that
    /// threw on a roster realm with no combat nodes would break the screen for 163 destinations.
    /// </summary>
    [Fact]
    public void EveryShippedRealmCompilesAtEveryRungOfTheLadder()
    {
        var content = BriefingContent();
        var levels = new[] { 0 }.Concat(RealmKnowledgeLevels.Required.Values).ToList();

        foreach (var realm in content.Realms.GetAll())
        foreach (var knowledge in levels)
        {
            var briefing = RealmBriefing.Compile(content, realm, knowledge);
            Assert.Equal(realm.Id, briefing.RealmId);
        }
    }

    [Fact]
    public void RenderTheDarkForestBriefingLadder()
    {
        foreach (var knowledge in new[] { 0 }.Concat(RealmKnowledgeLevels.Required.Values))
        {
            var briefing = DarkForestAt(knowledge);
            _output.WriteLine($"— Knowledge {knowledge}: {briefing.Unlocked.Count} insight(s) — "
                + $"{briefing.Yield.Count} material(s), {briefing.Threats.Count} threat(s), "
                + $"{briefing.Hazards.Count} hazard(s), {briefing.Resources.Count} working(s), "
                + $"{briefing.Routes.Count} route(s), enter at depth ≤{briefing.DeepestEntry}");

            foreach (var threat in briefing.Threats)
                _output.WriteLine($"    {threat.Name} [{threat.Rank}] d{threat.Depth} — "
                    + $"hit with {string.Join("/", threat.VulnerableDamageTypes)}; "
                    + $"burns to {string.Join("/", threat.ExposedLanes.Select(lane => lane.Lane))}; "
                    + $"shrugs off {string.Join("/", threat.ResistedLanes.Select(lane => lane.Lane))}");
        }
    }
}
