namespace RoslynQuery.Core.Contracts.Responses;

/// <summary>
/// Response containing diagnostics for files or solution.
/// </summary>
public record DiagnosticsResponse
{
    /// <summary>
    /// All diagnostics.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];

    /// <summary>
    /// Count of errors.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Count of warnings.
    /// </summary>
    public int WarningCount { get; init; }

    /// <summary>
    /// Count of info-level diagnostics.
    /// </summary>
    public int InfoCount { get; init; }
}
