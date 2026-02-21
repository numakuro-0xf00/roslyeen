namespace RoslynQuery.Core.Contracts.Requests;

/// <summary>
/// Request to get diagnostics (errors/warnings) for a file or the entire solution.
/// </summary>
public record DiagnosticsRequest
{
    /// <summary>
    /// Optional file path. If null, returns diagnostics for the entire solution.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Include warnings in addition to errors.
    /// </summary>
    public bool IncludeWarnings { get; init; } = true;

    /// <summary>
    /// Include info-level diagnostics.
    /// </summary>
    public bool IncludeInfo { get; init; }
}
