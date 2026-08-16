using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Professions;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Combat;

/// <summary>
/// The M2′ acquisition seam: technique items teach moves into a learned list, the list joins
/// moveset composition as its own grant source, and it survives a save round-trip in learn
/// order (order feeds composition, and composition must be deterministic).
/// </summary>
public class TechniqueTests
{
    // --- LearnedMoves ---------------------------------------------------------

    [Fact]
    public void LearningIsOncePerMove_AndPreservesLearnOrder()
    {
        var learned = new LearnedMoves();

        Assert.True(learned.Learn("move.fireball"));
        Assert.True(learned.Learn("move.heavy_strike"));
        Assert.False(learned.Learn("move.fireball")); // already known — the item must not be consumed
        Assert.False(learned.Learn(""));

        Assert.Equal(new[] { "move.fireball", "move.heavy_strike" }, learned.All);
        Assert.True(learned.Knows("move.fireball"));
        Assert.False(learned.Knows("move.shield_bash"));
    }

    [Fact]
    public void RestoreReplacesTheListAndDropsDuplicates()
    {
        var learned = new LearnedMoves();
        learned.Learn("move.old");

        learned.Restore(new[] { "move.a", "move.b", "move.a" });

        Assert.Equal(new[] { "move.a", "move.b" }, learned.All);
        Assert.False(learned.Knows("move.old"));
    }

    // --- Moveset composition --------------------------------------------------

    [Fact]
    public void ALearnedMoveJoinsTheMovesetWithLearnedProvenance()
    {
        var moves = new DataStore<MoveDefinition>();
        moves.Add(new MoveDefinition
        {
            Id = "move.fireball", Name = "Fireball",
            Tags = new[] { "action:attack", "delivery:projectile" },
            Packets = new[] { new Packet(DamageType.Magic, 40) },
        });

        var learned = new LearnedMoves();
        learned.Learn("move.fireball");

        var grants = learned.All
            .Select(id => new MoveGrant(new MoveGrantSpec { Id = id }, "learned"))
            .ToList();
        var conflicts = new MovesetBuilder(moves).Build(grants, Array.Empty<MoveModifierGrant>(), out var moveset);

        Assert.Empty(conflicts);
        var fireball = Assert.Single(moveset);
        Assert.Equal("move.fireball", fireball.Id);
        Assert.Contains(fireball.Provenance, p => p.Contains("learned"));
    }

    // --- Persistence ----------------------------------------------------------

    [Fact]
    public void LearnedMovesSurviveASaveRoundTrip_InLearnOrder()
    {
        var stash = new Inventory();
        var professions = new ProfessionSystem(new DataStore<ProfessionActionDefinition>(), stash, new SeededRandom(1));
        var learned = new LearnedMoves();
        learned.Learn("move.fireball");
        learned.Learn("move.heavy_strike");

        var save = SaveMapper.Capture(
            null, stash, professions, new DiscoverySystem(), new Dictionary<string, int>(),
            savedAtTick: 1, learnedMoves: learned);
        var loaded = new SaveSerializer().Deserialize(new SaveSerializer().Serialize(save));

        var freshStash = new Inventory();
        var fresh = new LearnedMoves();
        SaveMapper.Apply(
            loaded, freshStash,
            new ProfessionSystem(new DataStore<ProfessionActionDefinition>(), freshStash, new SeededRandom(1)),
            new DiscoverySystem(), new Dictionary<string, int>(), learnedMoves: fresh);

        Assert.Equal(new[] { "move.fireball", "move.heavy_strike" }, fresh.All);
    }

    /// <summary>An older save has no LearnedMoves field; it must load as an empty list.</summary>
    [Fact]
    public void AnOlderSaveLoadsWithNoLearnedMoves()
    {
        var learned = new LearnedMoves();
        learned.Learn("move.stale");

        var stash = new Inventory();
        SaveMapper.Apply(
            new SaveData(), stash,
            new ProfessionSystem(new DataStore<ProfessionActionDefinition>(), stash, new SeededRandom(1)),
            new DiscoverySystem(), new Dictionary<string, int>(), learnedMoves: learned);

        Assert.Empty(learned.All);
    }
}
