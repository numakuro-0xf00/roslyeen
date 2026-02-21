using RoslynQuery.Core.Contracts.Requests;
using RoslynQuery.Core.Contracts.Responses;

namespace RoslynQuery.Core.Contracts;

/// <summary>
/// Service interface for Roslyn-based code queries.
/// </summary>
public interface IQueryService
{
    /// <summary>
    /// Find the definition of a symbol at the given position.
    /// </summary>
    Task<QueryResult<LocationResponse>> GetDefinitionAsync(
        DefinitionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find the base definition (interface/base class member) of a symbol.
    /// </summary>
    Task<QueryResult<LocationResponse>> GetBaseDefinitionAsync(
        BaseDefinitionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all implementations of an interface or virtual member.
    /// </summary>
    Task<QueryResult<LocationsResponse>> GetImplementationsAsync(
        ImplementationsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all references to a symbol.
    /// </summary>
    Task<QueryResult<LocationsResponse>> GetReferencesAsync(
        ReferencesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all callers of a method/property (call hierarchy - incoming).
    /// </summary>
    Task<QueryResult<LocationsResponse>> GetCallersAsync(
        CallersRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find all methods/properties called by a method (call hierarchy - outgoing).
    /// </summary>
    Task<QueryResult<LocationsResponse>> GetCalleesAsync(
        CalleesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed information about a symbol.
    /// </summary>
    Task<QueryResult<SymbolInfoResponse>> GetSymbolInfoAsync(
        SymbolRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get diagnostics (errors/warnings) for a file or the entire solution.
    /// </summary>
    Task<QueryResult<DiagnosticsResponse>> GetDiagnosticsAsync(
        DiagnosticsRequest request,
        CancellationToken cancellationToken = default);
}
