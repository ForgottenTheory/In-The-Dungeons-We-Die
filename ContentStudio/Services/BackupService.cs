using System.Security.Cryptography;
using System.Text;
using ContentStudio.Infrastructure;

namespace ContentStudio.Services;

/// <summary>
/// Timestamped copies of content files, taken automatically before every save and kept under
/// %LOCALAPPDATA% (never inside the game repo). Old versions are pruned per file.
/// </summary>
public sealed class BackupService
{
    private readonly StudioSettings _settings;

    public BackupService(StudioSettings settings) => _settings = settings;

    public sealed record BackupVersion(string FileName, string FullPath, DateTime TakenAtUtc, long SizeBytes);

    /// <summary>Copies the file's current on-disk bytes into the backup store.</summary>
    public void BackupCurrentDiskVersion(string absolutePath, string projectRoot)
    {
        var backupDirectory = BackupDirectoryFor(absolutePath, projectRoot);
        Directory.CreateDirectory(backupDirectory);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var backupPath = Path.Combine(backupDirectory, $"{timestamp}.json");
        File.Copy(absolutePath, backupPath, overwrite: true);
        Prune(backupDirectory);
    }

    public List<BackupVersion> ListVersions(string absolutePath, string projectRoot)
    {
        var backupDirectory = BackupDirectoryFor(absolutePath, projectRoot);
        if (!Directory.Exists(backupDirectory))
            return new List<BackupVersion>();

        return Directory.EnumerateFiles(backupDirectory, "*.json")
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.Name)
            .Select(info => new BackupVersion(info.Name, info.FullName, info.LastWriteTimeUtc, info.Length))
            .ToList();
    }

    public string ReadVersion(string absolutePath, string projectRoot, string backupFileName)
    {
        var backupDirectory = BackupDirectoryFor(absolutePath, projectRoot);
        var backupPath = Path.Combine(backupDirectory, Path.GetFileName(backupFileName));
        return File.ReadAllText(backupPath);
    }

    private void Prune(string backupDirectory)
    {
        var versions = Directory.EnumerateFiles(backupDirectory, "*.json")
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToList();
        foreach (var stale in versions.Skip(Math.Max(1, _settings.BackupVersionsPerFile)))
        {
            try { File.Delete(stale); } catch (IOException) { /* pruning is best-effort */ }
        }
    }

    /// <summary>Backups are grouped per project (hashed root path) then mirror the data tree.</summary>
    private static string BackupDirectoryFor(string absolutePath, string projectRoot)
    {
        var projectKey = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(projectRoot.ToLowerInvariant())))[..12];
        var projectName = new DirectoryInfo(projectRoot).Name;
        var relative = Path.GetRelativePath(projectRoot, absolutePath).Replace('\\', '/').Replace('/', '_');
        return Path.Combine(StudioSettings.BackupRootDirectory, $"{projectName}-{projectKey}", relative);
    }
}
