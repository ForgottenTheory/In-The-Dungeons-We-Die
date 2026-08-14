using System.Text;
using Dungeons.Persistence;
using Xunit;

namespace Dungeons.Tests.Persistence;

public class SaveSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesVersionAndValues()
    {
        var serializer = new SaveSerializer();
        var original = new SaveData { SchemaVersion = 1, SavedAtTick = 4242, Coins = 137 };

        var json = serializer.Serialize(original);
        var restored = serializer.Deserialize(json);

        Assert.Equal(original.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(original.SavedAtTick, restored.SavedAtTick);
        Assert.Equal(original.Coins, restored.Coins);
    }

    [Fact]
    public void NewSave_UsesCurrentSchemaVersion()
    {
        var restored = new SaveSerializer().Deserialize(new SaveSerializer().Serialize(new SaveData()));
        Assert.Equal(SaveData.CurrentSchemaVersion, restored.SchemaVersion);
    }

    [Fact]
    public void StreamRoundTrip_Works()
    {
        var serializer = new SaveSerializer();
        var original = new SaveData { SavedAtTick = 9, Coins = 5 };

        using var stream = new MemoryStream();
        serializer.Serialize(original, stream);
        stream.Position = 0;
        var restored = serializer.Deserialize(stream);

        Assert.Equal(9, restored.SavedAtTick);
        Assert.Equal(5, restored.Coins);
    }

    [Fact]
    public void Deserialize_RejectsEmptyJson()
    {
        var serializer = new SaveSerializer();
        Assert.Throws<ArgumentException>(() => serializer.Deserialize(""));
    }

    [Fact]
    public void SerializedPayload_IsPlainJson()
    {
        var json = new SaveSerializer().Serialize(new SaveData { Coins = 1 });
        Assert.Contains("schemaVersion", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coins", json, StringComparison.OrdinalIgnoreCase);
    }
}
