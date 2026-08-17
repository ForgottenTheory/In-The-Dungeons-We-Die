using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Randomness;

namespace Dungeons.Crafting;

/// <summary>Turns one material into another, and shows the cost before the player commits (§18).</summary>
public interface IMaterialTransformationEngine
{
    /// <summary>What the craft would do, without doing it (§6.2c).</summary>
    CraftPreview PreviewCraft(CraftRequest request);

    /// <summary>Runs the craft: consumes the inputs, registers the result, deposits it.</summary>
    CraftOutcome RunCraft(CraftRequest request);
}

/// <summary>
/// Takes a <see cref="MaterialState"/> and a <see cref="CraftingActionDefinition"/> and produces
/// a new material — the universal pipeline (docs/emergent-item-system.md §8.7) that replaces
/// recipe matching entirely.
///
/// <para>There are <b>no recipes and no per-combination rules</b>. Every combination of
/// substrate, reagents and crafting action produces a result, computed the same way every time:
/// accept the request → for each reagent, apply <see cref="MaterialTransformationRules"/> →
/// spend workability → apply variance → birth traits → derive tags → compute material strength →
/// quantize into a signature → register → name it.</para>
///
/// <para>Deterministic apart from the variance perturbation (§12.3), which draws from the
/// injected <see cref="IRandomSource"/> — so <see cref="PreviewCraft"/> runs the identical
/// pipeline with variance switched off and shows the player exactly what to expect.</para>
/// </summary>
public sealed class MaterialTransformationEngine : IMaterialTransformationEngine
{
    private readonly ContentBundle _content;
    private readonly Func<Inventory> _inventory;
    private readonly MaterialStateResolver _materialStates;
    private readonly IEmergentRegistry _registry;
    private readonly NameGenerator _names;
    private readonly TagDeriver _tags;
    private readonly ByproductResolver _byproducts;
    private readonly TraitResolver _traitResolver;
    private readonly Func<string, int> _professionLevel;
    private readonly IRandomSource _random;

    public MaterialTransformationEngine(
        ContentBundle content,
        Func<Inventory> inventory,
        MaterialStateResolver materialStates,
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
        _materialStates = materialStates ?? throw new ArgumentNullException(nameof(materialStates));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _names = names ?? throw new ArgumentNullException(nameof(names));
        _tags = tags ?? throw new ArgumentNullException(nameof(tags));
        _byproducts = byproducts ?? throw new ArgumentNullException(nameof(byproducts));
        _traitResolver = traits ?? throw new ArgumentNullException(nameof(traits));
        _professionLevel = professionLevel ?? throw new ArgumentNullException(nameof(professionLevel));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public CraftPreview PreviewCraft(CraftRequest request)
    {
        var accepted = AcceptRequest(request);
        if (accepted.Failure != CraftFailure.None)
            return CraftPreview.Failed(accepted.Failure);

        // Variance off: the projection shows the outcome the player is aiming at, and the
        // spread is reported separately as a destruction chance rather than baked into it.
        var reaction = RunReaction(accepted, applyVariance: false);

        return new CraftPreview(
            CraftFailure.None,
            WorkabilityCalculator.ProjectRemaining(
                accepted.SubstrateState.Workability, reaction.TotalCost, reaction.VarianceMagnitude),
            reaction.MaterialStrength,
            _names.Generate(reaction.State, reaction.Tags, NameIsTaken),
            WouldBeFirstDiscovery: !_registry.Contains(reaction.Signature),
            Preview: reaction.Log,
            Projected: reaction.State,
            Steps: reaction.Steps);
    }

    public CraftOutcome RunCraft(CraftRequest request)
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
            var byproduct = _byproducts.ByproductFor(reaction.Tags, request.Quantity);
            var byproducts = new List<ItemStack>();

            if (byproduct is not null)
            {
                inventory.Add(byproduct.Value);
                byproducts.Add(byproduct.Value);
            }

            log.Destroyed(
                accepted.Substrate.Name,
                byproduct is null ? null : MaterialName(byproduct.Value.ItemId),
                request.Quantity);

            return new CraftOutcome(
                CraftFailure.None, null, accepted.Substrate.Name, 0,
                IsFirstDiscovery: false, WasDestroyed: true, byproducts, log.Build());
        }

        var name = _names.Generate(reaction.State, reaction.Tags, NameIsTaken);
        var registration = _registry.GetOrRegister(reaction.Signature, () => new MaterialDefinition
        {
            Id = reaction.Signature,
            Name = name,
            Tags = reaction.Tags,
            Properties = new Dictionary<string, double>(reaction.State.Properties.AsDictionary()),
            Essence = new Dictionary<string, double>(reaction.State.Essence),
            State = reaction.State,
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

        if (!_content.CraftingActions.TryGetById(request.CraftingActionId, out var craftingAction))
            return AcceptedCraft.Rejected(CraftFailure.UnknownCraftingAction);

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

        if (!craftingAction.IsUngated && _professionLevel(craftingAction.Profession) < craftingAction.Requires.ProfessionLevel)
            return AcceptedCraft.Rejected(CraftFailure.ProfessionTooLow);

        foreach (var required in craftingAction.Requires.SubstrateTags)
        {
            if (!substrate.Tags.Contains(required, StringComparer.OrdinalIgnoreCase))
                return AcceptedCraft.Rejected(CraftFailure.SubstrateRejected);
        }

        if (!HasInputs(request, catalyst))
            return AcceptedCraft.Rejected(CraftFailure.MissingInputs);

        return new AcceptedCraft(
            CraftFailure.None, request, craftingAction, substrate, reagents, catalyst,
            _materialStates.StateOf(substrate));
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
        var craftingAction = accepted.Process;
        var log = new ReactionLogBuilder(_content.Properties);

        var materialState = accepted.SubstrateState.Properties;
        var essence = (IReadOnlyDictionary<string, double>)new Dictionary<string, double>(
            accepted.SubstrateState.Essence, StringComparer.OrdinalIgnoreCase);
        var workability = accepted.SubstrateState.Workability;
        var totalCost = 0.0;
        var destroyed = false;
        var craftQuality = 0.0;
        var varianceMagnitude = 0.0;
        var steps = new List<TransformationStepResult>();

        foreach (var reagent in accepted.Reagents)
        {
            // Quality is recomputed each step: as workability falls the material grows less
            // predictable, so the same crafter has less control over step three than step one.
            // Strained essence (§5.3) feeds the same instability — an overloaded vessel is a
            // wilder vessel, which is the whole "attune first, then infuse" lesson.
            var effectiveInstability = WorkabilityCalculator.EffectiveInstability(
                materialState.Get(ItemProperties.Instability), workability,
                EssenceTuning.Stress(essence, materialState.Get(ItemProperties.Resonance)));

            craftQuality = CraftQuality.Normalised(
                craftingAction.IsUngated ? 0 : _professionLevel(craftingAction.Profession),
                effectiveInstability,
                request.Performance);

            varianceMagnitude = WorkabilityCalculator.VarianceMagnitude(
                effectiveInstability, craftQuality, craftingAction.Severity);

            var step = MaterialTransformationRules.ApplyReagent(
                materialState, reagent.BaseProperties, craftingAction, _content.Properties, workability,
                MaterialStrengthCalculator.QualityMultiplier(craftQuality),
                CatalystFactor(accepted.Catalyst));

            // Essence rides beside the property algebra (§8.4): additive transfer at the
            // crafting action's essence_rate, opposition annihilating the overlap into strain.
            var essenceStep = EssenceAlgebra.Apply(
                essence, _materialStates.StateOf(reagent).Essence, craftingAction, step.Coefficients, _content.Essences);

            var cost = WorkabilityCalculator.Cost(
                step.StateDelta, craftingAction.Severity,
                step.StressReleased + essenceStep.StressReleased, craftQuality);
            var workabilityAfterStep = WorkabilityCalculator.Apply(workability, cost);

            log.Step(new ReactionStepContext(
                craftingAction, accepted.Substrate.Name, reagent.Name,
                materialState, reagent.BaseProperties, step, workability, workabilityAfterStep, cost));
            log.Essence(essenceStep);
            steps.Add(step);

            materialState = step.Properties;
            essence = essenceStep.Essence;
            totalCost += cost;
            workability = workabilityAfterStep;

            if (workability <= 0)
            {
                destroyed = true;
                break;
            }
        }

        // §5.3 — the standing warning on the result, not just a step artifact.
        var finalStress = EssenceTuning.Stress(essence, materialState.Get(ItemProperties.Resonance));
        if (!destroyed && finalStress > 0)
            log.EssenceStress(
                essence.Values.Sum(),
                EssenceTuning.Capacity(materialState.Get(ItemProperties.Resonance)),
                materialState.Get(ItemProperties.Resonance));

        if (applyVariance)
            materialState = VariancePerturbation.Apply(materialState, craftingAction, varianceMagnitude, _random);

        var traitPass = ApplyTraitPass(accepted, materialState, workability, totalCost, destroyed, log);
        materialState = traitPass.MaterialState;
        workability = traitPass.Workability;
        totalCost = traitPass.TotalCost;
        destroyed = traitPass.Destroyed;

        // Tags derive from the post-trait state: a trait's consumption can drop a property
        // back through a grants_tags threshold, and the tag should tell the truth.
        var tags = _tags.Derive(accepted.Substrate.Tags, craftingAction, materialState);

        var reagentPotencies = accepted.Reagents.Select(r => _materialStates.StateOf(r).MaterialStrength).ToList();
        var materialStrength = MaterialStrengthCalculator.Compute(
            accepted.SubstrateState.MaterialStrength,
            reagentPotencies,
            accepted.Catalyst is null ? null : _materialStates.StateOf(accepted.Catalyst).MaterialStrength,
            craftingAction.RoleWeights,
            MaterialStrengthCalculator.QualityMultiplier(craftQuality),
            craftQuality);

        if (!destroyed)
            log.MaterialStrength(accepted.SubstrateState.MaterialStrength, reagentPotencies, materialStrength);

        var profile = new MaterialState(
            materialState, materialStrength, workability,
            MergeLineage(accepted, craftingAction),
            Signature: string.Empty)
        {
            Traits = traitPass.Resolution.Traits,
            Essence = essence,
        };

        var signature = MaterialSignature.Compute(profile, tags);

        return new ReactionRun(
            profile with { Signature = signature }, tags, signature, materialStrength,
            totalCost, varianceMagnitude, destroyed, log, steps);
    }

    /// <summary>
    /// The §10 trait pass, run once the property state has settled (variance included, so a
    /// lucky or unlucky roll can cross a threshold): birth → supersede → cap.
    ///
    /// <para>Births eat properties and charge workability (§6.2a: <c>traits_created × 4</c>), which
    /// can itself destroy the material — the best traits live in high-variance states, and
    /// reaching for them is a genuine gamble. Skipped entirely when the reagent loop already
    /// destroyed the material.</para>
    /// </summary>
    private TraitPass ApplyTraitPass(
        AcceptedCraft accepted,
        PropertySet materialState,
        int workability,
        double totalCost,
        bool destroyed,
        ReactionLogBuilder log)
    {
        if (destroyed)
        {
            var unchanged = new TraitResolution(
                accepted.SubstrateState.Traits, materialState,
                Array.Empty<TraitInstance>(),
                Array.Empty<(TraitInstance, TraitInstance, TraitInstance)>(),
                Array.Empty<TraitInstance>());
            return new TraitPass(unchanged, materialState, workability, totalCost, Destroyed: true);
        }

        var resolution = _traitResolver.Apply(materialState, accepted.SubstrateState.Traits);
        log.Traits(resolution, _traitResolver.TraitName);

        if (resolution.TraitsCreated == 0)
            return new TraitPass(resolution, resolution.Properties, workability, totalCost, Destroyed: false);

        var traitCost = resolution.TraitsCreated * RefinementTuning.TraitCost;
        var workabilityAfterTraits = WorkabilityCalculator.Apply(workability, traitCost);

        return new TraitPass(
            resolution, resolution.Properties, workabilityAfterTraits, totalCost + traitCost,
            Destroyed: workabilityAfterTraits <= 0);
    }

    /// <summary>What the trait pass produced, plus the running totals it changed.</summary>
    private sealed record TraitPass(
        TraitResolution Resolution,
        PropertySet MaterialState,
        int Workability,
        double TotalCost,
        bool Destroyed);

    /// <summary>
    /// §14 — roots merge by weight, renormalize, and anything under the trace threshold is
    /// dropped. Parent links stay one level deep; the full tree is walked through the registry
    /// rather than embedded, which is the whole answer to "lineage without becoming enormous".
    /// </summary>
    private Lineage MergeLineage(AcceptedCraft accepted, CraftingActionDefinition craftingAction)
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);

        void Contribute(Lineage lineage, double share)
        {
            foreach (var root in lineage.Roots)
                weights[root.RootId] = weights.GetValueOrDefault(root.RootId) + root.Weight * share;
        }

        Contribute(accepted.SubstrateState.Lineage, craftingAction.RoleWeights.Substrate);

        var perReagent = accepted.Reagents.Count == 0
            ? 0.0
            : craftingAction.RoleWeights.Reagent / accepted.Reagents.Count;
        foreach (var reagent in accepted.Reagents)
            Contribute(_materialStates.StateOf(reagent).Lineage, perReagent);

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

        var parents = new List<string> { accepted.SubstrateState.Signature };
        parents.AddRange(accepted.Reagents.Select(r => _materialStates.StateOf(r).Signature));

        return new Lineage(
            roots,
            accepted.SubstrateState.Generation + 1,
            craftingAction.Id,
            parents.Distinct(StringComparer.Ordinal).Take(4).ToList());
    }

    /// <summary>A catalyst modifies rates and transfers nothing of its own (§7.1). Its affinity
    /// is what makes it a good one, so that is what it lends.</summary>
    private double CatalystFactor(MaterialDefinition? catalyst) =>
        catalyst is null
            ? MaterialTransformationTuning.NoCatalyst
            : MaterialTransformationTuning.NoCatalyst
              + (catalyst.GetProperty(ItemProperties.Affinity) / 100.0 * MaterialTransformationTuning.CatalystAffinityBonus);

    private bool NameIsTaken(string name) =>
        _registry.All.Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

    private string MaterialName(string id) =>
        _content.Materials.TryGetById(id, out var material) ? material.Name : id;

    private sealed record AcceptedCraft(
        CraftFailure Failure,
        CraftRequest Request,
        CraftingActionDefinition Process,
        MaterialDefinition Substrate,
        IReadOnlyList<MaterialDefinition> Reagents,
        MaterialDefinition? Catalyst,
        MaterialState SubstrateState)
    {
        public static AcceptedCraft Rejected(CraftFailure failure) =>
            new(failure, null!, null!, null!, Array.Empty<MaterialDefinition>(), null, null!);
    }

    private sealed record ReactionRun(
        MaterialState State,
        IReadOnlyList<string> Tags,
        string Signature,
        int MaterialStrength,
        double TotalCost,
        double VarianceMagnitude,
        bool Destroyed,
        ReactionLogBuilder LogBuilder,
        IReadOnlyList<TransformationStepResult> Steps)
    {
        public ReactionLog Log => LogBuilder.Build();
    }
}
