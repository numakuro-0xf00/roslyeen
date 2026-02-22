namespace RoslynQuery.Core.FileSystem;

/// <summary>
/// Type of file change.
/// </summary>
public enum FileChangeType
{
    /// <summary>File content was modified.</summary>
    Changed,

    /// <summary>File was created.</summary>
    Created,

    /// <summary>File was deleted.</summary>
    Deleted,

    /// <summary>File was renamed.</summary>
    Renamed
}

/// <summary>
/// Represents a file change event.
/// </summary>
public record FileChangeEvent
{
    /// <summary>
    /// The type of change.
    /// </summary>
    public required FileChangeType ChangeType { get; init; }

    /// <summary>
    /// The absolute path of the file that changed.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// For rename events, the old path before rename.
    /// </summary>
    public string? OldFilePath { get; init; }

    /// <summary>
    /// Whether this file requires a full solution reload (.csproj, .sln).
    /// </summary>
    public bool RequiresFullReload =>
        FilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
        FilePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this is a C# source file.
    /// </summary>
    public bool IsSourceFile =>
        FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
}
