using Dungeons.Content;
using Xunit;

namespace Dungeons.Tests.Content;

public class DataStoreTests
{
    private const string OakBarkJson = """
        {
          "id": "material.oak_bark",
          "name": "Oak Bark",
          "tags": ["plant", "bark", "oak"]
        }
        """;

    private const string OakLogJson = """
        {
          "id": "material.oak_log",
          "name": "Oak Log",
          "tags": ["wood", "oak"]
        }
        """;

    [Fact]
    public void LoadOne_ParsesAndRegisters()
    {
        var store = new DataStore<MaterialDefinition>();
        var material = store.LoadOne(OakBarkJson);

        Assert.Equal("material.oak_bark", material.Id);
        Assert.Equal("Oak Bark", material.Name);
        Assert.Equal(new[] { "plant", "bark", "oak" }, material.Tags);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void GetById_ReturnsDefinition_And_MissingThrows()
    {
        var store = new DataStore<MaterialDefinition>();
        store.LoadOne(OakBarkJson);

        Assert.Equal("Oak Bark", store.GetById("material.oak_bark").Name);
        Assert.Throws<KeyNotFoundException>(() => store.GetById("material.missing"));
    }

    [Fact]
    public void TryGetById_ReportsPresence()
    {
        var store = new DataStore<MaterialDefinition>();
        store.LoadOne(OakLogJson);

        Assert.True(store.TryGetById("material.oak_log", out var found));
        Assert.Equal("Oak Log", found.Name);
        Assert.False(store.TryGetById("material.nope", out _));
    }

    [Fact]
    public void DuplicateId_FailsLoudly()
    {
        var store = new DataStore<MaterialDefinition>();
        store.LoadOne(OakBarkJson);

        var ex = Assert.Throws<DuplicateDefinitionException>(() => store.LoadOne(OakBarkJson));
        Assert.Equal("material.oak_bark", ex.Id);
        Assert.Equal(typeof(MaterialDefinition), ex.DefinitionType);
    }

    [Fact]
    public void EmptyId_FailsLoudly()
    {
        var store = new DataStore<MaterialDefinition>();
        Assert.Throws<ArgumentException>(() => store.LoadOne("""{ "id": "", "name": "Nameless" }"""));
    }

    [Fact]
    public void GetAll_ReturnsEveryDefinition()
    {
        var store = new DataStore<MaterialDefinition>();
        store.LoadOne(OakBarkJson);
        store.LoadOne(OakLogJson);

        var ids = store.GetAll().Select(m => m.Id).OrderBy(id => id).ToArray();
        Assert.Equal(new[] { "material.oak_bark", "material.oak_log" }, ids);
    }

    [Fact]
    public void Reload_ReplacesContents()
    {
        var store = new DataStore<MaterialDefinition>();
        store.LoadOne(OakBarkJson);

        store.Reload(new[] { OakLogJson });

        Assert.Equal(1, store.Count);
        Assert.False(store.Contains("material.oak_bark"));
        Assert.True(store.Contains("material.oak_log"));
    }

    [Fact]
    public void LoadMany_ParsesJsonArray()
    {
        var store = new DataStore<MaterialDefinition>();
        var json = $"[{OakBarkJson},{OakLogJson}]";

        var loaded = store.LoadMany(json);

        Assert.Equal(2, loaded.Count);
        Assert.Equal(2, store.Count);
    }

    // --- The unknown-field fence -------------------------------------------------------------
    //
    // Two shipped records were silently wrong for a whole milestone because a misspelled JSON
    // key matched no property and was ignored: movemod.emberbrand authored "moveId" where
    // MoveMatch declares "move_id" (so it modified EVERY move), and affix.reflection authored
    // "scalesWith" where EffectSpec declares "scales_with" (so it dealt its flat roll instead
    // of a fraction). These tests hold the fence that turned that bug class into a load error.

    [Fact]
    public void UnknownTopLevelKey_FailsToLoad()
    {
        var store = new DataStore<MaterialDefinition>();
        const string misspelled = """
            {
              "id": "material.oak_bark",
              "nmae": "Oak Bark"
            }
            """;

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => store.LoadOne(misspelled));
    }

    [Fact]
    public void UnknownNestedKey_FailsToLoad_TheEmberbrandBug()
    {
        var store = new DataStore<Dungeons.Combat.MoveModifierDefinition>();
        const string emberbrandAsOriginallyAuthored = """
            {
              "id": "movemod.emberbrand",
              "name": "Emberbrand",
              "match": { "moveId": "move.heavy_strike" },
              "ops": []
            }
            """;

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => store.LoadOne(emberbrandAsOriginallyAuthored));
    }
}
