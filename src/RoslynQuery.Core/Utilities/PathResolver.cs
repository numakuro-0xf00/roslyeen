using System.Security.Cryptography;
using System.Text;

namespace RoslynQuery.Core.Utilities;

/// <summary>
/// Utility for path resolution and normalization.
/// </summary>
public static class PathResolver
{
    /// <summary>
    /// Converts an absolute path to a solution-relative path.
    /// </summary>
    /// <param name="absolutePath">The absolute file path.</param>
    /// <param name="solutionRoot">The solution root directory.</param>
    /// <returns>Solution-relative path, or the original path if not under solution root.</returns>
    public static string ToRelativePath(string absolutePath, string solutionRoot)
    {
        ArgumentNullException.ThrowIfNull(absolutePath);
        ArgumentNullException.ThrowIfNull(solutionRoot);

        var normalizedAbsolute = NormalizePath(absolutePath);
        var normalizedRoot = NormalizePath(solutionRoot);

        if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            normalizedRoot += Path.DirectorySeparatorChar;
        }

        if (normalizedAbsolute.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedAbsolute[normalizedRoot.Length..];
        }

        return absolutePath;
    }

    /// <summary>
    /// Converts a solution-relative path to an absolute path.
    /// </summary>
    /// <param name="relativePath">The solution-relative path.</param>
    /// <param name="solutionRoot">The solution root directory.</param>
    /// <returns>Absolute path.</returns>
    public static string ToAbsolutePath(string relativePath, string solutionRoot)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(solutionRoot);

        if (Path.IsPathRooted(relativePath))
        {
            return NormalizePath(relativePath);
        }

        return NormalizePath(Path.Combine(solutionRoot, relativePath));
    }

    /// <summary>
    /// Normalizes a path (resolves .., /, etc.).
    /// </summary>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        var fullPath = Path.GetFullPath(path);
        return fullPath.Replace('\\', Path.DirectorySeparatorChar)
                       .Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Gets the socket path for daemon communication.
    /// </summary>
    /// <param name="solutionPath">The solution file path.</param>
    /// <returns>Unix domain socket path.</returns>
    public static string GetSocketPath(string solutionPath)
    {
        ArgumentNullException.ThrowIfNull(solutionPath);

        var normalized = NormalizePath(solutionPath);
        var hash = ComputeShortHash(normalized);
        var socketDir = GetSocketDirectory();

        Directory.CreateDirectory(socketDir);

        return Path.Combine(socketDir, $"roslyn-query-{hash}.sock");
    }

    /// <summary>
    /// Gets the PID file path for a daemon.
    /// </summary>
    /// <param name="solutionPath">The solution file path.</param>
    /// <returns>PID file path.</returns>
    public static string GetPidFilePath(string solutionPath)
    {
        ArgumentNullException.ThrowIfNull(solutionPath);

        var normalized = NormalizePath(solutionPath);
        var hash = ComputeShortHash(normalized);
        var socketDir = GetSocketDirectory();

        Directory.CreateDirectory(socketDir);

        return Path.Combine(socketDir, $"roslyn-query-{hash}.pid");
    }

    /// <summary>
    /// Gets the solution root directory from a solution file path.
    /// </summary>
    public static string GetSolutionRoot(string solutionPath)
    {
        ArgumentNullException.ThrowIfNull(solutionPath);

        return Path.GetDirectoryName(NormalizePath(solutionPath))
            ?? throw new ArgumentException("Invalid solution path", nameof(solutionPath));
    }

    /// <summary>
    /// Finds the solution file in a directory or its parents.
    /// </summary>
    /// <param name="startPath">Starting directory or file path.</param>
    /// <returns>Solution file path, or null if not found.</returns>
    public static string? FindSolutionFile(string startPath)
    {
        var directory = Directory.Exists(startPath)
            ? startPath
            : Path.GetDirectoryName(startPath);

        while (!string.IsNullOrEmpty(directory))
        {
            var slnFiles = Directory.GetFiles(directory, "*.sln");
            if (slnFiles.Length == 1)
            {
                return slnFiles[0];
            }
            if (slnFiles.Length > 1)
            {
                // Multiple solutions - return the first one alphabetically
                // Caller should specify explicitly
                return slnFiles.OrderBy(f => f).First();
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static string GetSocketDirectory()
    {
        // Use XDG_RUNTIME_DIR on Linux, or temp directory
        var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrEmpty(runtimeDir) && Directory.Exists(runtimeDir))
        {
            return Path.Combine(runtimeDir, "roslyn-query");
        }

        return Path.Combine(Path.GetTempPath(), "roslyn-query");
    }

    private static string ComputeShortHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        // Take first 8 bytes and convert to hex
        return Convert.ToHexString(bytes[..8]).ToLowerInvariant();
    }
}
