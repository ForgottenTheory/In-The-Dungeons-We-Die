using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Characters.Rules;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Realms;
using Xunit;
using Xunit.Abstractions;

namespace Dungeons.Tests.Realms;

/// <summary>
/// The Dark Forest's numbers, measured against each other.
///
/// <para><b>This is a coherence pass, not a feel pass.</b> Nobody has played this realm, so
/// nothing here claims a fight is fun — only that the numbers are not provably broken: a hazard
/// that deletes a fresh character, a boss that dies to three swings, a merchant price no run can
/// afford, a knowledge threshold no number of runs can reach.</para>
///
/// <para><see cref="RenderTheBalanceSheet"/> prints the whole realm as numbers so the next pass
/// can be made by reading rather than guessing.</para>
/// </summary>
public class DarkForestBalanceTests
{
    private readonly ITestOutputHelper _output;

    public DarkForestBalanceTests(ITestOutputHelper output) => _output = output;

    private static RealmDefinition DarkForest() =>
        TestPaths.LoadStore<RealmDefinition>("realms").GetById("realm.dark_forest");

    /// <summary>The character GameRoot composes on a new game: the shipped default build on a
    /// uniform-5 baseline. Balancing against anything else would be balancing a hypothetical.</summary>
    private static Character FreshCharacter()
    {
        var composer = new CharacterComposer(
            TestPaths.LoadStore<SpeciesDefinition>("species"),
            TestPaths.LoadStore<BaseClassDefinition>("classes"),
            TestPaths.LoadStore<PrefixDefinition>("prefixes"),
            TestPaths.LoadStore<SuffixDefinition>("suffixes"),
            new RuleRegistry(Array.Empty<ICharacterRule>()));

        var build = new CharacterBuild(
            new SpeciesId("species.fey_touched"), new BaseClassId("class.wizard"),
            new PrefixId("prefix.galvanic"), new SuffixId("suffix.questionable_ethics"));

        return new Character(composer.Compose(build, AttributeSet.Uniform(5)));
    }

    private static ResolvedActor Resolve(string actorId) => ActorResolver.Resolve(
        TestPaths.LoadStore<ActorDefinition>("actors").GetById(actorId),
        TestPaths.LoadStore<EnemyFamilyDefinition>("enemy_families"),
        TestPaths.LoadStore<CombatRoleDefinition>("enemy_roles"),
        TestPaths.LoadStore<AiProfileDefinition>("ai_profiles"));

    // ---- Hazards must be survivable, and must still hurt ---------------------------------------

    /// <summary>
    /// <b>No hazard may kill a healthy character outright</b>, and no run may be killed by
    /// hazards alone. A hazard is a toll on a route decision; a hazard that ends the run removes
    /// the decision it exists to create.
    ///
    /// <para>The ceiling is the total across one depth, because the route through a depth can
    /// legitimately cross more than one.</para>
    /// </summary>
    [Fact]
    public void HazardsCostRealHealthWithoutEndingTheRun()
    {
        var character = FreshCharacter();
        var maxHealth = character.Health.Max;
        var realm = DarkForest();

        foreach (var depth in realm.Locations.Select(l => l.Depth).Distinct())
        {
            var hazards = realm.Locations
                .Where(l => l.Depth == depth && l.Type == RealmLocationType.Hazard)
                .ToList();
            if (hazards.Count == 0)
                continue;

            var worst = hazards.Max(h => h.HazardDamage);
            var total = hazards.Sum(h => h.HazardDamage);

            _output.WriteLine($"depth {depth}: hazards {string.Join(" + ", hazards.Select(h => h.HazardDamage))} " +
                              $"= {total} of {maxHealth} health ({(double)total / maxHealth:P0})");

            Assert.True(worst < maxHealth,
                $"depth {depth}'s worst hazard ({worst}) kills a full-health character ({maxHealth}).");
            Assert.True(total < maxHealth,
                $"depth {depth}'s hazards total {total} against {maxHealth} health — the route is a death sentence.");
            Assert.True(worst >= maxHealth * 0.05,
                $"depth {depth}'s worst hazard costs {worst} of {maxHealth} — too little to be a decision.");
        }
    }

    /// <summary>Hazards must escalate with depth, or the deep ones are free.</summary>
    [Fact]
    public void HazardsGetWorseAsYouGoDown()
    {
        var byDepth = DarkForest().Locations
            .Where(l => l.Type == RealmLocationType.Hazard)
            .GroupBy(l => l.Depth)
            .OrderBy(g => g.Key)
            .Select(g => g.Max(l => l.HazardDamage))
            .ToList();

        Assert.Equal(byDepth.OrderBy(d => d), byDepth);
    }

    // ---- A camp must matter, and must not trivialise the depth ---------------------------------

    /// <summary>
    /// A camp has to give back more than a hazard takes at its depth — otherwise resting is
    /// never worth the walk — and less than a full heal, or the depth stops being attritional.
    /// </summary>
    [Fact]
    public void CampsOutweighTheirDepthsHazardsWithoutErasingAttrition()
    {
        var character = FreshCharacter();
        var realm = DarkForest();

        foreach (var camp in realm.Locations.Where(l => l.Type == RealmLocationType.Camp))
        {
            var restored = character.Health.Max * camp.RestoreFraction;
            var hazardsHere = realm.Locations
                .Where(l => l.Depth == camp.Depth && l.Type == RealmLocationType.Hazard)
                .Sum(l => l.HazardDamage);

            _output.WriteLine($"{camp.Id} (depth {camp.Depth}): restores {restored:0} vs {hazardsHere} hazard damage");

            Assert.True(restored > hazardsHere,
                $"{camp.Id} restores {restored:0} but its depth costs {hazardsHere} — resting is not worth the walk.");
            Assert.True(camp.RestoreFraction < 1.0, $"{camp.Id} is a full heal; the depth stops costing anything.");
        }
    }

    // ---- Ranked fights must actually be harder --------------------------------------------------

    /// <summary>
    /// The escalation, in health rather than in adjectives: every ordinary fight, then the
    /// elite, then the boss. If the boss is not the biggest thing in the realm the whole depth
    /// curve is a label.
    /// </summary>
    [Fact]
    public void TheEliteAndBossAreActuallyBigger()
    {
        var realm = DarkForest();

        var fights = realm.Locations
            .Where(l => l.Type == RealmLocationType.Combat && l.ActorId is not null)
            .Select(l => (l.Depth, Actor: Resolve(l.ActorId!)))
            .ToList();

        foreach (var (depth, actor) in fights.OrderBy(f => f.Depth))
            _output.WriteLine($"depth {depth}: {actor.Name,-26} HP {actor.Resources.Health,4}  armour {actor.Armor,4}  resolve {actor.Resolve}");

        var ordinary = fights.Where(f => !f.Actor.Tags.Contains("elite") && !f.Actor.Tags.Contains("boss"))
            .Max(f => f.Actor.Resources.Health);
        var elite = fights.Single(f => f.Actor.Tags.Contains("elite")).Actor.Resources.Health;
        var boss = fights.Single(f => f.Actor.Tags.Contains("boss")).Actor.Resources.Health;

        Assert.True(elite > ordinary, $"the elite ({elite} HP) is no bigger than the biggest ordinary fight ({ordinary}).");
        Assert.True(boss > elite, $"the boss ({boss} HP) is no bigger than the elite ({elite}).");
    }

    /// <summary>
    /// <b>No fight at a deeper depth may be weaker than the toughest fight above it.</b>
    ///
    /// <para>This caught a real inversion: the Goblin Hexer resolved to 24 HP — the caster role
    /// subtracts health — while the depth-1 Raider had 30, so the FIRST fight past the descent
    /// was a step backwards. Casters are meant to be squishy, not easier than the shallows.</para>
    /// </summary>
    [Fact]
    public void NoDeeperFightIsWeakerThanTheOnesAboveIt()
    {
        var byDepth = DarkForest().Locations
            .Where(l => l.Type == RealmLocationType.Combat && l.ActorId is not null)
            .GroupBy(l => l.Depth)
            .OrderBy(g => g.Key)
            .Select(g => (Depth: g.Key, Fights: g.Select(l => Resolve(l.ActorId!)).ToList()))
            .ToList();

        for (var i = 1; i < byDepth.Count; i++)
        {
            var above = byDepth[i - 1];
            var below = byDepth[i];

            var toughestAbove = above.Fights.Max(a => a.Resources.Health);
            var weakestBelow = below.Fights.Min(a => a.Resources.Health);
            var weakling = below.Fights.OrderBy(a => a.Resources.Health).First();

            Assert.True(weakestBelow >= toughestAbove,
                $"depth {below.Depth}'s {weakling.Name} has {weakestBelow} HP, under depth {above.Depth}'s "
                + $"toughest at {toughestAbove} — going deeper got easier.");
        }
    }

    /// <summary>
    /// A boss must be a <em>fight</em>, not a wall: long enough to be an event, short enough to
    /// finish. Measured in swings of the starter weapon, which is the worst case — anyone
    /// reaching depth 3 will hit far harder than this.
    /// </summary>
    [Fact]
    public void TheBossIsAFightAndNotAnAfternoon()
    {
        var moves = TestPaths.LoadStore<MoveDefinition>("moves");
        var starterSwing = moves.GetById("move.rusty_slash").Packets.Sum(p => p.Amount);
        var boss = Resolve("actor.thornheart");

        var swings = boss.Resources.Health / starterSwing;
        _output.WriteLine($"Thornheart: {boss.Resources.Health} HP ÷ {starterSwing} per starter swing = {swings:0} swings");

        Assert.True(swings > 10, $"{swings:0} starter swings is not a boss.");
        Assert.True(swings < 120, $"{swings:0} starter swings is an afternoon, not a fight.");
    }

    // ---- The knowledge ladder has to be climbable ------------------------------------------------

    /// <summary>
    /// <b>Every insight must be reachable.</b> A threshold no amount of play can reach is a
    /// feature that does not exist — and the top one, extraction routes at 42, is the whole
    /// payoff of the track.
    ///
    /// <para>Modelled as one thorough run: entering, walking every node, clearing every fight
    /// and event, both shrines' worth, the hazards, the descents and the extraction.</para>
    /// </summary>
    [Fact]
    public void EveryInsightIsReachableInASaneNumberOfRuns()
    {
        var realm = DarkForest();

        var perRun =
            RealmTuning.KnowledgePerEnter
            + realm.Locations.Count * RealmTuning.KnowledgePerTravel
            + realm.Locations.Count(l => l.Type == RealmLocationType.Event) * RealmTuning.KnowledgePerEvent
            + realm.Locations.Count(l => l.Type == RealmLocationType.Combat) * RealmTuning.KnowledgePerCombatCleared
            + realm.Locations.Count(l => l.Type == RealmLocationType.Shrine) * RealmTuning.KnowledgePerShrine
            + realm.Locations.Count(l => l.Type == RealmLocationType.Hazard) * RealmTuning.KnowledgePerHazardCrossed
            + realm.Locations.Count(l => l.Type == RealmLocationType.Descent) * RealmTuning.KnowledgePerDescend
            + RealmTuning.KnowledgePerExtract;

        var top = RealmKnowledgeLevels.Required.Values.Max();
        _output.WriteLine($"one thorough run ≈ {perRun} knowledge; the top insight needs {top} " +
                          $"(≈ {(double)top / perRun:0.0} runs)");

        foreach (var (insight, required) in RealmKnowledgeLevels.Required.OrderBy(p => p.Value))
            _output.WriteLine($"  {insight,-18} {required,3}  ≈ {(double)required / perRun:0.0} runs");

        Assert.True(perRun > 0);

        // The ladder must be a PROGRESSION, not a formality. This caught the thresholds shipping
        // at 6/12/20/30/42 against a 71-per-run yield: one thorough first run revealed the whole
        // realm, including the hidden routes that are supposed to be the reward for learning it.
        var runsForTheTop = (double)top / perRun;
        Assert.True(runsForTheTop >= 5,
            $"a thorough run pays {perRun} and the top insight needs {top} — {runsForTheTop:0.0} runs "
            + "is not a progression, it is a formality.");
        Assert.True(runsForTheTop <= 20,
            $"{runsForTheTop:0.0} thorough runs to finish the ladder is a grind.");

        // …and the first rung must be reachable inside a single complete run, or a new player
        // gets nothing at all for their first expedition.
        Assert.True(RealmKnowledgeLevels.Required.Values.Min() <= perRun,
            "the first insight cannot be reached in a single complete run.");
    }

    // ---- The merchant has to be affordable, and worth saving for ----------------------------------

    /// <summary>
    /// The trader spends UNSECURED coin, so the price has to sit inside what a run can plausibly
    /// be carrying by the time it reaches her — expensive enough to be a decision, cheap enough
    /// to be a real option.
    /// </summary>
    [Fact]
    public void TheTraderIsAffordableButNotFree()
    {
        var realm = DarkForest();
        var merchant = realm.Locations.Single(l => l.Type == RealmLocationType.Merchant);
        var gold = TestPaths.LoadStore<Dungeons.Loot.LootTableDefinition>("loot_tables");

        // What the shallow half of the realm can pay in coin, at best.
        var reachableGold = realm.Locations
            .Where(l => l.Depth <= merchant.Depth && !string.IsNullOrEmpty(l.LootTableId))
            .Select(l => gold.GetById(l.LootTableId!))
            .Sum(t => t.Gold?.MaxAmount ?? 0);

        _output.WriteLine($"trader asks {merchant.Cost}; nodes down to depth {merchant.Depth} can pay at most {reachableGold}");

        Assert.True(merchant.Cost > 0);
        Assert.True(reachableGold == 0 || merchant.Cost <= reachableGold,
            $"the trader asks {merchant.Cost} and the route to her pays at most {reachableGold} — nobody can ever buy.");
    }

    // ---- The sheet ---------------------------------------------------------------------------------

    [Fact]
    public void RenderTheBalanceSheet()
    {
        var realm = DarkForest();
        var character = FreshCharacter();
        var page = new System.Text.StringBuilder();

        page.AppendLine($"───── The Dark Forest, by the numbers ─────");
        page.AppendLine($"fresh character: {character.Health.Max} HP / {character.Stamina.Max} STA / {character.Mana.Max} MP");

        foreach (var depth in realm.Locations.Select(l => l.Depth).Distinct().OrderBy(d => d))
        {
            page.AppendLine($"\n── Depth {depth} ──");
            foreach (var location in realm.Locations.Where(l => l.Depth == depth))
            {
                var detail = location.Type switch
                {
                    RealmLocationType.Combat when location.ActorId is not null =>
                        $"{Resolve(location.ActorId).Name} — {Resolve(location.ActorId).Resources.Health} HP, armour {Resolve(location.ActorId).Armor}",
                    RealmLocationType.Hazard => $"−{location.HazardDamage} HP ({(double)location.HazardDamage / character.Health.Max:P0})",
                    RealmLocationType.Camp => $"restores {location.RestoreFraction:P0}",
                    RealmLocationType.Merchant => $"{location.Cost} coin",
                    _ => location.LootTableId ?? string.Empty,
                };

                page.AppendLine($"  {(location.Hidden ? "*" : " ")} {location.Type,-11} {location.Name,-26} {detail}");
            }
        }

        page.AppendLine("\n(* = hidden until Realm Knowledge reveals the routes)");
        _output.WriteLine(page.ToString());
    }
}
