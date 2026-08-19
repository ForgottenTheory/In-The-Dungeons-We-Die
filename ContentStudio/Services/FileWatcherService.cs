namespace ContentStudio.Services;

/// <summary>
/// Watches the project's data tree for edits made outside Content Studio (Claude, Visual
/// Studio, git operations…) and forwards debounced per-file notifications to the workspace.
/// </summary>
public sealed class FileWatcherService : IDisposable
{
    private readonly ContentWorkspace _workspace;
    private FileSystemWatcher? _watcher;
    private readonly object _pendingLock = new();
    private readonly Dictionary<string, DateTime> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _drainTimer;
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(300);

    public FileWatcherService(ContentWorkspace workspace)
    {
        _workspace = workspace;
        _drainTimer = new Timer(_ => DrainPending(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void WatchProject(string dataDirectory)
    {
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(dataDirectory, "*.json")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += (_, args) => QueueChange(args.FullPath);
        _watcher.Created += (_, args) => QueueChange(args.FullPath);
        _watcher.Deleted += (_, args) => QueueChange(args.FullPath);
        _watcher.Renamed += (_, args) => { QueueChange(args.OldFullPath); QueueChange(args.FullPath); };
        _watcher.EnableRaisingEvents = true;
    }

    private void QueueChange(string absolutePath)
    {
        if (absolutePath.EndsWith(".contentstudio-tmp", StringComparison.OrdinalIgnoreCase))
            return;
        lock (_pendingLock)
        {
            _pendingPaths[absolutePath] = DateTime.UtcNow;
            _drainTimer.Change(DebounceWindow, Timeout.InfiniteTimeSpan);
        }
    }

    private void DrainPending()
    {
        List<string> paths;
        lock (_pendingLock)
        {
            paths = _pendingPaths.Keys.ToList();
            _pendingPaths.Clear();
        }
        foreach (var path in paths)
            _workspace.OnExternalFileChange(path);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _drainTimer.Dispose();
    }
}
