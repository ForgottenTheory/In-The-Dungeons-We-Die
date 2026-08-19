using Dungeons.Content;
using Dungeons.Professions;

namespace Dungeons.Tests;

/// <summary>Locates the repository's shipped content directory from the test output folder.</summary>
internal static class TestPaths
{
    /// <summary>
    /// The shipped mastery ladder. Tests that exercise mastery read the <b>real</b> numbers
    /// rather than a fixture's guess at them — Phase 8 moved those magnitudes out of
    /// <c>ProfessionTuning</c> and into <c>game/data/mastery/</c>, and a test asserting against
    /// a private copy would stop noticing when the ladder is retuned.
    /// </summary>
    public static MasteryBenefits ShippedMasteryLadder() =>
        new(LoadStore<MasteryBenefitDefinition>("mastery"));

    /// <summary>Loads a shipped content subfolder into a store (array or single-object files),
    /// the one place test suites share instead of each re-implementing a loader.</summary>
    public static DataStore<T> LoadStore<T>(string subfolder) where T : IDefinition
    {
        var store = new DataStore<T>();
        store.LoadDocuments(
            Directory.GetFiles(Path.Combine(DataDir, subfolder), "*.json").Select(File.ReadAllText));
        return store;
    }

    public static string DataDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "game", "data");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate game/data from the test output directory.");
        }
    }
}
