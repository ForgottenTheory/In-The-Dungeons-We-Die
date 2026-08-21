using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting.Identity;

/// <summary>One bench click: which action, on what, fed with what.</summary>
public sealed record VerbActionInvocation(
    string ActionId,
    string SubstrateItemId,
    IReadOnlyList<string> SourceItemIds,
    string? TargetIdentityId = null,
    string? DisplacedIdentityId = null);

/// <summary>Why the bench refused before the verb even ran. Gates are deterministic and
/// engine refusals (<see cref="VerbFailureReason"/>) are separate — the gate is about the
/// player's situation, the refusal is about the material's.</summary>
public enum VerbActionGateFailure
{
    UnknownAction,
    ProfessionLevelTooLow,
    SubstrateNotOnHand,
    SubstrateNotMigrated,
    SubstrateOutsideDomain,
    SourceNotOnHand,
    SourceNotMigrated,
    TargetOutsideScope,
    MissingExtraCosts,
}

public sealed record VerbActionPreview(
    VerbActionGateFailure? GateFailure, string? GateDetail, VerbProjection? Projection);

/// <summary>One thing the bench put in the bag, and whether the world had ever seen it.</summary>
public sealed record DepositedItem(string ItemId, string Name, bool FirstDiscovery);

public sealed record VerbActionResult(
    VerbActionGateFailure? GateFailure, string? GateDetail,
    VerbOutcome? Outcome,
    IReadOnlyList<DepositedItem> Deposited,
    bool AnyFirstDiscovery);

/// <summary>
/// The application seam of the identity bench (migration Phase 2c): resolves a
/// <see cref="VerbActionDefinition"/>'s gates, runs the engine, consumes the inputs, and
/// registers + deposits what came out. <c>GameRoot</c> forwards into this and formats — it
/// decides nothing (D2).
///
/// <para><b>The authored-equivalence rule:</b> a result indistinguishable from an authored
/// definition's own starting state deposits <em>as that definition</em> — plain smelted ore
/// becomes <c>material.iron_ingot</c>, never an emergent twin — so mundane chains keep the
/// ids loot tables and profession actions already reference. Everything else registers under
/// its fingerprint into the shared store (the D20 rule: identical results stack, materials
/// are never per-unit instances).</para>
/// </summary>
public sealed class VerbActionRunner
{
    private readonly ContentBundle _content;
    private readonly IdentityCraftingEngine _engine;
    private readonly IEmergentRegistry _registry;
    private readonly Func<Inventory> _inventory;
    private readonly Func<string, int> _professionLevel;

    public VerbActionRunner(
        ContentBundle content,
        IdentityCraftingEngine engine,
        IEmergentRegistry registry,
        Func<Inventory> inventory,
        Func<string, int> professionLevel)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _professionLevel = professionLevel ?? throw new ArgumentNullException(nameof(professionLevel));
    }

    public VerbActionPreview Preview(VerbActionInvocation invocation)
    {
        var gate = CheckGates(invocation);
        if (gate.Failure is not null)
            return new VerbActionPreview(gate.Failure, gate.Detail, null);

        return new VerbActionPreview(null, null, _engine.Preview(gate.Request!));
    }

    public VerbActionResult Run(VerbActionInvocation invocation)
    {
        var gate = CheckGates(invocation);
        if (gate.Failure is not null)
            return new VerbActionResult(gate.Failure, gate.Detail, null, Array.Empty<DepositedItem>(), false);

        var outcome = _engine.Commit(gate.Request!);
        if (outcome.Kind == VerbResultKind.Refused)
            return new VerbActionResult(null, null, outcome, Array.Empty<DepositedItem>(), false);

        // The gamble was taken or the work was done — either way the inputs are spent.
        _inventory().TryRemoveAll(gate.Consumption!);

        var deposited = new List<DepositedItem>();
        if (outcome.Byproduct is { } byproduct)
        {
            _inventory().Add(byproduct);
            var name = _content.Materials.TryGetById(byproduct.ItemId, out var def) ? def.Name : byproduct.ItemId;
            deposited.Add(new DepositedItem(byproduct.ItemId, name, false));
        }
        if (outcome.Result is { } result)
            deposited.Add(Deposit(result));
        foreach (var produced in outcome.Produced)
            deposited.Add(Deposit(produced));

        return new VerbActionResult(null, null, outcome, deposited, deposited.Any(item => item.FirstDiscovery));
    }

    // --- Gates ---------------------------------------------------------------

    private sealed record GateCheck(
        VerbActionGateFailure? Failure, string? Detail,
        VerbRequest? Request = null, IReadOnlyCollection<ItemStack>? Consumption = null);

    private GateCheck CheckGates(VerbActionInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        if (!_content.VerbActions.TryGetById(invocation.ActionId, out var action))
            return new GateCheck(VerbActionGateFailure.UnknownAction, $"Unknown action '{invocation.ActionId}'.");

        if (!string.IsNullOrEmpty(action.Profession))
        {
            var level = _professionLevel(action.Profession);
            if (level < action.RequiredLevel)
                return new GateCheck(VerbActionGateFailure.ProfessionLevelTooLow,
                    $"{action.Name} needs {action.Profession} {action.RequiredLevel} (have {level}).");
        }

        if (!_content.Materials.TryGetById(invocation.SubstrateItemId, out var substrateDefinition))
            return new GateCheck(VerbActionGateFailure.SubstrateNotOnHand,
                $"'{invocation.SubstrateItemId}' is not a known material.");
        var substrateState = IdentityStateResolver.StateOf(substrateDefinition);
        if (substrateState is null)
            return new GateCheck(VerbActionGateFailure.SubstrateNotMigrated,
                $"{substrateDefinition.Name} has no identity model yet — the new bench cannot work it.");

        if (action.SubstrateTags.Count > 0
            && !action.SubstrateTags.Any(tag => substrateDefinition.Tags.Contains(tag, StringComparer.Ordinal)))
        {
            return new GateCheck(VerbActionGateFailure.SubstrateOutsideDomain,
                $"{action.Name} works {string.Join("/", action.SubstrateTags)}; {substrateDefinition.Name} is none of those.");
        }
        if (!string.IsNullOrWhiteSpace(action.SubstrateId))
        {
            var primaryRoot = substrateState.Roots.OrderByDescending(root => root.Weight)
                .FirstOrDefault()?.DefinitionId;
            if (substrateDefinition.Id != action.SubstrateId && primaryRoot != action.SubstrateId)
                return new GateCheck(VerbActionGateFailure.SubstrateOutsideDomain,
                    $"{action.Name} works {action.SubstrateId}; {substrateDefinition.Name} descends from something else.");
        }

        var sourceStates = new List<IdentityMaterialState>();
        foreach (var sourceItemId in invocation.SourceItemIds)
        {
            if (!_content.Materials.TryGetById(sourceItemId, out var sourceDefinition))
                return new GateCheck(VerbActionGateFailure.SourceNotOnHand, $"'{sourceItemId}' is not a known material.");
            var sourceState = IdentityStateResolver.StateOf(sourceDefinition);
            if (sourceState is null)
                return new GateCheck(VerbActionGateFailure.SourceNotMigrated,
                    $"{sourceDefinition.Name} has no identity model yet.");
            sourceStates.Add(sourceState);
        }

        if (action.IdentityScope.Count > 0)
        {
            var effectiveTarget = invocation.TargetIdentityId
                ?? (sourceStates.Count == 1 && sourceStates[0].Identities.Count == 1
                    ? sourceStates[0].Identities[0].Id
                    : null);
            if (effectiveTarget is not null
                && !action.IdentityScope.Contains(effectiveTarget, StringComparer.Ordinal))
            {
                return new GateCheck(VerbActionGateFailure.TargetOutsideScope,
                    $"{action.Name} works only {string.Join(", ", action.IdentityScope)}.");
            }
        }

        // Everything the commit will spend, grouped — the substrate, every source, the costs.
        var needs = new Dictionary<string, int>(StringComparer.Ordinal) { [invocation.SubstrateItemId] = 1 };
        foreach (var sourceItemId in invocation.SourceItemIds)
            needs[sourceItemId] = needs.GetValueOrDefault(sourceItemId) + 1;
        foreach (var cost in action.ExtraCosts)
            needs[cost.ItemId] = needs.GetValueOrDefault(cost.ItemId) + cost.Quantity;

        foreach (var (itemId, quantity) in needs)
        {
            if (_inventory().Contains(itemId, quantity))
                continue;
            var failure =
                itemId == invocation.SubstrateItemId ? VerbActionGateFailure.SubstrateNotOnHand
                : invocation.SourceItemIds.Contains(itemId, StringComparer.Ordinal) ? VerbActionGateFailure.SourceNotOnHand
                : VerbActionGateFailure.MissingExtraCosts;
            var name = _content.Materials.TryGetById(itemId, out var def) ? def.Name : itemId;
            return new GateCheck(failure, $"Needs {name} ×{quantity}.");
        }

        var request = new VerbRequest
        {
            Verb = action.Verb,
            Substrate = substrateState,
            Sources = sourceStates,
            TargetIdentityId = invocation.TargetIdentityId,
            DisplacedIdentityId = invocation.DisplacedIdentityId,
            OutputDefinitionId = action.Output,
        };
        var consumption = needs.Select(pair => new ItemStack(pair.Key, pair.Value)).ToList();
        return new GateCheck(null, null, request, consumption);
    }

    // --- Deposit -------------------------------------------------------------

    private DepositedItem Deposit(IdentityMaterialState state)
    {
        var primaryRootId = state.Roots.OrderByDescending(root => root.Weight)
            .FirstOrDefault()?.DefinitionId;
        var primaryRoot = primaryRootId is not null
            && _content.Materials.TryGetById(primaryRootId, out var rootDefinition)
                ? rootDefinition
                : null;

        // Authored equivalence: indistinguishable from the authored starting state means it
        // IS the authored material — mundane chains keep their ids.
        if (primaryRoot is { IdentityState: null }
            && IdentityStateResolver.StateOf(primaryRoot) is { } baseline
            && Fingerprint.Compute(baseline, primaryRoot.Tags) == Fingerprint.Compute(state, primaryRoot.Tags))
        {
            _inventory().Add(primaryRoot.Id, 1);
            return new DepositedItem(primaryRoot.Id, primaryRoot.Name, false);
        }

        var tags = DerivedTags(primaryRoot, state);
        var fingerprint = Fingerprint.Compute(state, tags);
        var lookup = _registry.GetOrRegister(fingerprint, () => new MaterialDefinition
        {
            Id = fingerprint,
            Name = IdentityNameGenerator.NameFor(state, _content),
            Tags = tags,
            Capacity = state.Capacity,
            Identities = state.Identities.Select(stake => new IdentityGrant { Id = stake.Id, Rank = stake.Rank }).ToArray(),
            Latent = state.Latent.ToArray(),
            IdentityState = state,
        });

        _inventory().Add(lookup.Definition.Id, 1);
        return new DepositedItem(lookup.Definition.Id, lookup.Definition.Name, lookup.IsFirstDiscovery);
    }

    /// <summary>The physical families carry from the primary root (so domain gates keep
    /// working on emergent materials); the state family says what it has become.</summary>
    private static IReadOnlyList<string> DerivedTags(MaterialDefinition? primaryRoot, IdentityMaterialState state)
    {
        var carried = primaryRoot?.Tags
            .Where(tag => tag.StartsWith("form:", StringComparison.Ordinal)
                       || tag.StartsWith("origin:", StringComparison.Ordinal)
                       || tag.StartsWith("comp:", StringComparison.Ordinal)
                       || tag.StartsWith("rarity:", StringComparison.Ordinal))
            ?? Enumerable.Empty<string>();
        return carried
            .Append(state.IsCarrier ? "state:extract" : "state:refined")
            .ToArray();
    }
}
