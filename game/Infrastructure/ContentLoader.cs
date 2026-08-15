using System.Collections.Generic;
using Dungeons.Content;
using Godot;

namespace Dungeons.Game.Infrastructure;

/// <summary>
/// The Godot-side bridge that reads JSON content from <c>res://</c> and feeds the
/// raw text into an engine-independent <see cref="DataStore{T}"/>. Core never sees
/// a Godot path — file access lives entirely on this side of the boundary.
/// </summary>
public static class ContentLoader
{
    public static DataStore<MaterialDefinition> LoadMaterials(string directory) =>
        LoadDefinitions<MaterialDefinition>(directory);

    /// <summary>
    /// Loads every <c>.json</c> definition of type <typeparamref name="T"/> from a directory.
    /// Each file may be a single object or an array of objects (e.g. materials grouped by
    /// category), auto-detected per file.
    /// </summary>
    public static DataStore<T> LoadDefinitions<T>(string directory) where T : IDefinition
    {
        var store = new DataStore<T>();
        store.LoadDocuments(ReadJsonFiles(directory));
        return store;
    }

    /// <summary>Returns the text of every <c>.json</c> file directly under <paramref name="directory"/>.</summary>
    public static IReadOnlyList<string> ReadJsonFiles(string directory)
    {
        var results = new List<string>();

        using var dir = DirAccess.Open(directory);
        if (dir is null)
        {
            GD.PushWarning($"[ContentLoader] Directory not found: {directory}");
            return results;
        }

        dir.ListDirBegin();
        for (var name = dir.GetNext(); name != string.Empty; name = dir.GetNext())
        {
            if (dir.CurrentIsDir() || !name.EndsWith(".json"))
                continue;

            var fullPath = $"{directory.TrimEnd('/')}/{name}";
            using var file = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read);
            if (file is null)
            {
                GD.PushWarning($"[ContentLoader] Could not open {fullPath}");
                continue;
            }

            results.Add(file.GetAsText());
        }

        dir.ListDirEnd();
        return results;
    }
}
