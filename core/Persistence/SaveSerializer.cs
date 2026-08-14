using System.Text.Json;

namespace Dungeons.Persistence;

/// <summary>
/// Converts <see cref="SaveData"/> to and from JSON. Engine-independent and free
/// of file IO — the Godot layer reads/writes <c>user://</c> and passes the text or
/// stream in. This keeps save/load deterministic and unit-testable.
/// </summary>
public sealed class SaveSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string Serialize(SaveData save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return JsonSerializer.Serialize(save, Options);
    }

    public void Serialize(SaveData save, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(destination);
        JsonSerializer.Serialize(destination, save, Options);
    }

    public SaveData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Save JSON is null or empty.", nameof(json));
        return JsonSerializer.Deserialize<SaveData>(json, Options)
            ?? throw new JsonException("Save JSON deserialized to null.");
    }

    public SaveData Deserialize(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return JsonSerializer.Deserialize<SaveData>(source, Options)
            ?? throw new JsonException("Save JSON deserialized to null.");
    }
}
