using System.Runtime.CompilerServices;
using RoslynQuery.Core.FileSystem;
using RoslynQuery.Core.Queries;
using RoslynQuery.Core.Utilities;
using RoslynQuery.Core.Workspace;

namespace RoslynQuery.Daemon;

/// <summary>
/// Orchestrates the daemon components: solution loading, file watching, and IPC server.
/// </summary>
public sealed class DaemonHost : IAsyncDisposable
{
    private readonly string _solutionPath;
    private readonly SolutionManager _solutionManager;
    private readonly QueryExecutor _queryExecutor;
    private readonly DebouncedFileWatcher _fileWatcher;
    private readonly IpcServer _ipcServer;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    private DaemonHost(
        string solutionPath,
        SolutionManager solutionManager,
        QueryExecutor queryExecutor,
        DebouncedFileWatcher fileWatcher,
        IpcServer ipcServer)
    {
        _solutionPath = solutionPath;
        _solutionManager = solutionManager;
        _queryExecutor = queryExecutor;
        _fileWatcher = fileWatcher;
        _ipcServer = ipcServer;
    }

    /// <summary>
    /// Create and start a daemon for the specified solution.
    /// This method must be called AFTER MSBuildLocator initialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<DaemonHost> CreateAndStartAsync(
        string solutionPath, TimeSpan? idleTimeout = null, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathResolver.NormalizePath(solutionPath);
        var solutionRoot = PathResolver.GetSolutionRoot(normalizedPath);
        var socketPath = PathResolver.GetSocketPath(normalizedPath);

        // Create components
        var solutionManager = new SolutionManager(normalizedPath);
        var queryExecutor = new QueryExecutor(solutionManager);
        var fileWatcher = new DebouncedFileWatcher(solutionRoot);
        var ipcServer = new IpcServer(socketPath, queryExecutor, idleTimeout);

        var host = new DaemonHost(normalizedPath, solutionManager, queryExecutor, fileWatcher, ipcServer);

        try
        {
            // Load solution
            Console.Error.WriteLine($"Loading solution: {normalizedPath}");
            await solutionManager.LoadSolutionAsync(cancellationToken);
            Console.Error.WriteLine("Solution loaded successfully");

            // Setup file watcher
            host._fileWatcher.FilesChanged += host.OnFilesChanged;
            host._fileWatcher.Start();
            Console.Error.WriteLine("File watcher started");

            // Start IPC server
            host._ipcServer.Start();
            Console.Error.WriteLine($"IPC server listening on: {socketPath}");

            if (idleTimeout.HasValue)
            {
                Console.Error.WriteLine($"Idle timeout: {idleTimeout.Value.TotalMinutes:F0} minutes");
            }
            else
            {
                Console.Error.WriteLine("Idle timeout: disabled");
            }

            // Write PID file
            var pidPath = PathResolver.GetPidFilePath(normalizedPath);
            await File.WriteAllTextAsync(pidPath, Environment.ProcessId.ToString(), cancellationToken);

            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Wait for the daemon to be signaled to shutdown.
    /// </summary>
    public async Task WaitForShutdownAsync()
    {
        // Setup signal handlers
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            _cts.Cancel();
        };

        try
        {
            await _ipcServer.WaitForShutdownAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    private async void OnFilesChanged(object? sender, FileChangesEventArgs e)
    {
        _ipcServer.RecordActivity();
        try
        {
            if (e.RequiresFullReload)
            {
                Console.Error.WriteLine("Project/solution file changed, reloading solution...");
                await _solutionManager.LoadSolutionAsync(_cts.Token);
                Console.Error.WriteLine("Solution reloaded");
            }
            else
            {
                // Incremental update for .cs files
                foreach (var change in e.Changes.Where(c => c.IsSourceFile))
                {
                    if (change.ChangeType == FileChangeType.Deleted)
                    {
                        // File deleted - will be handled on next full reload
                        continue;
                    }

                    await _solutionManager.UpdateDocumentFromDiskAsync(change.FilePath, _cts.Token);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error handling file changes: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _cts.CancelAsync();

        _fileWatcher.FilesChanged -= OnFilesChanged;
        _fileWatcher.Stop();
        _fileWatcher.Dispose();

        await _ipcServer.DisposeAsync();

        _solutionManager.Dispose();

        // Clean up PID file
        var pidPath = PathResolver.GetPidFilePath(_solutionPath);
        if (File.Exists(pidPath))
        {
            try
            {
                File.Delete(pidPath);
            }
            catch
            {
                // Ignore
            }
        }

        _cts.Dispose();
    }
}
