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
        long savedAtTick)
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
        IDictionary<string, int> realmKnowledge)
    {
        ArgumentNullException.ThrowIfNull(save);

        stash.Clear();
        foreach (var stack in save.Stash)
            stash.Add(stack);

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
}
