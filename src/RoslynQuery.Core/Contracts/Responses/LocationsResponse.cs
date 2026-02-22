namespace RoslynQuery.Core.Contracts.Responses;

/// <summary>
/// Response containing multiple locations (references, implementations, callers).
/// </summary>
public record LocationsResponse
{
    /// <summary>
    /// Name of the queried symbol.
    /// </summary>
    public string? SymbolName { get; init; }

    /// <summary>
    /// The locations found.
    /// </summary>
    public IReadOnlyList<ReferenceLocation> Locations { get; init; } = [];

    /// <summary>
    /// Total count of locations.
    /// </summary>
    public int Count => Locations.Count;
}

/// <summary>
/// A reference location with additional context.
/// </summary>
public record ReferenceLocation
{
    /// <summary>
    /// The source location.
    /// </summary>
    public required Location Location { get; init; }

    /// <summary>
    /// The code snippet at this location.
    /// </summary>
    public string? Snippet { get; init; }

    /// <summary>
    /// Name of the containing member (method, property, etc.).
    /// </summary>
    public string? ContainingMember { get; init; }

    /// <summary>
    /// Name of the containing type.
    /// </summary>
    public string? ContainingType { get; init; }
}
