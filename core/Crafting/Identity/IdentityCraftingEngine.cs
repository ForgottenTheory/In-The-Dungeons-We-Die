using Dungeons.Content;
using Dungeons.Items;
using Dungeons.Randomness;

namespace Dungeons.Crafting.Identity;

/// <summary>
/// The ten transformation verbs, resolved (docs/transformation-verbs.md, D47) — the identity
/// model's counterpart to <see cref="MaterialTransformationEngine"/>, landing beside it
/// during the migration and wired to the bench when the surfaces swap.
///
/// <para><b>Preview parity, kept:</b> <see cref="Preview"/> and <see cref="Commit"/> run the
/// same resolution; the only difference is whether the risk dice are rolled. Refusals are
/// deterministic and always precede risk, so the preview cannot lie.</para>
///
/// <para><b>Where the dice live</b> (§4): fracture when working an overfilled material
/// (the newest identity breaks away), destruction when condition-stepping work is attempted
/// at Fragile (byproducts are always paid). Nothing else rolls.</para>
///
/// <para>The engine works on <see cref="IdentityMaterialState"/>s and never touches an
/// inventory — consumption and deposit are the application layer's job. Everything in
/// <see cref="VerbRequest.Sources"/> is spent on commit, plus the substrate when the verb
/// consumes it (Extract, Fuse, Process, and any destruction).</para>
/// </summary>
public sealed class IdentityCraftingEngine
{
    private readonly ContentBundle _content;
    private readonly IRandomSource _random;
    private readonly ByproductResolver _byproducts;

    public IdentityCraftingEngine(ContentBundle content, IRandomSource random)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _byproducts = new ByproductResolver(content.Byproducts);
    }

    public VerbProjection Preview(VerbRequest request)
    {
        var resolved = Resolve(request);
        return new VerbProjection(resolved.Failure, resolved.Result, resolved.Produced, resolved.Risks, resolved.Steps);
    }

    public VerbOutcome Commit(VerbRequest request)
    {
        var resolved = Resolve(request);
        if (resolved.Failure is { } failure)
            return new VerbOutcome(VerbResultKind.Refused, failure, null,
                Array.Empty<IdentityMaterialState>(), null, null, VerbRisks.None, resolved.Steps);

        if (resolved.Risks.DestructionChance > 0
            && _random.NextDouble() < resolved.Risks.DestructionChance)
        {
            var byproduct = ByproductOf(request.Substrate);
            var steps = new List<VerbStep>(resolved.Steps)
            {
                new(VerbStepKind.RiskNoted, "The Fragile gamble lands: the material is destroyed. Byproducts are paid."),
            };
            return new VerbOutcome(VerbResultKind.Destroyed, null, null,
                Array.Empty<IdentityMaterialState>(), byproduct, null, resolved.Risks, steps);
        }

        if (resolved.Risks.FractureChance > 0
            && _random.NextDouble() < resolved.Risks.FractureChance)
        {
            var newest = request.Substrate.Identities[^1];
            var fractured = request.Substrate with
            {
                Identities = request.Substrate.Identities.Take(request.Substrate.Identities.Count - 1).ToArray(),
                Condition = StepDown(request.Substrate.Condition),
            };
            var steps = new List<VerbStep>(resolved.Steps)
            {
                new(VerbStepKind.RiskNoted, $"The overfilled material fractures: {newest.Id} breaks away and the verb's work is lost."),
            };
            return new VerbOutcome(VerbResultKind.Fractured, null, fractured,
                Array.Empty<IdentityMaterialState>(), null, newest.Id, resolved.Risks, steps);
        }

        return new VerbOutcome(VerbResultKind.Succeeded, null, resolved.Result,
            resolved.Produced, null, null, resolved.Risks, resolved.Steps);
    }

    // --- The shared resolution both entry points run ------------------------------------

    private sealed record ResolvedVerb(
        VerbFailureReason? Failure,
        IdentityMaterialState? Result,
        IReadOnlyList<IdentityMaterialState> Produced,
        VerbRisks Risks,
        IReadOnlyList<VerbStep> Steps)
    {
        public static ResolvedVerb Refused(VerbFailureReason reason) =>
            new(reason, null, Array.Empty<IdentityMaterialState>(), VerbRisks.None, Array.Empty<VerbStep>());
    }

    private ResolvedVerb Resolve(VerbRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Substrate);

        return request.Verb switch
        {
            CraftVerb.Process => ResolveProcess(request),
            CraftVerb.Fuse => ResolveFuse(request),
            CraftVerb.Reveal => ResolveReveal(request),
            CraftVerb.Transfer => ResolveTransfer(request),
            CraftVerb.Develop => ResolveDevelop(request),
            CraftVerb.Extract => ResolveExtract(request),
            CraftVerb.Displace => ResolveDisplace(request),
            CraftVerb.Refine => ResolveRefine(request),
            CraftVerb.Restore => ResolveRestore(request),
            CraftVerb.Expand => ResolveExpand(request),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Verb, "Unknown verb."),
        };
    }

    /// <summary>Fracture keys on the substrate's stability <em>before</em> the verb — the
    /// overfilling transfer itself is safe; it is <em>further</em> work that gambles (§10.3).
    /// Destruction keys on Fragile + a condition-stepping verb: three safe identity-changing
    /// actions, then every further one gambles (§10.4).</summary>
    private static VerbRisks RisksFor(IdentityMaterialState substrate, bool stepsCondition)
    {
        var fracture = substrate.Stability switch
        {
            Stability.Unstable => IdentityCraftTuning.FractureChanceUnstable,
            Stability.Volatile => IdentityCraftTuning.FractureChanceVolatile,
            _ => 0.0,
        };
        var destruction = stepsCondition && substrate.Condition == Condition.Fragile
            ? IdentityCraftTuning.DestructionChanceWhenFragile
            : 0.0;
        return new VerbRisks(stepsCondition ? fracture : 0.0, destruction);
    }

    private static Condition StepDown(Condition condition) =>
        condition == Condition.Fragile ? Condition.Fragile : condition + 1;

    private static void NoteRisks(List<VerbStep> steps, VerbRisks risks)
    {
        if (risks.DestructionChance > 0)
            steps.Add(new VerbStep(VerbStepKind.RiskNoted,
                $"Fragile: {risks.DestructionChance:P0} chance this work destroys the material (byproducts paid)."));
        if (risks.FractureChance > 0)
            steps.Add(new VerbStep(VerbStepKind.RiskNoted,
                $"Overfilled: {risks.FractureChance:P0} chance the newest identity fractures away."));
    }

    private static void NoteConditionStep(List<VerbStep> steps, Condition before, Condition after)
    {
        if (before != after)
            steps.Add(new VerbStep(VerbStepKind.ConditionStepped, $"Condition {before} → {after}."));
    }

    private ItemStack? ByproductOf(IdentityMaterialState substrate)
    {
        var primaryRoot = substrate.Roots.OrderByDescending(root => root.Weight).FirstOrDefault();
        var tags = primaryRoot is not null
            && _content.Materials.TryGetById(primaryRoot.DefinitionId, out var definition)
                ? definition.Tags
                : Array.Empty<string>();
        return _byproducts.ByproductFor(tags);
    }

    // --- The ten verbs -------------------------------------------------------------------

    private ResolvedVerb ResolveProcess(VerbRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OutputDefinitionId))
            return ResolvedVerb.Refused(VerbFailureReason.MissingOutputDefinition);
        if (!_content.Materials.TryGetById(request.OutputDefinitionId, out var output))
            return ResolvedVerb.Refused(VerbFailureReason.OutputDefinitionUnknown);
        if (output.Capacity is not int outputCapacity)
            return ResolvedVerb.Refused(VerbFailureReason.OutputDefinitionNotMigrated);

        var substrate = request.Substrate;

        // Identities and latents carry through processing untouched; the output definition's
        // own innate grants join them (higher rank wins on a shared identity).
        var identities = substrate.Identities.ToList();
        foreach (var grant in output.Identities)
        {
            var existing = identities.FindIndex(stake => stake.Id == grant.Id);
            if (existing < 0)
                identities.Add(new IdentityStake(grant.Id, grant.Rank));
            else if (grant.Rank > identities[existing].Rank)
                identities[existing] = identities[existing] with { Rank = grant.Rank };
        }
        var latent = substrate.Latent
            .Concat(output.Latent)
            .Distinct(StringComparer.Ordinal)
            .Where(id => identities.All(stake => stake.Id != id))
            .ToArray();

        var result = substrate with
        {
            Identities = identities,
            Latent = latent,
            Capacity = outputCapacity,
            IsCarrier = false,
            Roots = new[] { new ProvenanceRoot(output.Id, 1.0) },
        };

        var steps = new List<VerbStep>
        {
            new(VerbStepKind.SubstanceChanged, $"Processed into {output.Name}; identities carry through."),
        };
        return new ResolvedVerb(null, result, Array.Empty<IdentityMaterialState>(), VerbRisks.None, steps);
    }

    private ResolvedVerb ResolveFuse(VerbRequest request)
    {
        if (request.Sources.Count == 0)
            return ResolvedVerb.Refused(VerbFailureReason.NoSources);

        var inputs = new List<IdentityMaterialState> { request.Substrate };
        inputs.AddRange(request.Sources);

        var identities = new List<IdentityStake>();
        foreach (var input in inputs)
        {
            foreach (var stake in input.Identities)
            {
                var existing = identities.FindIndex(s => s.Id == stake.Id);
                if (existing < 0)
                    identities.Add(stake);
                else if (stake.Rank > identities[existing].Rank)
                    identities[existing] = identities[existing] with { Rank = stake.Rank };
            }
        }

        var capacity = inputs.Max(input => input.Capacity);
        if (identities.Count > capacity + IdentityCraftTuning.OverfillHardLimit)
            return ResolvedVerb.Refused(VerbFailureReason.OverfillLimit);

        var latent = inputs.SelectMany(input => input.Latent)
            .Distinct(StringComparer.Ordinal)
            .Where(id => identities.All(stake => stake.Id != id))
            .ToArray();

        // One step below the best input's condition (provisional, transformation-verbs §7 #1):
        // fusing is violent work even when every input was fresh.
        var bestCondition = inputs.Min(input => input.Condition);
        var share = 1.0 / inputs.Count;
        var result = new IdentityMaterialState
        {
            Identities = identities,
            Latent = latent,
            Capacity = capacity,
            Condition = StepDown(bestCondition),
            Quality = (int)Math.Round(inputs.Average(input => input.Quality)),
            IsCarrier = false,
            Roots = RootDerivations.MergeRoots(inputs.Select(input => (input.Roots, share))),
        };

        var steps = new List<VerbStep>
        {
            new(VerbStepKind.SubstanceChanged,
                $"Fused {inputs.Count} substances: {identities.Count} identities on capacity {capacity}."),
        };
        NoteConditionStep(steps, bestCondition, result.Condition);
        if (result.Stability != Stability.Stable)
            steps.Add(new VerbStep(VerbStepKind.Overfilled, $"The fused material is {result.Stability}."));
        return new ResolvedVerb(null, result, Array.Empty<IdentityMaterialState>(), VerbRisks.None, steps);
    }

    private ResolvedVerb ResolveReveal(VerbRequest request)
    {
        var substrate = request.Substrate;
        if (string.IsNullOrWhiteSpace(request.TargetIdentityId))
            return ResolvedVerb.Refused(VerbFailureReason.MissingTargetIdentity);
        if (!substrate.Latent.Contains(request.TargetIdentityId, StringComparer.Ordinal))
            return ResolvedVerb.Refused(VerbFailureReason.IdentityNotLatent);
        if (substrate.Identities.Count >= substrate.Capacity)
            return ResolvedVerb.Refused(VerbFailureReason.NoFreeSlot);

        var risks = RisksFor(substrate, stepsCondition: true);
        var result = substrate with
        {
            Identities = substrate.Identities
                .Append(new IdentityStake(request.TargetIdentityId, ContentValidator.MinIdentityRank))
                .ToArray(),
            Latent = substrate.Latent.Where(id => id != request.TargetIdentityId).ToArray(),
            Condition = StepDown(substrate.Condition),
        };

        var steps = new List<VerbStep>
        {
            new(VerbStepKind.LatentRevealed, $"{request.TargetIdentityId} awakens at rank {ContentValidator.MinIdentityRank}."),
        };
        NoteConditionStep(steps, substrate.Condition, result.Condition);
        NoteRisks(steps, risks);
        return new ResolvedVerb(null, result, Array.Empty<IdentityMaterialState>(), risks, steps);
    }

    private ResolvedVerb ResolveTransfer(VerbRequest request)
    {
        var substrate = request.Substrate;
        if (request.Sources.Count == 0)
            return ResolvedVerb.Refused(VerbFailureReason.NoSources);
        if (request.Sources.Count > 1)
            return ResolvedVerb.Refused(VerbFailureReason.TooManySources);

        var source = request.Sources[0];
        var targetId = request.TargetIdentityId
            ?? (source.Identities.Count == 1 ? source.Identities[0].Id : null);
        if (targetId is null)
            return ResolvedVerb.Refused(VerbFailureReason.MissingTargetIdentity);

        var sourceStake = source.StakeOf(targetId);
        if (sourceStake is null)
            return ResolvedVerb.Refused(VerbFailureReason.SourceLacksIdentity);
        if (substrate.Carries(targetId))
            return ResolvedVerb.Refused(VerbFailureReason.IdentityAlreadyActive); // feeding an identity it has is Develop's job
        if (substrate.Identities.Count + 1 > substrate.Capacity + IdentityCraftTuning.OverfillHardLimit)
            return ResolvedVerb.Refused(VerbFailureReason.OverfillLimit);

        // The rank economy (D47): raw sources deliver rank 1; prepared carriers deliver
        // their full rank. Preparation = fidelity — the rule that interlocks the professions.
        var deliveredRank = source.IsCarrier ? sourceStake.Rank : IdentityCraftTuning.RawTransferRank;

        var risks = RisksFor(substrate, stepsCondition: true);
        var result = substrate with
        {
            Identities = substrate.Identities
                .Append(new IdentityStake(targetId, deliveredRank))
                .ToArray(),
            Latent = substrate.Latent.Where(id => id != targetId).ToArray(),
            Condition = StepDown(substrate.Condition),
            // The source's provenance joins the substrate's — how oak's personality (profile,
            // base, the "Oakbound" name) travels with the identity it delivered.
            Roots = RootDerivations.MergeRoots(new[]
            {
                (substrate.Roots, 1.0 - IdentityCraftTuning.TransferRootShare),
                (source.Roots, IdentityCraftTuning.TransferRootShare),
            }),
        };

        var fidelity = source.IsCarrier ? "carrier fidelity" : "raw transfer";
        var steps = new List<VerbStep>
        {
            new(VerbStepKind.IdentityGained, $"{targetId} settles in at rank {deliveredRank} ({fidelity})."),
        };
        if (result.Stability != Stability.Stable)
            steps.Add(new VerbStep(VerbStepKind.Overfilled,
                $"{result.Identities.Count} identities on capacity {result.Capacity}: the material is now {result.Stability}."));
        NoteConditionStep(steps, substrate.Condition, result.Condition);
        NoteRisks(steps, risks);
        return new ResolvedVerb(null, result, Array.Empty<IdentityMaterialState>(), risks, steps);
    }

    private ResolvedVerb ResolveDevelop(VerbRequest request)
    {
        var substrate = request.Substrate;
        if (string.IsNullOrWhiteSpace(request.TargetIdentityId))
            return ResolvedVerb.Refused(VerbFailureReason.MissingTargetIdentity);
        var stake = substrate.StakeOf(request.TargetIdentityId);
        if (stake is null)
            return ResolvedVerb.Refused(VerbFailureReason.IdentityNotActive);
        if (stake.Rank >= IdentityCraftTuning.MaxRank)
            return ResolvedVerb.Refused(VerbFailureReason.RankAtMaximum);
        if (request.Sources.Count == 0)
            return ResolvedVerb.Refused(VerbFailureReason.NoSources);

        var feedPoints = 0;
        foreach (var source in request.Sources)
        {
            var sourceStake = source.StakeOf(request.TargetIdentityId);
            if (sourceStake is null)
                return ResolvedVerb.Refused(VerbFailureReason.SourceLacksIdentity);
            feedPoints += sourceStake.Rank; // deep sources feed more (D47)
        }

        var cost = IdentityCraftTuning.DevelopCostToLeaveRank(stake.Rank);
        if (feedPoints < cost)
            return ResolvedVerb.Refused(VerbFailureReason.InsufficientDevelopment);

        var risks = RisksFor(substrate, stepsCondition: true);
        var feedShare = IdentityCraftTuning.DevelopRootShare / request.Sources.Count;
        var result = substrate with
        {
            Identities = substrate.Identities
                .Select(s => s.Id == stake.Id ? s with { Rank = s.Rank + 1 } : s)
                .ToArray(),
            Condition = StepDown(substrate.Condition),
            Roots = RootDerivations.MergeRoots(
                request.Sources.Select(source => (source.Roots, feedShare))
                    .Prepend((substrate.Roots, 1.0 - IdentityCraftTuning.DevelopRootShare))),
        };

        var steps = new List<VerbStep>
        {
            new(VerbStepKind.RankRaised,
                $"{stake.Id} deepens to rank {stake.Rank + 1} (fed {feedPoints} against a cost of {cost})."),
        };
        NoteConditionStep(steps, substrate.Condition, result.Condition);
        NoteRisks(steps, risks);
        return new ResolvedVerb(null, result, Array.Empty<IdentityMaterialState>(), risks, steps);
    }

    private ResolvedVerb ResolveExtract(VerbRequest request)
    {
        var substrate = request.Substrate;
        if (string.IsNullOrWhiteSpace(request.TargetIdentityId))
            return ResolvedVerb.Refused(VerbFailureReason.MissingTargetIdentity);
        var stake = substrate.StakeOf(request.TargetIdentityId);
        if (stake is null)
            return ResolvedVerb.Refused(VerbFailureReason.IdentityNotActive);

        // The source is consumed (v1 default; a condition-degrading variant for precious
        // substrates is an authored action parameter later). Consuming it is also why
        // Extract rolls nothing: it is the escape hatch that sacrifices the material.
        var carrier = new IdentityMaterialState
        {
            Identities = new[] { stake },
            Latent = Array.Empty<string>(),
            Capacity = 1,
            Condition = Condition.Pristine,
            Quality = substrate.Quality,
            IsCarrier = true,
            Roots = substrate.Roots.ToArray(),
        };

        var steps = new List<VerbStep>
        {
            new(VerbStepKind.CarrierCreated,
                $"{stake.Id} drawn out at rank {stake.Rank} onto a carrier — rank preserved; the source is spent."),
        };
        return new ResolvedVerb(null, null, new[] { carrier }, VerbRisks.None, steps);
    }

    private ResolvedVerb ResolveDisplace(VerbRequest request)
    {
        var substrate = request.Substrate;
        if (string.IsNullOrWhiteSpace(request.DisplacedIdentityId))
            return ResolvedVerb.Refused(VerbFailureReason.DisplacedIdentityNotActive);
        if (!substrate.Carries(request.DisplacedIdentityId))
            return ResolvedVerb.Refused(VerbFailureReason.DisplacedIdentityNotActive);
        if (request.Sources.Count == 0)
            return ResolvedVerb.Refused(VerbFailureReason.NoSources);
        if (request.Sources.Count > 1)
            return ResolvedVerb.Refused(VerbFailureReason.TooManySources);

        var source = request.Sources[0];
        var incomingId = request.TargetIdentityId
            ?? (source.Identities.Count == 1 ? source.Identities[0].Id : null);
        if (incomingId is null)
            return ResolvedVerb.Refused(VerbFailureReason.MissingTargetIdentity);
        var incoming = source.StakeOf(incomingId);
        if (incoming is null)
            return ResolvedVerb.Refused(VerbFailureReason.SourceLacksIdentity);
        if (substrate.Carries(incomingId) && incomingId != request.DisplacedIdentityId)
            return ResolvedVerb.Refused(VerbFailureReason.IdentityAlreadyActive);
        if (incomingId == request.DisplacedIdentityId)
            return ResolvedVerb.Refused(VerbFailureReason.IdentityAlreadyActive); // upgrading in place is Develop's job

        var deliveredRank = source.IsCarrier ? incoming.Rank : IdentityCraftTuning.RawTransferRank;
        var risks = RisksFor(substrate, stepsCondition: true);
        var result = substrate with
        {
            Identities = substrate.Identities
                .Where(s => s.Id != request.DisplacedIdentityId)
                .Append(new IdentityStake(incomingId, deliveredRank))
                .ToArray(),
            Latent = substrate.Latent.Where(id => id != incomingId).ToArray(),
            Condition = StepDown(substrate.Condition),
            Roots = RootDerivations.MergeRoots(new[]
            {
                (substrate.Roots, 1.0 - IdentityCraftTuning.TransferRootShare),
                (source.Roots, IdentityCraftTuning.TransferRootShare),
            }),
        };

        var steps = new List<VerbStep>
        {
            new(VerbStepKind.IdentityRemoved, $"{request.DisplacedIdentityId} is ejected — no refund."),
            new(VerbStepKind.IdentityGained,
                $"{incomingId} settles in at rank {deliveredRank} ({(source.IsCarrier ? "carrier fidelity" : "raw transfer")})."),
        };
        NoteConditionStep(steps, substrate.Condition, result.Condition);
        NoteRisks(steps, risks);
        return new ResolvedVerb(null, result, Array.Empty<IdentityMaterialState>(), risks, steps);
    }

    private static ResolvedVerb ResolveRefine(VerbRequest request)
    {
        var substrate = request.Substrate;
        if (substrate.Quality >= IdentityCraftTuning.MaxQuality)
            return ResolvedVerb.Refused(VerbFailureReason.QualityAtMaximum);

        var result = substrate with
        {
            Quality = Math.Min(substrate.Quality + IdentityCraftTuning.RefineQualityStep, IdentityCraftTuning.MaxQuality),
        };
        var steps = new List<VerbStep>
        {
            new(VerbStepKind.QualityRaised, $"Quality {substrate.Quality} → {result.Quality}. Gentle work — no condition cost."),
        };
        return new ResolvedVerb(null, result, Array.Empty<IdentityMaterialState>(), VerbRisks.None, steps);
    }

    private static ResolvedVerb ResolveRestore(VerbRequest request)
    {
        var substrate = request.Substrate;
        if (substrate.Condition <= IdentityCraftTuning.RestoreCeiling)
            return ResolvedVerb.Refused(VerbFailureReason.ConditionAtCeiling);

        var result = substrate with { Condition = substrate.Condition - 1 };
        var steps = new List<VerbStep>
        {
            new(VerbStepKind.ConditionRestored,
                $"Condition {substrate.Condition} → {result.Condition}. Pristine cannot be faked — Worked is the ceiling."),
        };
        return new ResolvedVerb(null, result, Array.Empty<IdentityMaterialState>(), VerbRisks.None, steps);
    }

    private ResolvedVerb ResolveExpand(VerbRequest request)
    {
        var substrate = request.Substrate;
        if (substrate.Capacity >= IdentityCraftTuning.ExpandedCapacityCeiling)
            return ResolvedVerb.Refused(VerbFailureReason.CapacityAtCeiling);

        var risks = RisksFor(substrate, stepsCondition: true);
        var result = substrate with
        {
            Capacity = substrate.Capacity + 1,
            Condition = StepDown(substrate.Condition),
        };
        var steps = new List<VerbStep>
        {
            new(VerbStepKind.CapacityExpanded, $"Capacity {substrate.Capacity} → {result.Capacity}."),
        };
        NoteConditionStep(steps, substrate.Condition, result.Condition);
        NoteRisks(steps, risks);
        return new ResolvedVerb(null, result, Array.Empty<IdentityMaterialState>(), risks, steps);
    }
}
