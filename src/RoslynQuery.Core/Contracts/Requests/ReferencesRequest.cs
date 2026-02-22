namespace RoslynQuery.Core.Contracts.Requests;

/// <summary>
/// Request to find all references to a symbol.
/// </summary>
public record ReferencesRequest : PositionRequest
{
    /// <summary>
    /// Include the definition location in the results.
    /// </summary>
    public bool IncludeDefinition { get; init; }
}
