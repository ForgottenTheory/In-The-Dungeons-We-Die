using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
using Dungeons.Randomness;
using Dungeons.Rules;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The item-effect pipeline (migration Phase 3 — D50, docs/identity-foundation.md §8) over
/// the real shipped vocabulary: the floor is guaranteed and deterministic, rank gates the
/// rungs, authored favored payloads breach the open families (§9), the form leans the same
/// material two ways (reality test 5), overfill widens generation and mints drawbacks
/// (§10.3), Signatures are earned and coherent (§7.1), and everything the assemblers compile
/// survives the same validator authored rules do — the D30 fence, end to end.
/// </summary>
public class ItemEffectResolverTests
{
    private const string Vital = "identity.vital";
    private const string Dense = "identity.dense";

    // --- The guaranteed floor (D50 category 1) -------------------------------

    [Fact]
    public void TheFloorIsGuaranteedAndDeterministic()
    {
        var content = Content();
        var composition = LongswordComposition(content, DenseVitalIron(content));

        var first = new ItemEffectResolver(content).Project(composition);
        var second = new ItemEffectResolver(content).Project(composition);

        Assert.Equal(2, first.Floor.Count); // one floor expression per expressed identity
        Assert.All(first.Floor, sentence => Assert.Equal(ItemEffectCategory.Floor, sentence.Category));
        Assert.Contains(first.Floor, sentence => sentence.PayloadId == "impact");
        Assert.Contains(first.Floor, sentence => sentence.PayloadId == "vitality");
        Assert.Equal(first.Floor, second.Floor);
    }

    [Fact]
    public void TheFloorDeepensWithRankWithoutDice()
    {
        var content = Content();
        var iron = content.Materials.GetById("material.iron_ingot");
        IdentityComposition At(int rank) => LongswordComposition(content,
            IdentityStateResolver.StateOf(iron)! with
            {
                Identities = new[] { new IdentityStake(Vital, rank) },
            });

        var shallow = new ItemEffectResolver(content).Project(At(1)).Floor.Single();
        var deep = new ItemEffectResolver(content).Project(At(3)).Floor.Single();

        Assert.True(deep.Magnitude > shallow.Magnitude,
            $"rank 3 floor ({deep.Magnitude}) should out-deliver rank 1 ({shallow.Magnitude}).");
    }

    // --- Candidates: rungs, breaches, form lean ------------------------------

    [Fact]
    public void RankGatesHowDeepIntoAFamilyGenerationReaches()
    {
        var content = Content();
        var iron = content.Materials.GetById("material.iron_ingot");
        IReadOnlyList<ScoredSentenceCandidate> CandidatesAt(int rank) =>
            new ItemEffectResolver(content).Project(LongswordComposition(content,
                IdentityStateResolver.StateOf(iron)! with
                {
                    Identities = new[] { new IdentityStake(Vital, rank) },
                })).Candidates;

        Assert.DoesNotContain(CandidatesAt(1), candidate => candidate.PayloadId == "second_wind");
        Assert.Contains(CandidatesAt(2), candidate => candidate.PayloadId == "second_wind");
    }

    [Fact]
    public void AnAuthoredFavoredPayloadBreachesTheOpenFamilies()
    {
        // §9: oak's profile favors barrier — Warded turf, and nothing here carries Warded.
        // The authored lean alone makes it eligible, flagged as the breach it is.
        var content = Content();
        var projection = new ItemEffectResolver(content)
            .Project(BucklerComposition(content, OakboundVitalIron(content)));

        var barrier = projection.Candidates.Where(candidate => candidate.PayloadId == "barrier");
        Assert.NotEmpty(barrier);
        Assert.All(barrier, candidate => Assert.True(candidate.FromProfileBreach));
    }

    [Fact]
    public void TheFormLeansTheSameMaterialTwoWays()
    {
        // Reality test 5, pinned: identical floors (proved above), opposite generation
        // leans. The buckler's table leads with blocking; the longsword's with landing hits.
        var content = Content();
        var material = DenseVitalIron(content);

        var onLongsword = new ItemEffectResolver(content)
            .Project(LongswordComposition(content, material));
        var onBuckler = new ItemEffectResolver(content)
            .Project(BucklerComposition(content, material));

        Assert.Contains(onLongsword.Candidates[0].TriggerId, new[] { "on_hit", "on_crit" });
        Assert.Contains(onBuckler.Candidates[0].TriggerId, new[] { "on_block", "on_being_struck" });
    }

    // --- Selection: determinism, widening, drawbacks, signatures -------------

    [Fact]
    public void TheSameSeedMintsTheSameSentences()
    {
        var content = Content();
        var composition = LongswordComposition(content, DenseVitalIron(content));

        var first = new ItemEffectResolver(content).Resolve(composition, new SeededRandom(7));
        var second = new ItemEffectResolver(content).Resolve(composition, new SeededRandom(7));

        Assert.Equal(first.Sentences, second.Sentences);
    }

    [Fact]
    public void AnOverfilledComponentWidensGeneration()
    {
        var content = Content();
        var iron = content.Materials.GetById("material.iron_ingot");
        var stable = IdentityStateResolver.StateOf(iron)! with
        {
            Identities = new[] { new IdentityStake(Dense, 1) },
        };
        var unstable = stable with
        {
            Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 1) },
            Capacity = 1,
        };
        Assert.Equal(Stability.Unstable, unstable.Stability);

        var resolver = new ItemEffectResolver(content);
        var calm = resolver.Project(LongswordComposition(content, stable));
        var wild = resolver.Project(LongswordComposition(content, unstable));

        Assert.Equal(calm.GeneratedSentenceCount + 1, wild.GeneratedSentenceCount);
        Assert.True(wild.SignatureChance > calm.SignatureChance,
            "overfill raises the odds the mint turns special (§10.3).");
    }

    [Fact]
    public void AVolatileMintCanCarryADrawbackAimedAtTheWearer()
    {
        var content = Content();
        var iron = content.Materials.GetById("material.iron_ingot");
        var volatileIron = IdentityStateResolver.StateOf(iron)! with
        {
            Identities = new[]
            {
                new IdentityStake(Dense, 1), new IdentityStake(Vital, 1), new IdentityStake("identity.ember", 1),
            },
            Capacity = 1,
        };
        Assert.Equal(Stability.Volatile, volatileIron.Stability);

        var resolution = new ItemEffectResolver(content).Resolve(
            LongswordComposition(content, volatileIron), new AlwaysLands());

        var drawback = Assert.Single(resolution.Sentences, s => s.Category == ItemEffectCategory.Drawback);
        Assert.True(drawback.AfflictsWearer);
        // The curse is always an ailment aimed at the wearer — which one is a seeded pick
        // from every ailment payload shipped (one at Phase 3, a roster of them since D52).
        var cursePayload = content.SignaturePayloads.GetById(drawback.PayloadId);
        var curseStatus = content.Statuses.GetById(cursePayload.Binding.Key);
        Assert.Equal(StatusCategory.Ailment, curseStatus.Category);
    }

    [Fact]
    public void AStableMintNeverCarriesADrawback()
    {
        var content = Content();
        var resolution = new ItemEffectResolver(content).Resolve(
            LongswordComposition(content, DenseVitalIron(content)), new AlwaysLands());

        Assert.DoesNotContain(resolution.Sentences, s => s.Category == ItemEffectCategory.Drawback);
    }

    [Fact]
    public void AnEarnedSignatureBundlesCoherentSentences()
    {
        var content = Content();
        var resolution = new ItemEffectResolver(content).Resolve(
            BucklerComposition(content, OakboundVitalIron(content)), new AlwaysLands());

        var signature = resolution.Sentences.Where(s => s.Category == ItemEffectCategory.Signature).ToList();
        Assert.Equal(ItemEffectTuning.SignatureSentenceCount, signature.Count);

        // Coherence: the bundle shares a trigger or an owning identity (§7.1).
        var lead = signature[0];
        var leadFamilies = content.SignaturePayloads.GetById(lead.PayloadId)
            .Families.Select(f => f.Identity).ToHashSet(StringComparer.Ordinal);
        foreach (var sentence in signature.Skip(1))
        {
            var families = content.SignaturePayloads.GetById(sentence.PayloadId)
                .Families.Select(f => f.Identity);
            Assert.True(sentence.TriggerId == lead.TriggerId || families.Any(leadFamilies.Contains),
                "a Signature's sentences must read as one idea — shared trigger or shared family.");
        }
    }

    // --- Emission: the assemblers against the machinery (the D30 fence) ------

    [Fact]
    public void EveryCompiledRuleSurvivesTheSameValidatorAuthoredRulesDo()
    {
        var content = Content();
        var resolver = new ItemEffectResolver(content);
        var resolution = resolver.Resolve(
            BucklerComposition(content, OakboundVitalIron(content)), new AlwaysLands());

        var compiled = resolver.CompileAll(resolution.Sentences);
        var problems = new List<ContentProblem>();
        foreach (var rule in compiled.Rules)
            ContentValidator.ValidateTriggerRule(rule, $"compiled {rule.Id}", content.ModifierKeys, problems);
        foreach (var gauge in compiled.Gauges)
            foreach (var feed in gauge.Feeds)
                ContentValidator.ValidateTriggerRule(feed, $"compiled gauge feed", content.ModifierKeys, problems);

        Assert.True(problems.Count == 0,
            "compiled sentences must bind to machinery that resolves:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));

        foreach (var (modifierKey, _, _) in compiled.StatGrants)
            Assert.True(content.ModifierKeys.Contains(modifierKey),
                $"compiled stat grant targets unregistered key '{modifierKey}'.");
    }

    [Fact]
    public void ASustainFloorCompilesToAStandingStatGrant()
    {
        var content = Content();
        var resolver = new ItemEffectResolver(content);
        var floor = resolver.Project(LongswordComposition(content, DenseVitalIron(content))).Floor;

        var compiled = resolver.CompileAll(floor);

        Assert.Contains(compiled.StatGrants, grant => grant.ModifierKey == "resource.max_health");
        Assert.Contains(compiled.StatGrants, grant => grant.ModifierKey == "combat.damage.flat");
        Assert.Empty(compiled.Rules); // while_worn floors are standing grants, not hooks
    }

    [Fact]
    public void AfflictSendsStateStatusesToTheWearerAndAilmentsToTheEnemy()
    {
        var content = Content();
        var resolver = new ItemEffectResolver(content);
        CompiledSentence Compiled(string payloadId) => resolver.CompileAll(new[]
        {
            new ItemEffectSentence(ItemEffectCategory.Generated, "on_block", "afflict", payloadId, 5, 1.0),
        });

        var barrierRule = Compiled("barrier").Rules.Single();
        Assert.Equal(EffectTarget.Self, barrierRule.Effect.Target);

        var burnRule = Compiled("kindling").Rules.Single();
        Assert.Equal(EffectTarget.TriggerTarget, burnRule.Effect.Target);
    }

    [Fact]
    public void EveryShippedBehaviorHasAnAssembler()
    {
        // The behavior registry may only ship what the assemblers can compile — the same
        // pin OnlyMachineryBackedBehaviorsShip holds from the content side.
        var shipped = TestPaths.LoadStore<SignatureBehaviorDefinition>("signature_behaviors")
            .GetAll().Select(behavior => behavior.Id).OrderBy(id => id, StringComparer.Ordinal);

        Assert.Equal(shipped, SentenceAssemblers.CompilableBehaviors.OrderBy(id => id, StringComparer.Ordinal));
    }

    // --- Harness -------------------------------------------------------------

    private static ContentBundle Content() => new()
    {
        Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
        Forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms"),
        Identities = TestPaths.LoadStore<IdentityDefinition>("identities"),
        SignatureTriggers = TestPaths.LoadStore<SignatureTriggerDefinition>("signature_triggers"),
        SignatureBehaviors = TestPaths.LoadStore<SignatureBehaviorDefinition>("signature_behaviors"),
        SignatureThemes = TestPaths.LoadStore<SignatureThemeDefinition>("signature_themes"),
        SignaturePayloads = TestPaths.LoadStore<SignaturePayloadDefinition>("signature_payloads"),
        Statuses = TestPaths.LoadStore<StatusDefinition>("statuses"),
        ModifierKeys = TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
        Moves = TestPaths.LoadStore<MoveDefinition>("moves"),
    };

    /// <summary>Dense+Vital iron — reality test 4's material, minus the oak personality.</summary>
    private static IdentityMaterialState DenseVitalIron(ContentBundle content) =>
        IdentityStateResolver.StateOf(content.Materials.GetById("material.iron_ingot"))! with
        {
            Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 1) },
        };

    /// <summary>Reality test 4's actual material: Dense Oakbound Iron — oak in the roots, so
    /// oak's authored personality (store, on_block, barrier) enters generation.</summary>
    private static IdentityMaterialState OakboundVitalIron(ContentBundle content) =>
        DenseVitalIron(content) with
        {
            Roots = new[]
            {
                new ProvenanceRoot("material.iron_ingot", 0.85),
                new ProvenanceRoot("material.oak", 0.15),
            },
        };

    private static IdentityComposition LongswordComposition(
        ContentBundle content, IdentityMaterialState metalState)
    {
        var iron = content.Materials.GetById("material.iron_ingot");
        var leather = content.Materials.GetById("material.leather");
        var composition = IdentityEquipmentComposer.Compose(
            content.Forms.GetById("form.longsword"),
            new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
            {
                ["edge"] = (iron, metalState),
                ["core"] = (iron, metalState),
                ["binding"] = (leather, IdentityStateResolver.StateOf(leather)!),
            },
            content);
        Assert.Equal(IdentityCompositionFailure.None, composition.Failure);
        return composition;
    }

    private static IdentityComposition BucklerComposition(
        ContentBundle content, IdentityMaterialState metalState)
    {
        var iron = content.Materials.GetById("material.iron_ingot");
        var composition = IdentityEquipmentComposer.Compose(
            content.Forms.GetById("form.buckler"),
            new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
            {
                ["face"] = (iron, metalState),
            },
            content);
        Assert.Equal(IdentityCompositionFailure.None, composition.Failure);
        return composition;
    }

    /// <summary>Every roll lands (0.0 beats any chance) and every pick takes the first
    /// option — the deterministic worst-case/best-case probe.</summary>
    private sealed class AlwaysLands : IRandomSource
    {
        public double NextDouble() => 0.0;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
