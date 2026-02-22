using RoslynQuery.Core.Contracts.Enums;

namespace RoslynQuery.Core.Contracts.Responses;

/// <summary>
/// Base result for query operations.
/// </summary>
public record QueryResult
{
    /// <summary>
    /// Whether the query succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error code if the query failed.
    /// </summary>
    public QueryErrorCode ErrorCode { get; init; } = QueryErrorCode.None;

    /// <summary>
    /// Error message if the query failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static QueryResult Ok() => new() { Success = true };

    public static QueryResult Fail(QueryErrorCode code, string message) =>
        new() { Success = false, ErrorCode = code, ErrorMessage = message };
}

/// <summary>
/// Query result with data payload.
/// </summary>
public record QueryResult<T> : QueryResult
{
    /// <summary>
    /// The result data.
    /// </summary>
    public T? Data { get; init; }

    public static QueryResult<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public new static QueryResult<T> Fail(QueryErrorCode code, string message) =>
        new() { Success = false, ErrorCode = code, ErrorMessage = message };
}
