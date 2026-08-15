using Dungeons.Characters.Composition;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Professions;

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
        InstanceIdSource? instanceIds = null)
    {
        ArgumentNullException.ThrowIfNull(stash);
        ArgumentNullException.ThrowIfNull(professions);
        ArgumentNullException.ThrowIfNull(discoveries);
        ArgumentNullException.ThrowIfNull(realmKnowledge);

        return new SaveData
        {
            SavedAtTick = savedAtTick,
            Build = build,
            Stash = stash.Snapshot().ToList(),
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
        };
    }

    public static void Apply(
        SaveData save,
        Inventory stash,
        ProfessionSystem professions,
        DiscoverySystem discoveries,
        IDictionary<string, int> realmKnowledge,
        Equipment? equipment = null,
        InstanceIdSource? instanceIds = null)
    {
        ArgumentNullException.ThrowIfNull(save);

        stash.Clear();
        foreach (var stack in save.Stash)
            stash.Add(stack);
        foreach (var instance in save.StashInstances)
            stash.AddInstance(FromSave(instance));

        if (equipment is not null)
        {
            equipment.Clear();
            foreach (var pair in save.Equipment)
            {
                if (Enum.TryParse<EquipmentSlot>(pair.Key, out var slot))
                    equipment.Equip(slot, FromSave(pair.Value));
            }
        }

        instanceIds?.EnsureAtLeast(save.NextInstanceId);

        professions.RestoreProgress(save.Professions.Select(ToProgress));

        discoveries.Restore(save.Discoveries);

        realmKnowledge.Clear();
        foreach (var pair in save.RealmKnowledge)
            realmKnowledge[pair.Key] = pair.Value;
    }

    private static ProfessionProgress ToProgress(ProfessionSave save)
    {
        var progress = new ProfessionProgress(save.ProfessionId, save.Xp);
        foreach (var pair in save.Mastery)
            progress.AddMastery(pair.Key, pair.Value);
        return progress;
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
    };
}
