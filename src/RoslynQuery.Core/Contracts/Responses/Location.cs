namespace RoslynQuery.Core.Contracts.Responses;

/// <summary>
/// Represents a location in source code.
/// </summary>
public record Location
{
    /// <summary>
    /// File path (solution-relative by default).
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

    /// <summary>
    /// End line (1-based), if this represents a span.
    /// </summary>
    public int? EndLine { get; init; }

    /// <summary>
    /// End column (1-based), if this represents a span.
    /// </summary>
    public int? EndColumn { get; init; }

    public override string ToString() =>
        EndLine.HasValue
            ? $"{FilePath}:{Line}:{Column}-{EndLine}:{EndColumn}"
            : $"{FilePath}:{Line}:{Column}";
}
