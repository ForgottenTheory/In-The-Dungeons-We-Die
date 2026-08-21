using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Realms;

namespace Dungeons.Persistence;

/// <summary>
/// Maps between the live gameplay systems and a <see cref="SaveData"/>. All inputs
/// are engine-independent, so capture/apply is fully unit-testable. The Godot layer
/// owns file IO; this layer owns what a save contains and how it is restored.
/// </summary>
public static class SaveMapper
{
    public static SaveData Capture(
        CharacterBuild? build,
        Inventory stash,
        ProfessionSystem professions,
        DiscoverySystem discoveries,
        IReadOnlyDictionary<string, int> realmKnowledge,
        long savedAtTick,
        Equipment? equipment = null,
        InstanceIdSource? instanceIds = null,
        IEmergentRegistry? emergentRegistry = null,
        LearnedMoves? learnedMoves = null,
        IEnumerable<Items.EquipmentDefinition>? emergentEquipment = null,
        FarmingPlots? farmingPlots = null,
        TrainingCourse? trainingCourse = null,
        string? passiveActionId = null,
        long savedAtUnixSeconds = 0,
        RunLoadout? loadout = null,
        CharacterProgress? characterProgress = null)
    {
        ArgumentNullException.ThrowIfNull(stash);
        ArgumentNullException.ThrowIfNull(professions);
        ArgumentNullException.ThrowIfNull(discoveries);
        ArgumentNullException.ThrowIfNull(realmKnowledge);

        return new SaveData
        {
            SavedAtTick = savedAtTick,
            SavedAtUnixSeconds = savedAtUnixSeconds,
            PassiveActionId = passiveActionId,
            FarmingPlots = farmingPlots is null
                ? new List<FarmingPlotSave>()
                : farmingPlots.Plots
                    .Where(plot => !plot.IsEmpty)
                    .Select(plot => new FarmingPlotSave
                    {
                        Index = plot.Index,
                        ActionId = plot.PlantedActionId!,
                        ReadyAtTick = plot.ReadyAtTick,
                    })
                    .ToList(),
            TrainingCourse = trainingCourse is null
                ? new List<TrainingCourseSlotSave>()
                : trainingCourse.Fitted
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new TrainingCourseSlotSave
                    {
                        Slot = pair.Key.ToString(),
                        ObstacleId = pair.Value,
                    })
                    .ToList(),
            Loadout = loadout is null
                ? null
                : new LoadoutSave { RealmId = loadout.RealmId, Packed = loadout.PackedStacks().ToList() },
            CharacterXp = characterProgress?.Xp ?? 0,
            Build = build,
            Stash = stash.Snapshot().ToList(),
            Gold = stash.Gold,
            StashInstances = stash.Instances.Select(ToSave).ToList(),
            Equipment = equipment is null
                ? new Dictionary<string, ItemInstanceSave>()
                : equipment.Slots.ToDictionary(pair => pair.Key.ToString(), pair => ToSave(pair.Value)),
            NextInstanceId = instanceIds?.Peek() ?? 1,
            Professions = professions.AllProgress
                .Select(p => new ProfessionSave
                {
                    ProfessionId = p.ProfessionId,
                    Xp = p.Xp,
                    Mastery = new Dictionary<string, int>(p.Masteries),
                })
                .ToList(),
            RealmKnowledge = new Dictionary<string, int>(realmKnowledge),
            Discoveries = discoveries.All.ToList(),
            EmergentArchetypes = emergentRegistry is null
                ? new List<EmergentArchetypeSave>()
                : emergentRegistry.All.Where(a => a.IdentityState is null).Select(ToSave).ToList(),
            IdentityArchetypes = emergentRegistry is null
                ? new List<IdentityArchetypeSave>()
                : emergentRegistry.All.Where(a => a.IdentityState is not null).Select(ToIdentitySave).ToList(),
            LearnedMoves = learnedMoves?.All.ToList() ?? new List<string>(),
            EmergentEquipment = (emergentEquipment ?? Enumerable.Empty<Items.EquipmentDefinition>())
                .Select(ToSave).ToList(),
        };
    }

    /// <summary>
    /// Reads a persisted slot name. <b>The project's first save migration</b>: schemas v1–v8
    /// called the torso slot <c>Armor</c>, which stopped being true the moment head, hands and
    /// feet existed. Without this, a v8 save would fail to parse the key and silently drop
    /// whatever the player was wearing — so the rename buys a coherent slot vocabulary for the
    /// cost of these three lines (docs/code-map.md §12, DECISIONS D32).
    /// </summary>
    private static bool TryReadSlot(string persistedName, out EquipmentSlot slot)
    {
        if (string.Equals(persistedName, EquipmentSlots.LegacyBodySlotName, StringComparison.Ordinal))
        {
            slot = EquipmentSlot.Body;
            return true;
        }

        return Enum.TryParse(persistedName, out slot);
    }

    private static EquipmentArchetypeSave ToSave(Items.EquipmentDefinition definition) => new()
    {
        Id = definition.Id,
        Name = definition.Name,
        Slot = definition.Slot.ToString(),
        Tags = definition.Tags.ToList(),
        MoveIds = definition.Moves.Select(m => m.Id).ToList(),
        HasArmor = definition.Armor is not null,
        ArmorValue = definition.Armor?.Armor ?? 0,
        ArmorResistances = new Dictionary<string, double>(definition.Armor?.Resistances ?? new()),
        Properties = new Dictionary<string, double>(definition.Properties),
        ExpressedTraits = definition.ExpressedTraits.ToDictionary(t => t.Id, t => t.Magnitude),
        DormantTraits = definition.DormantTraits.ToDictionary(t => t.Id, t => t.Magnitude),
        Essence = new Dictionary<string, double>(definition.Essence),
    };

    private static Items.EquipmentDefinition FromSave(EquipmentArchetypeSave save) => new()
    {
        Id = save.Id,
        Name = save.Name,
        // Same v9 rename: a fabricated vest stored before the slot vocabulary grew says "Armor".
        Slot = TryReadSlot(save.Slot, out var slot) ? slot : Items.EquipmentSlot.Weapon,
        Tags = save.Tags,
        Moves = save.MoveIds.Select(id => new Combat.MoveGrantSpec { Id = id }).ToList(),
        Armor = save.HasArmor
            ? new Items.ArmorStats { Armor = save.ArmorValue, Resistances = new Dictionary<string, double>(save.ArmorResistances) }
            : null,
        Properties = new Dictionary<string, double>(save.Properties),
        ExpressedTraits = save.ExpressedTraits.OrderBy(t => t.Key, StringComparer.Ordinal)
            .Select(t => new Crafting.TraitInstance(t.Key, t.Value)).ToList(),
        DormantTraits = save.DormantTraits.OrderBy(t => t.Key, StringComparer.Ordinal)
            .Select(t => new Crafting.TraitInstance(t.Key, t.Value)).ToList(),
        Essence = new Dictionary<string, double>(save.Essence),
    };

    public static void Apply(
        SaveData save,
        Inventory stash,
        ProfessionSystem professions,
        DiscoverySystem discoveries,
        IDictionary<string, int> realmKnowledge,
        Equipment? equipment = null,
        InstanceIdSource? instanceIds = null,
        IEmergentRegistry? emergentRegistry = null,
        LearnedMoves? learnedMoves = null,
        DataStore<Items.EquipmentDefinition>? equipmentStore = null,
        FarmingPlots? farmingPlots = null,
        TrainingCourse? trainingCourse = null,
        RunLoadout? loadout = null,
        CharacterProgress? characterProgress = null)
    {
        ArgumentNullException.ThrowIfNull(save);

        // Restored first: stash stacks may refer to emergent archetype ids, which nothing else
        // in the game can resolve until they are back in the material store.
        emergentRegistry?.Restore(save.EmergentArchetypes.Select(FromSave));
        emergentRegistry?.Restore(save.IdentityArchetypes.Select(FromSave));

        stash.Clear();
        foreach (var stack in save.Stash)
            stash.Add(stack);
        foreach (var instance in save.StashInstances)
            stash.AddInstance(FromSave(instance));
        stash.RestoreGold(save.Gold);

        if (equipment is not null)
        {
            equipment.Clear();
            foreach (var pair in save.Equipment)
            {
                if (TryReadSlot(pair.Key, out var slot))
                    equipment.Equip(slot, FromSave(pair.Value));
            }
        }

        instanceIds?.EnsureAtLeast(save.NextInstanceId);

        professions.RestoreProgress(save.Professions.Select(ToProgress));

        discoveries.Restore(save.Discoveries);

        realmKnowledge.Clear();
        foreach (var pair in save.RealmKnowledge)
            realmKnowledge[pair.Key] = pair.Value;

        learnedMoves?.Restore(save.LearnedMoves);

        farmingPlots?.Restore(save.FarmingPlots.Select(plot => (plot.Index, plot.ActionId, plot.ReadyAtTick)));

        // A v9 save has no loadout at all, which restores as "never prepared" — no destination
        // and an empty pack. That is the same state a new game starts in, so there is nothing
        // to migrate.
        loadout?.Restore(save.Loadout?.RealmId, save.Loadout?.Packed ?? Enumerable.Empty<ItemStack>());

        // A v10 save has no character XP, which restores as level 1 — where every existing
        // character already is, because nothing awarded it until Phase 8.
        characterProgress?.Restore(save.CharacterXp);

        trainingCourse?.Restore(save.TrainingCourse
            .Where(slot => Enum.TryParse<TrainingSlot>(slot.Slot, out _))
            .Select(slot => (Enum.Parse<TrainingSlot>(slot.Slot), slot.ObstacleId)));

        // Fabrication-derived gear (C2a) — restored before anything resolves the stash's
        // instances, exactly like emergent material archetypes.
        if (equipmentStore is not null)
        {
            foreach (var archetype in save.EmergentEquipment.Where(a => !equipmentStore.Contains(a.Id)))
                equipmentStore.Add(FromSave(archetype));
        }
    }

    private static ProfessionProgress ToProgress(ProfessionSave save)
    {
        var progress = new ProfessionProgress(save.ProfessionId, save.Xp);
        foreach (var pair in save.Mastery)
            progress.AddMastery(pair.Key, pair.Value);
        return progress;
    }

    private static EmergentArchetypeSave ToSave(MaterialDefinition archetype)
    {
        var profile = archetype.State
            ?? throw new InvalidOperationException($"Emergent archetype '{archetype.Id}' has no profile to save.");

        return new EmergentArchetypeSave
        {
            Signature = archetype.Id,
            Name = archetype.Name,
            Tags = archetype.Tags.ToList(),
            Properties = new Dictionary<string, double>(profile.Properties.AsDictionary()),
            Potency = profile.MaterialStrength,          // save key stays "Potency" (v4+)
            Integrity = profile.Workability,             // save key stays "Integrity" (v4+)
            Generation = profile.Generation,
            ProcessId = profile.Lineage.CraftingActionId,   // save key stays "ProcessId" (v4+)
            Roots = profile.Lineage.Roots
                .Select(r => new LineageRootSave { RootId = r.RootId, Weight = r.Weight })
                .ToList(),
            ParentSignatures = profile.Lineage.ParentSignatures.ToList(),
            Traits = profile.Traits.ToDictionary(t => t.Id, t => t.Magnitude),
            Essence = new Dictionary<string, double>(profile.Essence),
        };
    }

    private static MaterialDefinition FromSave(EmergentArchetypeSave save) => new()
    {
        Id = save.Signature,
        Name = save.Name,
        Tags = save.Tags,
        Properties = new Dictionary<string, double>(save.Properties),
        State = new MaterialState(
            Properties: new PropertySet(save.Properties),
            MaterialStrength: save.Potency,
            Workability: save.Integrity,
            Lineage: new Lineage(
                save.Roots.Select(r => new RootShare(r.RootId, r.Weight)).ToList(),
                save.Generation,
                save.ProcessId,
                save.ParentSignatures),
            Signature: save.Signature)
        {
            Traits = save.Traits
                .OrderBy(t => t.Key, StringComparer.Ordinal)
                .Select(t => new Crafting.TraitInstance(t.Key, t.Value))
                .ToList(),
            Essence = new Dictionary<string, double>(save.Essence),
        },
    };

    private static IdentityArchetypeSave ToIdentitySave(MaterialDefinition archetype)
    {
        var state = archetype.IdentityState
            ?? throw new InvalidOperationException($"Identity archetype '{archetype.Id}' has no state to save.");

        return new IdentityArchetypeSave
        {
            Id = archetype.Id,
            Name = archetype.Name,
            Tags = archetype.Tags.ToList(),
            Identities = state.Identities
                .Select(stake => new IdentityStakeSave { Id = stake.Id, Rank = stake.Rank })
                .ToList(),
            Latent = state.Latent.ToList(),
            Capacity = state.Capacity,
            Condition = state.Condition.ToString(),
            Quality = state.Quality,
            IsCarrier = state.IsCarrier,
            Roots = state.Roots
                .Select(root => new IdentityRootSave { DefinitionId = root.DefinitionId, Weight = root.Weight })
                .ToList(),
        };
    }

    private static MaterialDefinition FromSave(IdentityArchetypeSave save)
    {
        var state = new Crafting.Identity.IdentityMaterialState
        {
            Identities = save.Identities
                .Select(stake => new Crafting.Identity.IdentityStake(stake.Id, stake.Rank))
                .ToList(),
            Latent = save.Latent.ToList(),
            Capacity = save.Capacity,
            Condition = Enum.TryParse<Crafting.Identity.Condition>(save.Condition, out var condition)
                ? condition
                : Crafting.Identity.Condition.Pristine,
            Quality = save.Quality,
            IsCarrier = save.IsCarrier,
            Roots = save.Roots
                .Select(root => new Crafting.Identity.ProvenanceRoot(root.DefinitionId, root.Weight))
                .ToList(),
        };

        return new MaterialDefinition
        {
            Id = save.Id,
            Name = save.Name,
            Tags = save.Tags,
            Capacity = save.Capacity,
            Identities = save.Identities
                .Select(stake => new IdentityGrant { Id = stake.Id, Rank = stake.Rank })
                .ToList(),
            Latent = save.Latent.ToList(),
            IdentityState = state,
        };
    }

    private static ItemInstanceSave ToSave(ItemInstance instance) => new()
    {
        InstanceId = instance.InstanceId,
        BaseDefinitionId = instance.BaseDefinitionId,
        ItemType = instance.ItemType,
        DisplayName = instance.DisplayName,
        Quality = instance.Quality,
        Properties = new Dictionary<string, double>(instance.Properties.AsDictionary()),
        Provenance = instance.Provenance.ToList(),
        Traits = instance.Traits.ToList(),
        Genome = instance.Potential is { } potential            // save key stays "Genome" (v6)
            ? new GenomeSave
            {
                FormId = potential.BlueprintId,                     // save key stays "FormId"
                Pressure = new Dictionary<string, double>(                // save key stays "Pressure"
                    potential.MaterialInfluence.ToDictionary(entry => entry.Key, entry => entry.Value)),
                Essence = new Dictionary<string, double>(
                    potential.Essence.ToDictionary(entry => entry.Key, entry => entry.Value)),
                Expressed = potential.Expressed.Select(t => new TraitInstanceSave { Id = t.Id, Magnitude = t.Magnitude }).ToList(),
                Dormant = potential.Dormant.Select(t => new TraitInstanceSave { Id = t.Id, Magnitude = t.Magnitude }).ToList(),
                Tags = potential.Tags.ToList(),
                Potency = potential.MaterialStrength,               // save key stays "Potency" (v6)
                GenerationDepth = potential.GenerationDepth,
                Signatures = potential.Signatures.ToList(),
            }
            : null,
        Affixes = instance.Affixes
            .Select(a => new RolledAffixSave { AffixId = a.AffixId, Tier = a.Tier, Roll = a.Roll })
            .ToList(),
    };

    private static ItemInstance FromSave(ItemInstanceSave save) => new()
    {
        InstanceId = save.InstanceId,
        BaseDefinitionId = save.BaseDefinitionId,
        ItemType = save.ItemType,
        DisplayName = save.DisplayName,
        Quality = save.Quality,
        Properties = new PropertySet(save.Properties),
        Provenance = save.Provenance,
        Traits = save.Traits,
        Potential = save.Genome is { } savedPotential
            ? new Dungeons.Crafting.ItemPotential(
                savedPotential.FormId,
                savedPotential.Pressure,
                savedPotential.Essence,
                savedPotential.Expressed.Select(t => new Dungeons.Crafting.TraitInstance(t.Id, t.Magnitude)).ToList(),
                savedPotential.Dormant.Select(t => new Dungeons.Crafting.TraitInstance(t.Id, t.Magnitude)).ToList(),
                savedPotential.Tags,
                savedPotential.Potency,
                savedPotential.GenerationDepth,
                savedPotential.Signatures)
            : null,
        Affixes = save.Affixes
            .Select(a => new Dungeons.Affixes.RolledAffix(a.AffixId, a.Tier, a.Roll))
            .ToList(),
    };
}
