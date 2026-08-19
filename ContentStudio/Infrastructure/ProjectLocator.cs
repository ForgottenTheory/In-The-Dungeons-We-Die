namespace ContentStudio.Infrastructure;

/// <summary>
/// Finds and verifies game-project roots. A valid project root is a directory containing
/// <c>game/data</c> — the authored-content tree Content Studio manages.
/// </summary>
public static class ProjectLocator
{
    public const string DataDirectoryRelativePath = "game/data";

    public static bool IsValidProjectRoot(string? candidateRoot) =>
        !string.IsNullOrWhiteSpace(candidateRoot) &&
        Directory.Exists(Path.Combine(candidateRoot, "game", "data"));

    public static string DataDirectoryOf(string projectRoot) => Path.Combine(projectRoot, "game", "data");

    /// <summary>
    /// Walks up from the application's own location looking for a directory containing
    /// <c>game/data</c>, so launching the tool from inside the repo needs no configuration.
    /// </summary>
    public static string? DiscoverProjectRootNearApplication()
    {
        var candidates = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
        foreach (var startingPoint in candidates)
        {
            for (var directory = new DirectoryInfo(startingPoint); directory is not null; directory = directory.Parent)
            {
                if (IsValidProjectRoot(directory.FullName))
                    return directory.FullName;
            }
        }
        return null;
    }
}
