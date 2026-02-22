using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using RoslynQuery.Core.Utilities;

namespace RoslynQuery.Core.Workspace;

/// <summary>
/// Manages the loaded solution and provides incremental updates.
/// </summary>
public sealed class SolutionManager : ISolutionProvider, IDisposable
{
    private readonly string _solutionPath;
    private readonly string _solutionRoot;
    private MSBuildWorkspace? _workspace;
    private Solution? _solution;
    private readonly ReaderWriterLockSlim _lock = new();
    private bool _disposed;

    public SolutionManager(string solutionPath)
    {
        _solutionPath = PathResolver.NormalizePath(solutionPath);
        _solutionRoot = PathResolver.GetSolutionRoot(_solutionPath);
    }

    /// <summary>
    /// Gets the solution root directory.
    /// </summary>
    public string SolutionRoot => _solutionRoot;

    /// <summary>
    /// Gets the solution file path.
    /// </summary>
    public string SolutionPath => _solutionPath;

    /// <summary>
    /// Gets whether the solution is loaded.
    /// </summary>
    public bool IsLoaded => _solution != null;

    /// <summary>
    /// Load the solution from disk.
    /// </summary>
    public async Task LoadSolutionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Ensure MSBuild is initialized before creating workspace
        MsBuildInitializer.EnsureInitialized();

        _lock.EnterWriteLock();
        try
        {
            // Dispose previous workspace if reloading
            _workspace?.Dispose();

            _workspace = MSBuildWorkspace.Create();
            _workspace.RegisterWorkspaceFailedHandler(e => OnWorkspaceFailed(e));

            _solution = await _workspace.OpenSolutionAsync(_solutionPath, cancellationToken: cancellationToken);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Get the current solution snapshot (thread-safe read).
    /// </summary>
    public Solution GetSolution()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            return _solution ?? throw new InvalidOperationException("Solution not loaded");
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Update a document with new content (incremental update, ~10-200ms).
    /// </summary>
    public async Task UpdateDocumentAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var absolutePath = PathResolver.ToAbsolutePath(filePath, _solutionRoot);

        _lock.EnterWriteLock();
        try
        {
            if (_solution == null)
            {
                throw new InvalidOperationException("Solution not loaded");
            }

            var documentId = FindDocumentId(absolutePath);
            if (documentId == null)
            {
                // Document not in solution - ignore
                return;
            }

            var sourceText = SourceText.From(content);
            _solution = _solution.WithDocumentText(documentId, sourceText);

            await Task.CompletedTask; // Placeholder for any async work
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Update a document from disk.
    /// </summary>
    public async Task UpdateDocumentFromDiskAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = PathResolver.ToAbsolutePath(filePath, _solutionRoot);

        if (!File.Exists(absolutePath))
        {
            return;
        }

        var content = await File.ReadAllTextAsync(absolutePath, cancellationToken);
        await UpdateDocumentAsync(absolutePath, content, cancellationToken);
    }

    /// <summary>
    /// Find a document by file path.
    /// </summary>
    public Document? FindDocument(string filePath)
    {
        ThrowIfDisposed();

        var absolutePath = PathResolver.ToAbsolutePath(filePath, _solutionRoot);

        _lock.EnterReadLock();
        try
        {
            if (_solution == null)
            {
                return null;
            }

            foreach (var project in _solution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    if (document.FilePath != null &&
                        string.Equals(PathResolver.NormalizePath(document.FilePath),
                                     PathResolver.NormalizePath(absolutePath),
                                     StringComparison.OrdinalIgnoreCase))
                    {
                        return document;
                    }
                }
            }

            return null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get all projects in the solution.
    /// </summary>
    public IEnumerable<Project> GetProjects()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            return _solution?.Projects.ToList() ?? [];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private DocumentId? FindDocumentId(string absolutePath)
    {
        if (_solution == null)
        {
            return null;
        }

        foreach (var project in _solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (document.FilePath != null &&
                    string.Equals(PathResolver.NormalizePath(document.FilePath),
                                 PathResolver.NormalizePath(absolutePath),
                                 StringComparison.OrdinalIgnoreCase))
                {
                    return document.Id;
                }
            }
        }

        return null;
    }

    private void OnWorkspaceFailed(WorkspaceDiagnosticEventArgs e)
    {
        // Log diagnostic - could be warning or error during project load
        // For now, we'll just capture these silently
        // TODO: Add proper logging
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SolutionManager));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _workspace?.Dispose();
        _lock.Dispose();
    }
}
