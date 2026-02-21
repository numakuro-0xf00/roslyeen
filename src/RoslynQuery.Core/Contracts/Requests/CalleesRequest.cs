namespace RoslynQuery.Core.Contracts.Requests;

/// <summary>
/// Request to find all methods/properties called by a method (call hierarchy - outgoing).
/// </summary>
public record CalleesRequest : PositionRequest
{
}
