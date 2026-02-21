namespace RoslynQuery.Core.FileSystem;

/// <summary>
/// File system watcher with debouncing for batching rapid changes.
/// </summary>
public sealed class DebouncedFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly TimeSpan _debounceDelay;
    private readonly Dictionary<string, FileChangeEvent> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private Timer? _debounceTimer;
    private bool _disposed;

    /// <summary>
    /// Event raised when file changes are ready to be processed (after debounce).
    /// </summary>
    public event EventHandler<FileChangesEventArgs>? FilesChanged;

    /// <summary>
    /// Creates a new file watcher.
    /// </summary>
    /// <param name="watchPath">Directory to watch.</param>
    /// <param name="debounceDelay">Delay before processing changes (default 300ms).</param>
    public DebouncedFileWatcher(string watchPath, TimeSpan? debounceDelay = null)
    {
        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(300);

        _watcher = new FileSystemWatcher(watchPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = true,
            EnableRaisingEvents = false
        };

        // Watch relevant file types
        _watcher.Filters.Add("*.cs");
        _watcher.Filters.Add("*.csproj");
        _watcher.Filters.Add("*.sln");

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileCreated;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Error += OnError;
    }

    /// <summary>
    /// Start watching for file changes.
    /// </summary>
    public void Start()
    {
        ThrowIfDisposed();
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Stop watching for file changes.
    /// </summary>
    public void Stop()
    {
        ThrowIfDisposed();
        _watcher.EnableRaisingEvents = false;

        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
            _pendingChanges.Clear();
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        QueueChange(new FileChangeEvent
        {
            ChangeType = FileChangeType.Changed,
            FilePath = e.FullPath
        });
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        QueueChange(new FileChangeEvent
        {
            ChangeType = FileChangeType.Created,
            FilePath = e.FullPath
        });
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        QueueChange(new FileChangeEvent
        {
            ChangeType = FileChangeType.Deleted,
            FilePath = e.FullPath
        });
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        QueueChange(new FileChangeEvent
        {
            ChangeType = FileChangeType.Renamed,
            FilePath = e.FullPath,
            OldFilePath = e.OldFullPath
        });
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        // Log error - buffer overflow or other watcher issue
        // TODO: Add proper logging
    }

    private void QueueChange(FileChangeEvent change)
    {
        // If it's a project/solution file, process immediately without debouncing
        if (change.RequiresFullReload)
        {
            RaiseFilesChanged([change]);
            return;
        }

        lock (_lock)
        {
            // Coalesce changes for the same file
            _pendingChanges[change.FilePath] = change;

            // Reset debounce timer
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(OnDebounceElapsed, null, _debounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        FileChangeEvent[] changes;

        lock (_lock)
        {
            if (_pendingChanges.Count == 0)
            {
                return;
            }

            changes = [.. _pendingChanges.Values];
            _pendingChanges.Clear();
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        RaiseFilesChanged(changes);
    }

    private void RaiseFilesChanged(FileChangeEvent[] changes)
    {
        try
        {
            FilesChanged?.Invoke(this, new FileChangesEventArgs(changes));
        }
        catch
        {
            // Don't let subscriber exceptions crash the watcher
            // TODO: Add proper logging
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DebouncedFileWatcher));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.Dispose();
        _debounceTimer?.Dispose();
    }
}

/// <summary>
/// Event args for file changes.
/// </summary>
public class FileChangesEventArgs : EventArgs
{
    public FileChangesEventArgs(IReadOnlyList<FileChangeEvent> changes)
    {
        Changes = changes;
    }

    /// <summary>
    /// The file changes.
    /// </summary>
    public IReadOnlyList<FileChangeEvent> Changes { get; }

    /// <summary>
    /// Whether any change requires a full solution reload.
    /// </summary>
    public bool RequiresFullReload => Changes.Any(c => c.RequiresFullReload);
}
