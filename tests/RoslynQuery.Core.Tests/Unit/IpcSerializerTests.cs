using System.Text.Json;
using RoslynQuery.Core.Contracts.Responses;
using RoslynQuery.Core.IpcProtocol;

namespace RoslynQuery.Core.Tests.Unit;

public class IpcSerializerTests
{
    #region Location serialization

    [Fact]
    public void Serialize_Location_ProducesCorrectJsonStructure()
    {
        var location = new Location
        {
            FilePath = "src/MyClass.cs",
            Line = 10,
            Column = 5
        };

        var json = IpcSerializer.Serialize(location);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("src/MyClass.cs", root.GetProperty("filePath").GetString());
        Assert.Equal(10, root.GetProperty("line").GetInt32());
        Assert.Equal(5, root.GetProperty("column").GetInt32());
    }

    [Fact]
    public void Serialize_ThenDeserialize_Location_RoundTripsCorrectly()
    {
        var original = new Location
        {
            FilePath = "src/MyClass.cs",
            Line = 10,
            Column = 5,
            EndLine = 10,
            EndColumn = 15
        };

        var json = IpcSerializer.Serialize(original);
        var deserialized = IpcSerializer.Deserialize<Location>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.FilePath, deserialized.FilePath);
        Assert.Equal(original.Line, deserialized.Line);
        Assert.Equal(original.Column, deserialized.Column);
        Assert.Equal(original.EndLine, deserialized.EndLine);
        Assert.Equal(original.EndColumn, deserialized.EndColumn);
    }

    [Fact]
    public void Serialize_LocationWithNullOptionalFields_OmitsNullProperties()
    {
        var location = new Location
        {
            FilePath = "test.cs",
            Line = 1,
            Column = 1
            // EndLine and EndColumn are null
        };

        var json = IpcSerializer.Serialize(location);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("endLine", out _), "endLine should be omitted when null");
        Assert.False(root.TryGetProperty("endColumn", out _), "endColumn should be omitted when null");
    }

    #endregion

    #region Enum serialization

    [Fact]
    public void Serialize_DiagnosticSeverityEnum_UsesCamelCase()
    {
        var info = new DiagnosticInfo
        {
            Id = "CS0001",
            Severity = DiagnosticSeverity.Error,
            Message = "test"
        };

        var json = IpcSerializer.Serialize(info);
        using var doc = JsonDocument.Parse(json);
        var severity = doc.RootElement.GetProperty("severity").GetString();

        Assert.Equal("error", severity);
    }

    #endregion

    #region JSON-RPC request/response

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
    public void CreateSuccessResponse_HasResultAndNoError()
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
    public void CreateErrorResponse_HasErrorAndNoResult()
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

    #endregion

    #region Byte serialization

    [Fact]
    public void SerializeToUtf8Bytes_ThenDeserialize_RoundTripsCorrectly()
    {
        var original = new Location
        {
            FilePath = "test.cs",
            Line = 42,
            Column = 7
        };

        var bytes = IpcSerializer.SerializeToUtf8Bytes(original);
        var deserialized = IpcSerializer.Deserialize<Location>(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal(original.FilePath, deserialized.FilePath);
        Assert.Equal(original.Line, deserialized.Line);
        Assert.Equal(original.Column, deserialized.Column);
    }

    #endregion

    #region ToJsonElement

    [Fact]
    public void ToJsonElement_PreservesStructure()
    {
        var obj = new { Value = 42, Name = "test" };

        var element = IpcSerializer.ToJsonElement(obj);

        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.Equal(42, element.GetProperty("value").GetInt32());
        Assert.Equal("test", element.GetProperty("name").GetString());
    }

    #endregion
}
