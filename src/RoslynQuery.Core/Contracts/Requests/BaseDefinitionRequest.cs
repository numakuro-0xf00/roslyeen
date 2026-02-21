namespace RoslynQuery.Core.Contracts.Requests;

/// <summary>
/// Request to find the base definition (interface/base class) of a symbol.
/// </summary>
public record BaseDefinitionRequest : PositionRequest
{
}
