namespace RoslynQuery.Core.Contracts.Responses;

/// <summary>
/// Information about a compiler diagnostic (error/warning).
/// </summary>
public record DiagnosticInfo
{
    /// <summary>
    /// Diagnostic ID (e.g., CS0103).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Severity level.
    /// </summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// The diagnostic message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Location of the diagnostic.
    /// </summary>
    public Location? Location { get; init; }
}

/// <summary>
/// Diagnostic severity levels.
/// </summary>
public enum DiagnosticSeverity
{
    Hidden,
    Info,
    Warning,
    Error
}
