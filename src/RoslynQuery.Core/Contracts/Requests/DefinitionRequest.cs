namespace RoslynQuery.Core.Contracts.Requests;

/// <summary>
/// Request to find the definition of a symbol at a given position.
/// </summary>
public record DefinitionRequest : PositionRequest
{
}
