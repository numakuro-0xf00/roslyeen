namespace RoslynQuery.Core.Contracts.Enums;

/// <summary>
/// Error codes returned by query operations.
/// </summary>
public enum QueryErrorCode
{
    /// <summary>No error occurred.</summary>
    None = 0,

    /// <summary>The requested symbol was not found at the specified location.</summary>
    SymbolNotFound = 1,

    /// <summary>Failed to load the solution or project.</summary>
    LoadFailure = 2,

    /// <summary>Invalid arguments were provided.</summary>
    ArgumentError = 3,

    /// <summary>Failed to connect to or communicate with the daemon.</summary>
    DaemonError = 4,

    /// <summary>The document/file was not found in the solution.</summary>
    DocumentNotFound = 5,

    /// <summary>An internal error occurred during query execution.</summary>
    InternalError = 6,

    /// <summary>The operation timed out.</summary>
    Timeout = 7
}
