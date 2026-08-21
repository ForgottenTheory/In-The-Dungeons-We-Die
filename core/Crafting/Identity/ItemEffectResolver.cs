using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Randomness;

namespace Dungeons.Crafting.Identity;

/// <summary>One row of the scored candidate table — what the preview shows and exactly what
/// the draws come from (the projection cannot lie).</summary>
public sealed record ScoredSentenceCandidate(
    string TriggerId,
    string BehaviorId,
    string PayloadId,
    double Score,
    /// <summary>True when the payload's family is not opened by any expressed identity and
    /// eligibility came from an authored favored-payload entry — the §9 breach path.</summary>
    bool FromProfileBreach);

/// <summary>
/// The deterministic half of a resolution — everything knowable before dice: the guaranteed
/// floor, the scored candidate table, and the odds. This IS the preview.
/// </summary>
public sealed record ItemEffectProjection(
    IReadOnlyList<ItemEffectSentence> Floor,
    IReadOnlyList<ScoredSentenceCandidate> Candidates,
    int GeneratedSentenceCount,
    double SignatureChance,
    double DrawbackChance);

/// <summary>A committed resolution: the projection it was drawn from, and every sentence the
/// item now carries — floor first, then generated, then signature, then drawback.</summary>
public sealed record ItemEffectResolution(
    ItemEffectProjection Projection,
    IReadOnlyList<ItemEffectSentence> Sentences);

/// <summary>
/// The item-effect pipeline (D50, docs/identity-foundation.md §8) — the one generator every
/// minted item's effects come from, succeeding the genome → <c>ModifierGenerator</c> path.
/// Three output categories, kept apart: the identity floor (guaranteed), ordinary generated
/// effects (weighted), and optional Signatures (earned). <see cref="SignatureResolver"/>
/// names the Signature-specific stage.
///
/// <para><b>Project/Resolve parity:</b> <see cref="Project"/> computes everything
/// deterministic; <see cref="Resolve"/> runs the same projection and then draws. The scored
/// table the player sees is the distribution the dice use — "I am engineering the odds,"
/// never "I pulled a lever."</para>
/// </summary>
public sealed class ItemEffectResolver
{
    private readonly ContentBundle _content;

    public ItemEffectResolver(ContentBundle content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    // ---- The deterministic half ------------------------------------------------------------

    public ItemEffectProjection Project(IdentityComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        var floor = FloorSentences(composition);
        var candidates = ScoreCandidates(composition, floor);

        var generatedCount = candidates.Count == 0 ? 0 : Math.Min(
            ItemEffectTuning.MaxGeneratedSentences,
            ItemEffectTuning.GeneratedSentenceBaseCount
                + (composition.Quality >= ItemEffectTuning.HighQualityBonusThreshold ? 1 : 0)
                + (composition.WildestComponentStability >= Stability.Unstable ? 1 : 0));

        return new ItemEffectProjection(
            floor,
            candidates,
            generatedCount,
            SignatureResolver.SignatureChance(composition, candidates.Count),
            composition.WildestComponentStability == Stability.Volatile
                ? ItemEffectTuning.DrawbackChanceVolatile
                : 0);
    }

    // ---- The committed resolution ----------------------------------------------------------

    public ItemEffectResolution Resolve(IdentityComposition composition, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var projection = Project(composition);
        var sentences = new List<ItemEffectSentence>(projection.Floor);
        var remaining = new List<ScoredSentenceCandidate>(projection.Candidates);

        for (var i = 0; i < projection.GeneratedSentenceCount && remaining.Count > 0; i++)
        {
            var drawn = DrawWeighted(remaining, random);
            remaining.Remove(drawn);
            sentences.Add(SentenceFrom(drawn, ItemEffectCategory.Generated, composition, random));
        }

        if (remaining.Count >= ItemEffectTuning.SignatureSentenceCount
            && random.NextDouble() < projection.SignatureChance)
        {
            foreach (var candidate in SignatureResolver.CoherentBundle(remaining, _content))
            {
                remaining.Remove(candidate);
                sentences.Add(SentenceFrom(candidate, ItemEffectCategory.Signature, composition, random));
            }
        }

        if (random.NextDouble() < projection.DrawbackChance
            && DrawbackSentence(random) is { } drawback)
        {
            sentences.Add(drawback);
        }

        return new ItemEffectResolution(projection, sentences);
    }

    // ---- Stage: the guaranteed floor (D50 category 1) ---------------------------------------

    /// <summary>Each expressed identity's floor payload, compiled through its authored floor
    /// sentence at a magnitude positioned by quality and deepened by rank — deterministic to
    /// the last digit, which is what "guaranteed" means.</summary>
    private IReadOnlyList<ItemEffectSentence> FloorSentences(IdentityComposition composition)
    {
        var floor = new List<ItemEffectSentence>();

        foreach (var stake in composition.Expressed)
        {
            var floorPayload = _content.SignaturePayloads.GetAll()
                .Where(payload => payload.Floor is not null
                    && payload.Families.Any(family => family.Identity == stake.Id))
                .OrderBy(payload => payload.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (floorPayload?.Floor is not { } floorSentence)
                continue; // an identity whose family has no authored payloads yet grants nothing — content, not code

            var position = RangePosition(composition.Quality)
                + (ItemEffectTuning.FloorPositionPerExtraRank * (stake.Rank - 1));
            floor.Add(new ItemEffectSentence(
                ItemEffectCategory.Floor,
                floorSentence.Trigger,
                floorSentence.Behavior,
                floorPayload.Id,
                MagnitudeAt(floorPayload, position),
                floorSentence.Chance));
        }

        return floor;
    }

    // ---- Stage: candidates and scoring (§8 stages 2–3) --------------------------------------

    private IReadOnlyList<ScoredSentenceCandidate> ScoreCandidates(
        IdentityComposition composition, IReadOnlyList<ItemEffectSentence> floor)
    {
        var openFamilies = composition.Expressed.ToDictionary(
            stake => stake.Id, stake => stake.Rank, StringComparer.Ordinal);
        var favoredPayloadWeights = composition.Profile.FavoredPayloads.ToDictionary(
            lean => lean.Id, lean => lean.Weight, StringComparer.Ordinal);
        var floorShapes = floor
            .Select(sentence => (sentence.TriggerId, sentence.BehaviorId, sentence.PayloadId))
            .ToHashSet();

        var candidates = new List<ScoredSentenceCandidate>();

        foreach (var payload in _content.SignaturePayloads.GetAll().OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            // Open space: any expressed identity whose rank reaches the payload's rung.
            var familyOpens = payload.Families.Any(family =>
                openFamilies.TryGetValue(family.Identity, out var rank) && rank >= family.Rung);

            // The §9 breach: an authored favored payload is eligible even outside the open
            // families — deliberately, in data, with no special-case code. The rung gate is
            // waived with it; that is the breach's entire power.
            var breached = !familyOpens && favoredPayloadWeights.ContainsKey(payload.Id);

            if (!familyOpens && !breached)
                continue;

            foreach (var behaviorId in SentenceAssemblers.CompilableBehaviors.OrderBy(id => id, StringComparer.Ordinal))
            {
                foreach (var trigger in _content.SignatureTriggers.GetAll().OrderBy(t => t.Id, StringComparer.Ordinal))
                {
                    if (!SentenceAssemblers.Accepts(behaviorId, trigger, payload))
                        continue;
                    if (floorShapes.Contains((trigger.Id, behaviorId, payload.Id)))
                        continue; // the floor already promises this exact sentence

                    var score = payload.Weight
                        * LeanFactor(composition.Profile.FavoredTriggers, trigger.Id)
                        * LeanFactor(composition.Profile.FavoredBehaviors, behaviorId)
                        * LeanFactor(composition.Profile.FavoredPayloads, payload.Id)
                        * FormLeanFactor(composition.Form?.GenerationProfile, trigger.Id, behaviorId, payload.Id);

                    candidates.Add(new ScoredSentenceCandidate(trigger.Id, behaviorId, payload.Id, Math.Round(score, 3), breached));
                }
            }
        }

        // Two cuts: per-payload first (the diversity cap — a payload is an idea, its
        // behavior spellings are variations), then the overall table.
        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.PayloadId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.TriggerId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.BehaviorId, StringComparer.Ordinal)
            .GroupBy(candidate => candidate.PayloadId, StringComparer.Ordinal)
            .SelectMany(payloadGroup => payloadGroup.Take(ItemEffectTuning.MaxCandidatesPerPayload))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.PayloadId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.TriggerId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.BehaviorId, StringComparer.Ordinal)
            .Take(ItemEffectTuning.MaxCandidateTableSize)
            .ToList();
    }

    private static double LeanFactor(IReadOnlyList<WeightedLean> leans, string id)
    {
        var lean = leans.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
        return lean is null ? 1.0 : 1.0 + (lean.Weight * ItemEffectTuning.ProfileLeanFactor);
    }

    /// <summary>§8 stage 1's form contribution: the shield's thumb on the on_block scale.</summary>
    private static double FormLeanFactor(
        SignatureProfile? generationProfile, string triggerId, string behaviorId, string payloadId)
    {
        if (generationProfile is null)
            return 1.0;

        var factor = 1.0;
        if (generationProfile.FavoredTriggers.Contains(triggerId, StringComparer.Ordinal))
            factor *= ItemEffectTuning.FormLeanFactor;
        if (generationProfile.FavoredBehaviors.Contains(behaviorId, StringComparer.Ordinal))
            factor *= ItemEffectTuning.FormLeanFactor;
        if (generationProfile.FavoredPayloads.Contains(payloadId, StringComparer.Ordinal))
            factor *= ItemEffectTuning.FormLeanFactor;
        return factor;
    }

    // ---- Stage: ⟨Emergent Phenomena seam⟩ (§8 stage 4) --------------------------------------

    // Reserved between scoring and selection: rare anomaly rules may later inject or bend
    // candidates here. It has a name and a place and nothing else — deliberately not designed
    // (D43); overfill raising Signature odds is its only designed input so far, and that
    // lives in SignatureResolver.SignatureChance.

    // ---- Stage: selection (§8 stage 5) ------------------------------------------------------

    private ItemEffectSentence SentenceFrom(
        ScoredSentenceCandidate candidate,
        ItemEffectCategory category,
        IdentityComposition composition,
        IRandomSource random)
    {
        var payload = _content.SignaturePayloads.GetById(candidate.PayloadId);
        var variance = ((random.NextDouble() * 2.0) - 1.0) * ItemEffectTuning.GeneratedRollVariance;
        return new ItemEffectSentence(
            category,
            candidate.TriggerId,
            candidate.BehaviorId,
            candidate.PayloadId,
            MagnitudeAt(payload, RangePosition(composition.Quality) + variance),
            Chance: 1.0);
    }

    /// <summary>The §10.3 price of minting from the deep end: an ailment aimed at the
    /// wearer. Weighted like any draw, seeded like any draw — and absent when no ailment
    /// payload exists to curse with.</summary>
    private ItemEffectSentence? DrawbackSentence(IRandomSource random)
    {
        var cursePayloads = _content.SignaturePayloads.GetAll()
            .Where(payload => payload.Binding.Kind == PayloadBindingKinds.Status
                && _content.Statuses.TryGetById(payload.Binding.Key, out var status)
                && status.Category == StatusCategory.Ailment)
            .OrderBy(payload => payload.Id, StringComparer.Ordinal)
            .ToList();
        if (cursePayloads.Count == 0)
            return null;

        var curse = cursePayloads[random.NextInt(0, cursePayloads.Count)];
        return new ItemEffectSentence(
            ItemEffectCategory.Drawback,
            TriggerId: "on_hit",
            BehaviorId: "afflict",
            curse.Id,
            MagnitudeAt(curse, ItemEffectTuning.DrawbackRangePosition),
            ItemEffectTuning.DrawbackProcChance,
            AfflictsWearer: true);
    }

    private static ScoredSentenceCandidate DrawWeighted(
        IReadOnlyList<ScoredSentenceCandidate> candidates, IRandomSource random)
    {
        var totalScore = candidates.Sum(candidate => candidate.Score);
        var roll = random.NextDouble() * totalScore;
        foreach (var candidate in candidates)
        {
            roll -= candidate.Score;
            if (roll <= 0)
                return candidate;
        }

        return candidates[^1];
    }

    // ---- Magnitudes -------------------------------------------------------------------------

    /// <summary>Quality's lever: where in the payload range a delivery lands (the proven
    /// roll-position shape, workmanship edition).</summary>
    private static double RangePosition(int quality) =>
        ItemEffectTuning.MinimumRangePosition
        + (ItemEffectTuning.QualityRangeSpan * Math.Clamp(quality, 0, 100) / 100.0);

    private static double MagnitudeAt(SignaturePayloadDefinition payload, double position)
    {
        if (payload.Range.Count < 2)
            return 0;

        var clamped = Math.Clamp(position, 0.0, 1.0);
        return Math.Round(payload.Range[0] + ((payload.Range[1] - payload.Range[0]) * clamped), 4);
    }

    // ---- Emission (§8 stage 6) ---------------------------------------------------------------

    /// <summary>Compiles every sentence to grants through the behavior assemblers, merged
    /// into one bundle for the equip pipeline. Deterministic; callable any number of times
    /// from the persisted sentences.</summary>
    public CompiledSentence CompileAll(IEnumerable<ItemEffectSentence> sentences)
    {
        ArgumentNullException.ThrowIfNull(sentences);

        var statGrants = new List<(string, double, string?)>();
        var rules = new List<Rules.TriggerRule>();
        var gauges = new List<Characters.Composition.GaugeDefinition>();
        var moveModifierIds = new List<string>();

        foreach (var sentence in sentences)
        {
            if (!_content.SignatureTriggers.TryGetById(sentence.TriggerId, out var trigger)
                || !_content.SignaturePayloads.TryGetById(sentence.PayloadId, out var payload))
            {
                continue; // vocabulary the bundle no longer ships — the sentence stays dormant rather than lying
            }

            var compiled = SentenceAssemblers.Compile(sentence, trigger, payload, _content);
            statGrants.AddRange(compiled.StatGrants);
            rules.AddRange(compiled.Rules);
            gauges.AddRange(compiled.Gauges);
            moveModifierIds.AddRange(compiled.MoveModifierIds);
        }

        return new CompiledSentence(statGrants, rules, gauges, moveModifierIds);
    }
}

/// <summary>
/// The Signature-specific stage of the pipeline (D50's naming): when a mint earns the
/// special layer, and which sentences cohere into it. Small on purpose — the Signature is a
/// selection discipline over the same scored space, not a second generator.
/// </summary>
public static class SignatureResolver
{
    /// <summary>Theme resonance (the summed merged-theme weights — §6.1's only job),
    /// overfill wildness (§10.3's designed input to the anomaly seam), and a ceiling that
    /// keeps Signatures earned rather than owed.</summary>
    public static double SignatureChance(IdentityComposition composition, int candidateCount)
    {
        ArgumentNullException.ThrowIfNull(composition);

        if (candidateCount < ItemEffectTuning.SignatureSentenceCount)
            return 0;

        var resonance = composition.Profile.Themes.Sum(theme => theme.Weight);
        var overfillBonus = composition.WildestComponentStability switch
        {
            Stability.Volatile => ItemEffectTuning.SignatureChanceVolatileBonus,
            Stability.Unstable => ItemEffectTuning.SignatureChanceUnstableBonus,
            _ => 0,
        };

        return Math.Clamp(
            ItemEffectTuning.SignatureBaseChance
                + (resonance * ItemEffectTuning.SignatureChancePerResonance)
                + overfillBonus,
            0, ItemEffectTuning.SignatureChanceCeiling);
    }

    /// <summary>The coherence rule (§7.1): the bundle leads with the strongest candidate and
    /// fills with candidates sharing its trigger or an owning identity — sentences that read
    /// as one idea, scored toward coherence rather than enforced by rule.</summary>
    public static IReadOnlyList<ScoredSentenceCandidate> CoherentBundle(
        IReadOnlyList<ScoredSentenceCandidate> candidates, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(content);

        if (candidates.Count == 0)
            return Array.Empty<ScoredSentenceCandidate>();

        var lead = candidates[0];
        var leadFamilies = FamiliesOf(lead, content);
        var bundle = new List<ScoredSentenceCandidate> { lead };

        foreach (var candidate in candidates.Skip(1))
        {
            if (bundle.Count >= ItemEffectTuning.SignatureSentenceCount)
                break;
            var coheres = string.Equals(candidate.TriggerId, lead.TriggerId, StringComparison.Ordinal)
                || FamiliesOf(candidate, content).Overlaps(leadFamilies);
            if (coheres)
                bundle.Add(candidate);
        }

        return bundle;
    }

    private static HashSet<string> FamiliesOf(ScoredSentenceCandidate candidate, ContentBundle content) =>
        content.SignaturePayloads.TryGetById(candidate.PayloadId, out var payload)
            ? payload.Families.Select(family => family.Identity).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
}
