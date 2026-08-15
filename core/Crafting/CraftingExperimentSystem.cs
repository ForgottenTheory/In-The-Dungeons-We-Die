using System.Linq;
using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>
/// Resolves crafting experiments: given the materials the player chose to combine
/// and their profession knowledge, it finds a matching interaction, consumes the
/// inputs, produces the result, and records the discovery. A discovered interaction
/// is simply re-craftable — the same call succeeds again without being a new
/// discovery, so recipes and discoveries are one mechanism (docs/crafting.md §5–6).
/// Holds no balance formulas; matching and gating only.
/// </summary>
public sealed class CraftingExperimentSystem
{
    private readonly DataStore<CraftingInteractionDefinition> _interactions;
    private readonly DataStore<MaterialDefinition> _materials;
    private readonly Inventory _inventory;
    private readonly DiscoverySystem _discoveries;
    private readonly Func<string, int> _professionLevel;
    private readonly InstanceIdSource _instanceIds;

    public CraftingExperimentSystem(
        DataStore<CraftingInteractionDefinition> interactions,
        DataStore<MaterialDefinition> materials,
        Inventory inventory,
        DiscoverySystem discoveries,
        Func<string, int> professionLevel,
        InstanceIdSource? instanceIds = null)
    {
        _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        _materials = materials ?? throw new ArgumentNullException(nameof(materials));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _discoveries = discoveries ?? throw new ArgumentNullException(nameof(discoveries));
        _professionLevel = professionLevel ?? throw new ArgumentNullException(nameof(professionLevel));
        _instanceIds = instanceIds ?? new InstanceIdSource();
    }

    /// <summary>
    /// Attempts to craft using the submitted set of material ids (order-independent).
    /// </summary>
    public ExperimentOutcome Experiment(IReadOnlyCollection<string> submittedItemIds)
    {
        ArgumentNullException.ThrowIfNull(submittedItemIds);
        var submitted = new HashSet<string>(submittedItemIds, StringComparer.Ordinal);

        // Candidates: interactions whose full input set was submitted.
        var candidates = _interactions.GetAll()
            .Where(i => i.Inputs.All(input => submitted.Contains(input.ItemId)))
            .ToList();

        if (candidates.Count == 0)
            return ExperimentOutcome.Failed(ExperimentFailure.NoMatch);

        ExperimentOutcome? firstFailure = null;
        foreach (var interaction in candidates)
        {
            var outcome = TryPerform(interaction);
            if (outcome.Success)
                return outcome;
            firstFailure ??= outcome;
        }

        return firstFailure!;
    }

    private ExperimentOutcome TryPerform(CraftingInteractionDefinition interaction)
    {
        foreach (var requirement in interaction.ProfessionRequirements)
        {
            if (_professionLevel(requirement.ProfessionId) < requirement.Level)
                return ExperimentOutcome.Failed(ExperimentFailure.ProfessionTooLow, interaction.Id, requirement.ProfessionId, requirement.Level);
        }

        if (!_inventory.CanRemoveAll(interaction.Inputs))
            return ExperimentOutcome.Failed(ExperimentFailure.MissingInputs, interaction.Id);

        _inventory.TryRemoveAll(interaction.Inputs);

        ItemInstance? produced = null;
        IReadOnlyList<MaterialProperty> resultProperties;

        if (interaction.ResultIsInstance)
        {
            // Generated material: derive its properties from the inputs and mint unique instances.
            var inputProperties = interaction.Inputs
                .Select(i => _materials.TryGetById(i.ItemId, out var m) ? m.BaseProperties : PropertySet.Empty)
                .ToList();
            var derived = CraftingDerivation.Derive(inputProperties);
            var name = _materials.TryGetById(interaction.ResultItemId, out var resultDef) ? resultDef.Name : interaction.ResultItemId;
            var provenance = interaction.Inputs.Select(i => i.ItemId).ToList();

            for (var q = 0; q < interaction.ResultQuantity; q++)
            {
                produced = new ItemInstance
                {
                    InstanceId = _instanceIds.Next(),
                    BaseDefinitionId = interaction.ResultItemId,
                    ItemType = ItemType.Material,
                    DisplayName = name,
                    Properties = derived,
                    Provenance = provenance,
                };
                _inventory.AddInstance(produced);
            }

            resultProperties = derived.AsDictionary()
                .Select(kv => new MaterialProperty { Property = kv.Key, Value = kv.Value })
                .ToList();
        }
        else
        {
            _inventory.Add(interaction.ResultItemId, interaction.ResultQuantity);
            resultProperties = _materials.TryGetById(interaction.ResultItemId, out var material)
                ? material.BaseProperties.AsDictionary()
                    .Select(kv => new MaterialProperty { Property = kv.Key, Value = kv.Value })
                    .ToList()
                : Array.Empty<MaterialProperty>();
        }

        var wasNew = !string.IsNullOrEmpty(interaction.DiscoveryId) && _discoveries.Record(interaction.DiscoveryId);

        return new ExperimentOutcome
        {
            Success = true,
            InteractionId = interaction.Id,
            ResultItemId = interaction.ResultItemId,
            ResultQuantity = interaction.ResultQuantity,
            WasNewDiscovery = wasNew,
            ResultProperties = resultProperties,
            ProducedInstance = produced,
        };
    }
}
