using RoslynQuery.Core.Contracts.Enums;

namespace RoslynQuery.Core.Contracts.Responses;

/// <summary>
/// Detailed information about a symbol.
/// </summary>
public record SymbolInfoResponse
{
    /// <summary>
    /// The symbol's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Fully qualified name.
    /// </summary>
    public required string FullName { get; init; }

    /// <summary>
    /// The kind of symbol.
    /// </summary>
    public required SymbolKind Kind { get; init; }

    /// <summary>
    /// The symbol's signature (e.g., method signature with parameters).
    /// </summary>
    public string? Signature { get; init; }

    /// <summary>
    /// XML documentation summary.
    /// </summary>
    public string? Documentation { get; init; }

    /// <summary>
    /// Containing type name, if applicable.
    /// </summary>
    public string? ContainingType { get; init; }

    /// <summary>
    /// Containing namespace.
    /// </summary>
    public string? ContainingNamespace { get; init; }

    /// <summary>
    /// Return type for methods/properties.
    /// </summary>
    public string? ReturnType { get; init; }

    /// <summary>
    /// Accessibility (public, private, etc.).
    /// </summary>
    public string? Accessibility { get; init; }

    /// <summary>
    /// Modifiers (static, virtual, abstract, etc.).
    /// </summary>
    public IReadOnlyList<string> Modifiers { get; init; } = [];

    /// <summary>
    /// Definition location.
    /// </summary>
    public Location? Location { get; init; }
}
