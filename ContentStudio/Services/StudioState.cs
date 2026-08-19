using ContentStudio.Infrastructure;

namespace ContentStudio.Services;

/// <summary>
/// The composition root's living state: one workspace, its derived indexes, and the plumbing
/// that keeps them fresh (debounced revalidation after every change, SSE fan-out to the UI).
/// </summary>
public sealed class StudioState : IDisposable
{
    public StudioSettings Settings { get; }
    public ContentWorkspace Workspace { get; } = new();
    public ReferenceIndexService References { get; } = new();
    public ValidationService Validation { get; } = new();
    public BackupService Backups { get; }
    public SseHub Sse { get; } = new();

    private readonly FileWatcherService _watcher;
    private readonly Timer _revalidateTimer;
    private static readonly TimeSpan RevalidateDelay = TimeSpan.FromMilliseconds(250);

    private volatile ValidationState _currentValidation = ValidationState.Empty;
    private int _revision;

    public ValidationState CurrentValidation => _currentValidation;

    /// <summary>Bumped on every workspace change; lets the client discard stale fetches.</summary>
    public int Revision => _revision;

    public StudioState()
    {
        Settings = StudioSettings.Load();
        Backups = new BackupService(Settings);
        _watcher = new FileWatcherService(Workspace);
        _revalidateTimer = new Timer(_ => RecomputeDerivedState(), null, Timeout.Infinite, Timeout.Infinite);

        Workspace.Changed += () =>
        {
            Interlocked.Increment(ref _revision);
            _revalidateTimer.Change(RevalidateDelay, Timeout.InfiniteTimeSpan);
        };
        Workspace.FileStateChanged += (file, reason) =>
            Sse.Broadcast("file", new { path = file.RelativePath, reason, dirty = file.IsDirty, conflict = file.HasDiskConflict });
    }

    public bool TryOpenProjectFromSettingsOrDiscovery()
    {
        var candidate = ProjectLocator.IsValidProjectRoot(Settings.ProjectRoot)
            ? Settings.ProjectRoot!
            : ProjectLocator.DiscoverProjectRootNearApplication();
        if (candidate is null)
            return false;
        OpenProject(candidate);
        return true;
    }

    public void OpenProject(string projectRoot)
    {
        var fullPath = Path.GetFullPath(projectRoot);
        if (!ProjectLocator.IsValidProjectRoot(fullPath))
            throw new InvalidOperationException($"'{fullPath}' has no game/data directory — not a game project root.");

        Workspace.LoadProject(fullPath);
        _watcher.WatchProject(Workspace.DataDirectory);
        Settings.ProjectRoot = fullPath;
        Settings.Save();
        RecomputeDerivedState();
    }

    private void RecomputeDerivedState()
    {
        if (!Workspace.IsLoaded)
            return;
        lock (Workspace.MutationLock)
        {
            References.Rebuild(Workspace);
            _currentValidation = Validation.Recompute(Workspace, References);
        }
        Sse.Broadcast("validation", new
        {
            revision = _revision,
            errors = _currentValidation.ErrorCount,
            warnings = _currentValidation.WarningCount,
        });
    }

    /// <summary>Runs validation synchronously — used right after mutations so the API response
    /// already carries fresh problem counts instead of racing the debounce timer.</summary>
    public ValidationState RevalidateNow()
    {
        RecomputeDerivedState();
        return _currentValidation;
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _revalidateTimer.Dispose();
    }
}
