using System.Text.Json;
using RoslynQuery.Core.Contracts.Responses;
using RoslynQuery.Core.IpcProtocol;

namespace RoslynQuery.Core.Tests.Unit;

public class IpcSerializerTests
{
    [Fact]
    public void Serialize_Location_ProducesValidJson()
    {
        var location = new Location
        {
            FilePath = "src/MyClass.cs",
            Line = 10,
            Column = 5
        };

        var json = IpcSerializer.Serialize(location);

        Assert.Contains("\"filePath\":", json);
        Assert.Contains("\"src/MyClass.cs\"", json);
        Assert.Contains("\"line\":10", json);
        Assert.Contains("\"column\":5", json);
    }

    [Fact]
    public void Deserialize_Location_ReturnsCorrectObject()
    {
        var json = "{\"filePath\":\"src/MyClass.cs\",\"line\":10,\"column\":5}";

        var location = IpcSerializer.Deserialize<Location>(json);

        Assert.NotNull(location);
        Assert.Equal("src/MyClass.cs", location.FilePath);
        Assert.Equal(10, location.Line);
        Assert.Equal(5, location.Column);
    }

    [Fact]
    public void CreateRequest_ProducesValidJsonRpcRequest()
    {
        var parameters = new { FilePath = "test.cs", Line = 1, Column = 1 };

        var request = IpcSerializer.CreateRequest("definition", parameters);

        Assert.Equal("2.0", request.JsonRpc);
        Assert.Equal("definition", request.Method);
        Assert.NotNull(request.Params);
        Assert.NotEmpty(request.Id);
    }

    [Fact]
    public void CreateSuccessResponse_ProducesValidJsonRpcResponse()
    {
        var result = new { Success = true };

        var response = IpcSerializer.CreateSuccessResponse("123", result);

        Assert.Equal("2.0", response.JsonRpc);
        Assert.Equal("123", response.Id);
        Assert.NotNull(response.Result);
        Assert.Null(response.Error);
        Assert.True(response.IsSuccess);
    }

    [Fact]
    public void CreateErrorResponse_ProducesValidJsonRpcResponse()
    {
        var response = IpcSerializer.CreateErrorResponse(
            "123",
            JsonRpcErrorCodes.SymbolNotFound,
            "Symbol not found");

        Assert.Equal("2.0", response.JsonRpc);
        Assert.Equal("123", response.Id);
        Assert.Null(response.Result);
        Assert.NotNull(response.Error);
        Assert.Equal(JsonRpcErrorCodes.SymbolNotFound, response.Error.Code);
        Assert.Equal("Symbol not found", response.Error.Message);
        Assert.False(response.IsSuccess);
    }

    [Fact]
    public void SerializeToUtf8Bytes_ProducesValidBytes()
    {
        var obj = new { Name = "test" };

        var bytes = IpcSerializer.SerializeToUtf8Bytes(obj);

        Assert.NotEmpty(bytes);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"name\":\"test\"", json);
    }

    [Fact]
    public void ToJsonElement_ProducesValidElement()
    {
        var obj = new { Value = 42 };

        var element = IpcSerializer.ToJsonElement(obj);

        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.True(element.TryGetProperty("value", out var valueProp));
        Assert.Equal(42, valueProp.GetInt32());
    }
}
