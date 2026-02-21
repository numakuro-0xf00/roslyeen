namespace RoslynQuery.Core.Contracts.Requests;

/// <summary>
/// Base request for position-based queries (definition, references, etc.).
/// </summary>
public record PositionRequest
{
    /// <summary>
    /// Path to the source file (can be absolute or solution-relative).
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// 1-based line number.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// 1-based column number.
    /// </summary>
    public required int Column { get; init; }
}
