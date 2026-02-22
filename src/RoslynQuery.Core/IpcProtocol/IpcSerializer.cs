using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoslynQuery.Core.IpcProtocol;

/// <summary>
/// JSON serialization helpers for IPC protocol.
/// </summary>
public static class IpcSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    /// <summary>
    /// Serialize an object to JSON.
    /// </summary>
    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    /// <summary>
    /// Serialize an object to JSON bytes.
    /// </summary>
    public static byte[] SerializeToUtf8Bytes<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, Options);
    }

    /// <summary>
    /// Deserialize JSON to an object.
    /// </summary>
    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    /// <summary>
    /// Deserialize JSON bytes to an object.
    /// </summary>
    public static T? Deserialize<T>(ReadOnlySpan<byte> utf8Json)
    {
        return JsonSerializer.Deserialize<T>(utf8Json, Options);
    }

    /// <summary>
    /// Deserialize a JsonElement to a specific type.
    /// </summary>
    public static T? Deserialize<T>(JsonElement element)
    {
        return element.Deserialize<T>(Options);
    }

    /// <summary>
    /// Convert an object to a JsonElement.
    /// </summary>
    public static JsonElement ToJsonElement<T>(T value)
    {
        var json = Serialize(value);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Create a JSON-RPC request.
    /// </summary>
    public static JsonRpcRequest CreateRequest<T>(string method, T parameters)
    {
        return new JsonRpcRequest
        {
            Method = method,
            Params = ToJsonElement(parameters)
        };
    }

    /// <summary>
    /// Create a successful JSON-RPC response.
    /// </summary>
    public static JsonRpcResponse CreateSuccessResponse<T>(string id, T result)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Result = ToJsonElement(result)
        };
    }

    /// <summary>
    /// Create an error JSON-RPC response.
    /// </summary>
    public static JsonRpcResponse CreateErrorResponse(string id, int code, string message, object? data = null)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message,
                Data = data != null ? ToJsonElement(data) : null
            }
        };
    }
}
