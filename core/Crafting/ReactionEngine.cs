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
        _professionLevel = professionLevel ?? throw new ArgumentNullException(nameof(professionLevel));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public CraftProjection Project(CraftRequest request)
    {
        var gate = Gate(request);
        if (gate.Failure != CraftFailure.None)
            return CraftProjection.Failed(gate.Failure);

        // Variance off: the projection shows the outcome the player is aiming at, and the
        // spread is reported separately as a destruction chance rather than baked into it.
        var run = Run(gate, applyVariance: false);

        return new CraftProjection(
            CraftFailure.None,
            IntegrityCalculator.Project(gate.SubstrateProfile.Integrity, run.TotalCost, run.VarianceMagnitude),
            run.Potency,
            _names.Generate(run.Profile, run.Tags, NameIsTaken),
            WouldBeFirstDiscovery: !_registry.Contains(run.Signature),
            Preview: run.Log);
    }

    public CraftOutcome Resolve(CraftRequest request)
    {
        var gate = Gate(request);
        if (gate.Failure != CraftFailure.None)
            return CraftOutcome.Failed(gate.Failure);

        var run = Run(gate, applyVariance: true);
        var inventory = _inventory();

        // Reagents are consumed entirely; the catalyst is not (§8.6).
        inventory.TryRemove(request.SubstrateId, request.Quantity);
        foreach (var reagent in request.ReagentIds)
            inventory.TryRemove(reagent, request.Quantity);

        var log = run.LogBuilder;

        if (run.Destroyed)
        {
            // §6.2c: destruction is terminal, but never total loss.
            var byproduct = _byproducts.Resolve(run.Tags, request.Quantity);
            var byproducts = new List<ItemStack>();

            if (byproduct is not null)
            {
                inventory.Add(byproduct.Value);
                byproducts.Add(byproduct.Value);
            }

            log.Destroyed(gate.Substrate.Name, byproduct is null ? null : MaterialName(byproduct.Value.ItemId), request.Quantity);

            return new CraftOutcome(
                CraftFailure.None, null, gate.Substrate.Name, 0,
                IsFirstDiscovery: false, WasDestroyed: true, byproducts, log.Build());
        }

        var name = _names.Generate(run.Profile, run.Tags, NameIsTaken);
        var lookup = _registry.GetOrRegister(run.Signature, () => new MaterialDefinition
        {
            Id = run.Signature,
            Name = name,
            Tags = run.Tags,
            Properties = new Dictionary<string, double>(run.Profile.Properties.AsDictionary()),
            Profile = run.Profile,
        });

        inventory.Add(lookup.Definition.Id, request.Quantity);
        log.Result(lookup.Definition.Name, request.Quantity, lookup.IsFirstDiscovery);

        return new CraftOutcome(
            CraftFailure.None,
            lookup.Definition.Id,
            lookup.Definition.Name,
            request.Quantity,
            lookup.IsFirstDiscovery,
            WasDestroyed: false,
            Array.Empty<ItemStack>(),
            log.Build());
    }

    // ---- §8.7 step 1: the gate -----------------------------------------------------------

    private GateResult Gate(CraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Quantity <= 0)
            return GateResult.Rejected(CraftFailure.InvalidQuantity);

        if (!_content.Processes.TryGetById(request.ProcessId, out var process))
            return GateResult.Rejected(CraftFailure.UnknownProcess);

        if (request.ReagentIds.Count == 0)
            return GateResult.Rejected(CraftFailure.NoReagents);

        if (!_content.Materials.TryGetById(request.SubstrateId, out var substrate))
            return GateResult.Rejected(CraftFailure.UnknownSubstrate);

        var reagents = new List<MaterialDefinition>();
        foreach (var id in request.ReagentIds)
        {
            if (!_content.Materials.TryGetById(id, out var reagent))
                return GateResult.Rejected(CraftFailure.UnknownReagent);
            reagents.Add(reagent);
        }

        MaterialDefinition? catalyst = null;
        if (request.CatalystId is { } catalystId)
        {
            if (!_content.Materials.TryGetById(catalystId, out var found))
                return GateResult.Rejected(CraftFailure.UnknownCatalyst);
            catalyst = found;
        }

        if (!process.IsUngated && _professionLevel(process.Profession) < process.Requires.ProfessionLevel)
            return GateResult.Rejected(CraftFailure.ProfessionTooLow);

        foreach (var required in process.Requires.SubstrateTags)
        {
            if (!substrate.Tags.Contains(required, StringComparer.OrdinalIgnoreCase))
                return GateResult.Rejected(CraftFailure.SubstrateRejected);
        }

        if (!HasInputs(request, catalyst))
            return GateResult.Rejected(CraftFailure.MissingInputs);

        return new GateResult(
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

    private RunResult Run(GateResult gate, bool applyVariance)
    {
        var request = gate.Request;
        var process = gate.Process;
        var log = new ReactionLogBuilder(_content.Properties);

        var state = gate.SubstrateProfile.Properties;
        var integrity = gate.SubstrateProfile.Integrity;
        var totalCost = 0.0;
        var destroyed = false;
        var qualityNorm = 0.0;
        var variance = 0.0;

        foreach (var reagent in gate.Reagents)
        {
            // Quality is recomputed each step: as integrity falls the material grows less
            // predictable, so the same crafter has less control over step three than step one.
            var effectiveInstability = IntegrityCalculator.EffectiveInstability(
                state.Get(ItemProperties.Instability), integrity);

            qualityNorm = CraftQuality.Norm(
                process.IsUngated ? 0 : _professionLevel(process.Profession),
                effectiveInstability,
                request.Performance);

            variance = IntegrityCalculator.VarianceMagnitude(effectiveInstability, qualityNorm, process.Severity);

            var step = ReactionAlgebra.ApplyReagent(
                state, reagent.BaseProperties, process, _content.Properties, integrity,
                PotencyCalculator.QualityMultiplier(qualityNorm),
                CatalystFactor(gate.Catalyst));

            var cost = IntegrityCalculator.Cost(step.StateDelta, process.Severity, step.StrainReleased, qualityNorm);
            var after = IntegrityCalculator.Apply(integrity, cost);

            log.Step(new ReactionStepContext(
                process, gate.Substrate.Name, reagent.Name,
                state, reagent.BaseProperties, step, integrity, after, cost));

            state = step.Properties;
            totalCost += cost;
            integrity = after;

            if (integrity <= 0)
            {
                destroyed = true;
                break;
            }
        }

        if (applyVariance)
            state = VariancePerturbation.Apply(state, process, variance, _random);

        var tags = _tags.Derive(gate.Substrate.Tags, process, state);

        var reagentPotencies = gate.Reagents.Select(r => _profiles.Resolve(r).Potency).ToList();
        var potency = PotencyCalculator.Compute(
            gate.SubstrateProfile.Potency,
            reagentPotencies,
            gate.Catalyst is null ? null : _profiles.Resolve(gate.Catalyst).Potency,
            process.RoleWeights,
            PotencyCalculator.QualityMultiplier(qualityNorm),
            qualityNorm);

        if (!destroyed)
            log.Potency(gate.SubstrateProfile.Potency, reagentPotencies, potency);

        var profile = new MaterialProfile(
            state, potency, integrity,
            MergeLineage(gate, process),
            Signature: string.Empty);

        var signature = MaterialSignature.Compute(profile, tags);

        return new RunResult(
            profile with { Signature = signature }, tags, signature, potency,
            totalCost, variance, destroyed, log);
    }

    /// <summary>
    /// §14 — roots merge by weight, renormalize, and anything under the trace threshold is
    /// dropped. Parent links stay one level deep; the full tree is walked through the registry
    /// rather than embedded, which is the whole answer to "lineage without becoming enormous".
    /// </summary>
    private Lineage MergeLineage(GateResult gate, ProcessDefinition process)
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);

        void Contribute(Lineage lineage, double share)
        {
            foreach (var root in lineage.Roots)
                weights[root.RootId] = weights.GetValueOrDefault(root.RootId) + root.Weight * share;
        }

        Contribute(gate.SubstrateProfile.Lineage, process.RoleWeights.Substrate);

        var perReagent = gate.Reagents.Count == 0
            ? 0.0
            : process.RoleWeights.Reagent / gate.Reagents.Count;
        foreach (var reagent in gate.Reagents)
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

        var parents = new List<string> { gate.SubstrateProfile.Signature };
        parents.AddRange(gate.Reagents.Select(r => _profiles.Resolve(r).Signature));

        return new Lineage(
            roots,
            gate.SubstrateProfile.Generation + 1,
            process.Id,
            parents.Distinct(StringComparer.Ordinal).Take(4).ToList());
    }

    /// <summary>A catalyst modifies rates and transfers nothing of its own (§7.1). Its affinity
    /// is what makes it a good one, so that is what it lends.</summary>
    private double CatalystFactor(MaterialDefinition? catalyst) =>
        catalyst is null
            ? ReactionTuning.NoCatalyst
            : ReactionTuning.NoCatalyst + catalyst.GetProperty(ItemProperties.Affinity) / 100.0 * 0.25;

    private bool NameIsTaken(string name) =>
        _registry.All.Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

    private string MaterialName(string id) =>
        _content.Materials.TryGetById(id, out var material) ? material.Name : id;

    private sealed record GateResult(
        CraftFailure Failure,
        CraftRequest Request,
        ProcessDefinition Process,
        MaterialDefinition Substrate,
        IReadOnlyList<MaterialDefinition> Reagents,
        MaterialDefinition? Catalyst,
        MaterialProfile SubstrateProfile)
    {
        public static GateResult Rejected(CraftFailure failure) =>
            new(failure, null!, null!, null!, Array.Empty<MaterialDefinition>(), null, null!);
    }

    private sealed record RunResult(
        MaterialProfile Profile,
        IReadOnlyList<string> Tags,
        string Signature,
        int Potency,
        double TotalCost,
        double VarianceMagnitude,
        bool Destroyed,
        ReactionLogBuilder LogBuilder)
    {
        public ReactionLog Log => LogBuilder.Build();
    }
}
