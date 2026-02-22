namespace RoslynQuery.Core.Contracts.Requests;

/// <summary>
/// Request to find all callers of a method/property (call hierarchy - incoming).
/// </summary>
public record CallersRequest : PositionRequest
{
}
