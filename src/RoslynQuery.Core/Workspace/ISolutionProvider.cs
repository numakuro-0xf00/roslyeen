using Microsoft.CodeAnalysis;

namespace RoslynQuery.Core.Workspace;

/// <summary>
/// Provides access to a loaded Roslyn solution for query execution.
/// </summary>
public interface ISolutionProvider
{
    /// <summary>
    /// Get the current solution snapshot.
    /// </summary>
    Solution GetSolution();

    /// <summary>
    /// Find a document by file path.
    /// </summary>
    Document? FindDocument(string filePath);

    /// <summary>
    /// Gets the solution root directory.
    /// </summary>
    string SolutionRoot { get; }

    /// <summary>
    /// Get all projects in the solution.
    /// </summary>
    IEnumerable<Project> GetProjects();
}
