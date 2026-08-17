using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Randomness;

namespace Dungeons.Crafting;

/// <summary>Resolves crafts and projects their cost before the player commits (§18).</summary>
public interface IReactionEngine
{
    /// <summary>What the craft would do, without doing it (§6.2c).</summary>
    CraftProjection Project(CraftRequest request);

    /// <summary>Runs the craft: consumes the inputs, registers the result, deposits it.</summary>
    CraftOutcome Resolve(CraftRequest request);
}

/// <summary>
/// The universal crafting pipeline (docs/emergent-item-system.md §8.7) — the thing that
/// replaces recipe matching entirely.
///
/// <para>There are <b>no recipes and no per-combination rules</b>. Every combination of
/// substrate, reagents and process produces a result, computed the same way every time:
/// gate → converge → drift → oppose → charge integrity → derive tags → compute potency →
/// quantize → register → name. Authored content is seven processes and a name grammar.</para>
///
/// <para>Deterministic apart from the variance perturbation (§12.3), which draws from the
/// injected <see cref="IRandomSource"/> — so <see cref="Project"/> can run the identical
/// pipeline with variance switched off and show the player exactly what to expect.</para>
/// </summary>
public sealed class ReactionEngine : IReactionEngine
{
    private readonly ContentBundle _content;
    private readonly Func<Inventory> _inventory;
    private readonly MaterialProfileResolver _profiles;
    private readonly IEmergentRegistry _registry;
    private readonly NameGenerator _names;
    private readonly TagDeriver _tags;
    private readonly ByproductResolver _byproducts;
    private readonly TraitResolver _traitResolver;
    private readonly Func<string, int> _professionLevel;
    private readonly IRandomSource _random;

    public ReactionEngine(
        ContentBundle content,
        Func<Inventory> inventory,
        MaterialProfileResolver profiles,
        IEmergentRegistry registry,
        NameGenerator names,
        TagDeriver tags,
        ByproductResolver byproducts,
        TraitResolver traits,
        Func<string, int> professionLevel,
        IRandomSource random)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _names = names ?? throw new ArgumentNullException(nameof(names));
        _tags = tags ?? throw new ArgumentNullException(nameof(tags));
        _byproducts = byproducts ?? throw new ArgumentNullException(nameof(byproducts));
        _traitResolver = traits ?? throw new ArgumentNullException(nameof(traits));
        _professionLevel = professionLevel ?? throw new ArgumentNullException(nameof(professionLevel));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public CraftProjection Project(CraftRequest request)
    {
        var accepted = AcceptRequest(request);
        if (accepted.Failure != CraftFailure.None)
            return CraftProjection.Failed(accepted.Failure);

        // Variance off: the projection shows the outcome the player is aiming at, and the
        // spread is reported separately as a destruction chance rather than baked into it.
        var reaction = RunReaction(accepted, applyVariance: false);

        return new CraftProjection(
            CraftFailure.None,
            IntegrityCalculator.Project(accepted.SubstrateProfile.Integrity, reaction.TotalCost, reaction.VarianceMagnitude),
            reaction.Potency,
            _names.Generate(reaction.Profile, reaction.Tags, NameIsTaken),
            WouldBeFirstDiscovery: !_registry.Contains(reaction.Signature),
            Preview: reaction.Log,
            Projected: reaction.Profile,
            Steps: reaction.Steps);
    }

    public CraftOutcome Resolve(CraftRequest request)
    {
        var accepted = AcceptRequest(request);
        if (accepted.Failure != CraftFailure.None)
            return CraftOutcome.Failed(accepted.Failure);

        var reaction = RunReaction(accepted, applyVariance: true);
        var inventory = _inventory();

        // Reagents are consumed entirely; the catalyst is not (§8.6).
        inventory.TryRemove(request.SubstrateId, request.Quantity);
        foreach (var reagent in request.ReagentIds)
            inventory.TryRemove(reagent, request.Quantity);

        var log = reaction.LogBuilder;

        if (reaction.Destroyed)
        {
            // §6.2c: destruction is terminal, but never total loss.
            var byproduct = _byproducts.Resolve(reaction.Tags, request.Quantity);
            var byproducts = new List<ItemStack>();

            if (byproduct is not null)
            {
                inventory.Add(byproduct.Value);
                byproducts.Add(byproduct.Value);
            }

            log.Destroyed(accepted.Substrate.Name, byproduct is null ? null : MaterialName(byproduct.Value.ItemId), request.Quantity);

            return new CraftOutcome(
                CraftFailure.None, null, accepted.Substrate.Name, 0,
                IsFirstDiscovery: false, WasDestroyed: true, byproducts, log.Build());
        }

        var name = _names.Generate(reaction.Profile, reaction.Tags, NameIsTaken);
        var registration = _registry.GetOrRegister(reaction.Signature, () => new MaterialDefinition
        {
            Id = reaction.Signature,
            Name = name,
            Tags = reaction.Tags,
            Properties = new Dictionary<string, double>(reaction.Profile.Properties.AsDictionary()),
            Essence = new Dictionary<string, double>(reaction.Profile.Essence),
            Profile = reaction.Profile,
        });

        inventory.Add(registration.Definition.Id, request.Quantity);
        log.Result(registration.Definition.Name, request.Quantity, registration.IsFirstDiscovery);

        return new CraftOutcome(
            CraftFailure.None,
            registration.Definition.Id,
            registration.Definition.Name,
            request.Quantity,
            registration.IsFirstDiscovery,
            WasDestroyed: false,
            Array.Empty<ItemStack>(),
            log.Build());
    }

    // ---- §8.7 step 1: the gate — validate the request and resolve its inputs ---------------

    private AcceptedCraft AcceptRequest(CraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Quantity <= 0)
            return AcceptedCraft.Rejected(CraftFailure.InvalidQuantity);

        if (!_content.Processes.TryGetById(request.ProcessId, out var process))
            return AcceptedCraft.Rejected(CraftFailure.UnknownProcess);

        if (request.ReagentIds.Count == 0)
            return AcceptedCraft.Rejected(CraftFailure.NoReagents);

        if (!_content.Materials.TryGetById(request.SubstrateId, out var substrate))
            return AcceptedCraft.Rejected(CraftFailure.UnknownSubstrate);

        var reagents = new List<MaterialDefinition>();
        foreach (var id in request.ReagentIds)
        {
            if (!_content.Materials.TryGetById(id, out var reagent))
                return AcceptedCraft.Rejected(CraftFailure.UnknownReagent);
            reagents.Add(reagent);
        }

        MaterialDefinition? catalyst = null;
        if (request.CatalystId is { } catalystId)
        {
            if (!_content.Materials.TryGetById(catalystId, out var found))
                return AcceptedCraft.Rejected(CraftFailure.UnknownCatalyst);
            catalyst = found;
        }

        if (!process.IsUngated && _professionLevel(process.Profession) < process.Requires.ProfessionLevel)
            return AcceptedCraft.Rejected(CraftFailure.ProfessionTooLow);

        foreach (var required in process.Requires.SubstrateTags)
        {
            if (!substrate.Tags.Contains(required, StringComparer.OrdinalIgnoreCase))
                return AcceptedCraft.Rejected(CraftFailure.SubstrateRejected);
        }

        if (!HasInputs(request, catalyst))
            return AcceptedCraft.Rejected(CraftFailure.MissingInputs);

        return new AcceptedCraft(
            CraftFailure.None, request, process, substrate, reagents, catalyst,
            _profiles.Resolve(substrate));
    }

    private bool HasInputs(CraftRequest request, MaterialDefinition? catalyst)
    {
        var inventory = _inventory();
        var needed = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [request.SubstrateId] = request.Quantity,
        };

        foreach (var reagent in request.ReagentIds)
            needed[reagent] = needed.GetValueOrDefault(reagent) + request.Quantity;

        // The catalyst is not consumed but must be on hand — you cannot forge over a bed of
        // ash you do not own.
        if (catalyst is not null)
            needed.TryAdd(catalyst.Id, 1);

        return needed.All(pair => inventory.GetQuantity(pair.Key) >= pair.Value);
    }

    // ---- §8.7 steps 2–17: the pipeline ------------------------------------------------------

    private ReactionRun RunReaction(AcceptedCraft accepted, bool applyVariance)
    {
        var request = accepted.Request;
        var process = accepted.Process;
        var log = new ReactionLogBuilder(_content.Properties);

        var materialState = accepted.SubstrateProfile.Properties;
        var essence = (IReadOnlyDictionary<string, double>)new Dictionary<string, double>(
            accepted.SubstrateProfile.Essence, StringComparer.OrdinalIgnoreCase);
        var integrity = accepted.SubstrateProfile.Integrity;
        var totalCost = 0.0;
        var destroyed = false;
        var craftQuality = 0.0;
        var varianceMagnitude = 0.0;
        var steps = new List<ReactionStepResult>();

        foreach (var reagent in accepted.Reagents)
        {
            // Quality is recomputed each step: as integrity falls the material grows less
            // predictable, so the same crafter has less control over step three than step one.
            // Strained essence (§5.3) feeds the same instability — an overloaded vessel is a
            // wilder vessel, which is the whole "attune first, then infuse" lesson.
            var effectiveInstability = IntegrityCalculator.EffectiveInstability(
                materialState.Get(ItemProperties.Instability), integrity,
                EssenceTuning.Strain(essence, materialState.Get(ItemProperties.Resonance)));

            craftQuality = CraftQuality.Normalised(
                process.IsUngated ? 0 : _professionLevel(process.Profession),
                effectiveInstability,
                request.Performance);

            varianceMagnitude = IntegrityCalculator.VarianceMagnitude(effectiveInstability, craftQuality, process.Severity);

            var step = ReactionAlgebra.ApplyReagent(
                materialState, reagent.BaseProperties, process, _content.Properties, integrity,
                PotencyCalculator.QualityMultiplier(craftQuality),
                CatalystFactor(accepted.Catalyst));

            // Essence rides beside the property algebra (§8.4): additive transfer at the
            // process's essence_rate, opposition annihilating the overlap into strain.
            var essenceStep = EssenceAlgebra.Apply(
                essence, _profiles.Resolve(reagent).Essence, process, step.Coefficients, _content.Essences);

            var cost = IntegrityCalculator.Cost(
                step.StateDelta, process.Severity,
                step.StrainReleased + essenceStep.StrainReleased, craftQuality);
            var integrityAfterStep = IntegrityCalculator.Apply(integrity, cost);

            log.Step(new ReactionStepContext(
                process, accepted.Substrate.Name, reagent.Name,
                materialState, reagent.BaseProperties, step, integrity, integrityAfterStep, cost));
            log.Essence(essenceStep);
            steps.Add(step);

            materialState = step.Properties;
            essence = essenceStep.Essence;
            totalCost += cost;
            integrity = integrityAfterStep;

            if (integrity <= 0)
            {
                destroyed = true;
                break;
            }
        }

        // §5.3 — the standing warning on the result, not just a step artifact.
        var finalStrain = EssenceTuning.Strain(essence, materialState.Get(ItemProperties.Resonance));
        if (!destroyed && finalStrain > 0)
            log.EssenceStrain(
                essence.Values.Sum(),
                EssenceTuning.Capacity(materialState.Get(ItemProperties.Resonance)),
                materialState.Get(ItemProperties.Resonance));

        if (applyVariance)
            materialState = VariancePerturbation.Apply(materialState, process, varianceMagnitude, _random);

        var traitPass = ApplyTraitPass(accepted, materialState, integrity, totalCost, destroyed, log);
        materialState = traitPass.MaterialState;
        integrity = traitPass.Integrity;
        totalCost = traitPass.TotalCost;
        destroyed = traitPass.Destroyed;

        // Tags derive from the post-trait state: a trait's consumption can drop a property
        // back through a grants_tags threshold, and the tag should tell the truth.
        var tags = _tags.Derive(accepted.Substrate.Tags, process, materialState);

        var reagentPotencies = accepted.Reagents.Select(r => _profiles.Resolve(r).Potency).ToList();
        var potency = PotencyCalculator.Compute(
            accepted.SubstrateProfile.Potency,
            reagentPotencies,
            accepted.Catalyst is null ? null : _profiles.Resolve(accepted.Catalyst).Potency,
            process.RoleWeights,
            PotencyCalculator.QualityMultiplier(craftQuality),
            craftQuality);

        if (!destroyed)
            log.Potency(accepted.SubstrateProfile.Potency, reagentPotencies, potency);

        var profile = new MaterialProfile(
            materialState, potency, integrity,
            MergeLineage(accepted, process),
            Signature: string.Empty)
        {
            Traits = traitPass.Resolution.Traits,
            Essence = essence,
        };

        var signature = MaterialSignature.Compute(profile, tags);

        return new ReactionRun(
            profile with { Signature = signature }, tags, signature, potency,
            totalCost, varianceMagnitude, destroyed, log, steps);
    }

    /// <summary>
    /// The §10 trait pass, run once the property state has settled (variance included, so a
    /// lucky or unlucky roll can cross a threshold): birth → supersede → cap.
    ///
    /// <para>Births eat properties and charge integrity (§6.2a: <c>traits_created × 4</c>), which
    /// can itself destroy the material — the best traits live in high-variance states, and
    /// reaching for them is a genuine gamble. Skipped entirely when the reagent loop already
    /// destroyed the material.</para>
    /// </summary>
    private TraitPass ApplyTraitPass(
        AcceptedCraft accepted,
        PropertySet materialState,
        int integrity,
        double totalCost,
        bool destroyed,
        ReactionLogBuilder log)
    {
        if (destroyed)
        {
            var unchanged = new TraitResolution(
                accepted.SubstrateProfile.Traits, materialState,
                Array.Empty<TraitInstance>(),
                Array.Empty<(TraitInstance, TraitInstance, TraitInstance)>(),
                Array.Empty<TraitInstance>());
            return new TraitPass(unchanged, materialState, integrity, totalCost, Destroyed: true);
        }

        var resolution = _traitResolver.Apply(materialState, accepted.SubstrateProfile.Traits);
        log.Traits(resolution, _traitResolver.TraitName);

        if (resolution.TraitsCreated == 0)
            return new TraitPass(resolution, resolution.Properties, integrity, totalCost, Destroyed: false);

        var traitCost = resolution.TraitsCreated * RefinementTuning.TraitCost;
        var integrityAfterTraits = IntegrityCalculator.Apply(integrity, traitCost);

        return new TraitPass(
            resolution, resolution.Properties, integrityAfterTraits, totalCost + traitCost,
            Destroyed: integrityAfterTraits <= 0);
    }

    /// <summary>What the trait pass produced, plus the running totals it changed.</summary>
    private sealed record TraitPass(
        TraitResolution Resolution,
        PropertySet MaterialState,
        int Integrity,
        double TotalCost,
        bool Destroyed);

    /// <summary>
    /// §14 — roots merge by weight, renormalize, and anything under the trace threshold is
    /// dropped. Parent links stay one level deep; the full tree is walked through the registry
    /// rather than embedded, which is the whole answer to "lineage without becoming enormous".
    /// </summary>
    private Lineage MergeLineage(AcceptedCraft accepted, ProcessDefinition process)
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);

        void Contribute(Lineage lineage, double share)
        {
            foreach (var root in lineage.Roots)
                weights[root.RootId] = weights.GetValueOrDefault(root.RootId) + root.Weight * share;
        }

        Contribute(accepted.SubstrateProfile.Lineage, process.RoleWeights.Substrate);

        var perReagent = accepted.Reagents.Count == 0
            ? 0.0
            : process.RoleWeights.Reagent / accepted.Reagents.Count;
        foreach (var reagent in accepted.Reagents)
            Contribute(_profiles.Resolve(reagent).Lineage, perReagent);

        var total = weights.Values.Sum();
        var roots = weights
            .Select(pair => new RootShare(pair.Key, total <= 0 ? 0 : pair.Value / total))
            .Where(r => r.Weight >= Lineage.TraceWeight)
            .OrderByDescending(r => r.Weight)
            .ThenBy(r => r.RootId, StringComparer.Ordinal)
            .Take(Lineage.MaxRoots)
            .ToList();

        // Renormalized again after pruning, so the shares a name or valuation reads always
        // sum to one regardless of how much trace was dropped.
        var kept = roots.Sum(r => r.Weight);
        if (kept > 0)
            roots = roots.Select(r => r with { Weight = r.Weight / kept }).ToList();

        var parents = new List<string> { accepted.SubstrateProfile.Signature };
        parents.AddRange(accepted.Reagents.Select(r => _profiles.Resolve(r).Signature));

        return new Lineage(
            roots,
            accepted.SubstrateProfile.Generation + 1,
            process.Id,
            parents.Distinct(StringComparer.Ordinal).Take(4).ToList());
    }

    /// <summary>A catalyst modifies rates and transfers nothing of its own (§7.1). Its affinity
    /// is what makes it a good one, so that is what it lends.</summary>
    private double CatalystFactor(MaterialDefinition? catalyst) =>
        catalyst is null
            ? ReactionTuning.NoCatalyst
            : ReactionTuning.NoCatalyst
              + (catalyst.GetProperty(ItemProperties.Affinity) / 100.0 * ReactionTuning.CatalystAffinityBonus);

    private bool NameIsTaken(string name) =>
        _registry.All.Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

    private string MaterialName(string id) =>
        _content.Materials.TryGetById(id, out var material) ? material.Name : id;

    private sealed record AcceptedCraft(
        CraftFailure Failure,
        CraftRequest Request,
        ProcessDefinition Process,
        MaterialDefinition Substrate,
        IReadOnlyList<MaterialDefinition> Reagents,
        MaterialDefinition? Catalyst,
        MaterialProfile SubstrateProfile)
    {
        public static AcceptedCraft Rejected(CraftFailure failure) =>
            new(failure, null!, null!, null!, Array.Empty<MaterialDefinition>(), null, null!);
    }

    private sealed record ReactionRun(
        MaterialProfile Profile,
        IReadOnlyList<string> Tags,
        string Signature,
        int Potency,
        double TotalCost,
        double VarianceMagnitude,
        bool Destroyed,
        ReactionLogBuilder LogBuilder,
        IReadOnlyList<ReactionStepResult> Steps)
    {
        public ReactionLog Log => LogBuilder.Build();
    }
}
