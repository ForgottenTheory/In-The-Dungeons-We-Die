using Dungeons.Characters.Composition;
using Dungeons.Items;
using Dungeons.Persistence;
using Xunit;

namespace Dungeons.Tests.Persistence;

public class SaveSerializerTests
{
    private static SaveData Sample() => new()
    {
        SavedAtTick = 4242,
        Build = new CharacterBuild("species.undead", "class.bastion", "prefix.frenzied", "suffix.the_last_laugh"),
        Stash = new List<ItemStack> { new("material.oak_log", 7), new("material.iron_ingot", 2) },
        Professions = new List<ProfessionSave>
        {
            new() { ProfessionId = "profession.forestry", Xp = 350, Mastery = new() { ["action.chop_oak"] = 12 } },
        },
        RealmKnowledge = new() { ["realm.dark_forest"] = 9 },
        Discoveries = new List<string> { "discovery.barkbound_iron" },
    };

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var serializer = new SaveSerializer();
        var restored = serializer.Deserialize(serializer.Serialize(Sample()));

        Assert.Equal(SaveData.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Equal(4242, restored.SavedAtTick);
        Assert.Equal("species.undead", restored.Build!.SpeciesId);
        Assert.Equal("suffix.the_last_laugh", restored.Build.SuffixId);
        Assert.Equal(7, restored.Stash.First(s => s.ItemId == "material.oak_log").Quantity);
        Assert.Equal(350, restored.Professions.Single().Xp);
        Assert.Equal(12, restored.Professions.Single().Mastery["action.chop_oak"]);
        Assert.Equal(9, restored.RealmKnowledge["realm.dark_forest"]);
        Assert.Contains("discovery.barkbound_iron", restored.Discoveries);
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
        using var stream = new MemoryStream();
        serializer.Serialize(Sample(), stream);
        stream.Position = 0;
        var restored = serializer.Deserialize(stream);
        Assert.Equal(4242, restored.SavedAtTick);
    }

    [Fact]
    public void Deserialize_RejectsEmptyJson()
    {
        Assert.Throws<ArgumentException>(() => new SaveSerializer().Deserialize(""));
    }
}
