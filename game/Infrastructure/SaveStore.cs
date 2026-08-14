using Dungeons.Persistence;
using Godot;

namespace Dungeons.Game.Infrastructure;

/// <summary>
/// The Godot-side save file store. It owns <c>user://</c> access and delegates all
/// (de)serialization to the engine-independent <see cref="SaveSerializer"/>.
/// Deliberately minimal for Milestone 1 — a single slot, no migration.
/// </summary>
public sealed class SaveStore
{
    private const string SavePath = "user://save.json";
    private readonly SaveSerializer _serializer = new();

    public void Save(SaveData data)
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushError($"[SaveStore] Could not open {SavePath} for writing.");
            return;
        }

        file.StoreString(_serializer.Serialize(data));
    }

    public SaveData? Load()
    {
        if (!FileAccess.FileExists(SavePath))
            return null;

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushError($"[SaveStore] Could not open {SavePath} for reading.");
            return null;
        }

        return _serializer.Deserialize(file.GetAsText());
    }
}
