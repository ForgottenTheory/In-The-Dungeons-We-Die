namespace Dungeons.Tests;

/// <summary>Locates the repository's shipped content directory from the test output folder.</summary>
internal static class TestPaths
{
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
