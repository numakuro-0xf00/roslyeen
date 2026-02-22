namespace RoslynQuery.Core.Contracts.Responses;

/// <summary>
/// Response containing a single location (definition, base-definition).
/// </summary>
public record LocationResponse
{
    /// <summary>
    /// The location of the symbol.
    /// </summary>
    public Location? Location { get; init; }

    /// <summary>
    /// Name of the symbol at this location.
    /// </summary>
    public string? SymbolName { get; init; }

    /// <summary>
    /// Kind of the symbol.
    /// </summary>
    public string? SymbolKind { get; init; }
}
