using Dungeons.Characters;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Professions;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Persistence;

/// <summary>
/// Save schema v11 — the character's own XP.
///
/// <para>Same forward-compatible rule as v7, v8 and v10: a v10 save loads at 0, which is
/// character level 1 — exactly where every existing character already is, because until Phase 8
/// nothing awarded it. No migration step.</para>
/// </summary>
public class CharacterXpSaveV11Tests
{
    private static (Inventory Stash, ProfessionSystem Professions, DiscoverySystem Discoveries) FreshSession()
    {
        var stash = new Inventory();
        return (stash, new ProfessionSystem(new DataStore<ProfessionActionDefinition>(), stash, new SeededRandom(1)),
            new DiscoverySystem());
    }

    private static SaveData RoundTrip(SaveData save)
    {
        var serializer = new SaveSerializer();
        return serializer.Deserialize(serializer.Serialize(save))!;
    }

    [Fact]
    public void ANewSaveIsWrittenAtSchemaElevenOrLater()
    {
        // The exact pin lives with the newest version's tests (v12 at the time of writing);
        // this one only holds that v11's fields are part of the written schema.
        Assert.True(SaveData.CurrentSchemaVersion >= 11);
    }

    [Fact]
    public void CharacterXpSurvivesASaveAndLoad()
    {
        var (stash, professions, discoveries) = FreshSession();
        var progress = new CharacterProgress();
        progress.AddXp(CharacterLeveling.XpForLevel(6) + 17);

        var save = RoundTrip(SaveMapper.Capture(
            null, stash, professions, discoveries, new Dictionary<string, int>(),
            savedAtTick: 5, characterProgress: progress));

        var restored = new CharacterProgress();
        var (freshStash, freshProfessions, freshDiscoveries) = FreshSession();
        SaveMapper.Apply(save, freshStash, freshProfessions, freshDiscoveries,
            new Dictionary<string, int>(), characterProgress: restored);

        Assert.Equal(progress.Xp, restored.Xp);
        Assert.Equal(6, restored.Level);
    }

    /// <summary>Levels survive death, like every other persistent track (GDD §13.1) — which in
    /// save terms means the XP total is written whatever happened on the last run.</summary>
    [Fact]
    public void AV10SaveLoadsAtLevelOneRatherThanFailing()
    {
        const string legacyJson = """
        {
          "schemaVersion": 10,
          "savedAtTick": 900,
          "stash": [ { "itemId": "material.oak_log", "quantity": 3 } ],
          "professions": [],
          "realmKnowledge": {},
          "discoveries": []
        }
        """;

        var save = new SaveSerializer().Deserialize(legacyJson)!;
        Assert.Equal(0, save.CharacterXp);

        var progress = new CharacterProgress();
        progress.AddXp(5000); // whatever was in memory must be replaced by what the save says

        var (stash, professions, discoveries) = FreshSession();
        SaveMapper.Apply(save, stash, professions, discoveries, new Dictionary<string, int>(), characterProgress: progress);

        Assert.Equal(0, progress.Xp);
        Assert.Equal(1, progress.Level);
        Assert.Equal(0, stash.GetQuantity("material.oak_log")); // D54: pre-v14 items reset
    }

    [Fact]
    public void CapturingWithoutCharacterProgressWritesZero()
    {
        var (stash, professions, discoveries) = FreshSession();

        var save = SaveMapper.Capture(null, stash, professions, discoveries,
            new Dictionary<string, int>(), savedAtTick: 0);

        Assert.Equal(0, save.CharacterXp);
    }

    /// <summary>Mastery points are the mastery track's whole state and the save key has not moved
    /// — Phase 8 derives the level from the same int v1 wrote.</summary>
    [Fact]
    public void MasteryPointsStillRoundTripUnderTheirOriginalKey()
    {
        var (stash, professions, discoveries) = FreshSession();
        professions.GetProgress("profession.forestry").AddMastery("action.chop_oak", 42);

        var save = RoundTrip(SaveMapper.Capture(
            null, stash, professions, discoveries, new Dictionary<string, int>(), savedAtTick: 0));

        var (freshStash, freshProfessions, freshDiscoveries) = FreshSession();
        SaveMapper.Apply(save, freshStash, freshProfessions, freshDiscoveries, new Dictionary<string, int>());

        Assert.Equal(42, freshProfessions.GetProgress("profession.forestry").GetMastery("action.chop_oak"));
        Assert.Equal(42, MasteryLeveling.LevelFor(42));
    }
}
