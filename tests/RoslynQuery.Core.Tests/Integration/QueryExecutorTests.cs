using RoslynQuery.Core.Contracts.Requests;

namespace RoslynQuery.Core.Tests.Integration;

public class QueryExecutorTests
{
    [Fact]
    public async Task GetDefinitionAsync_WithMethodCall_ReturnsMethodDefinition()
    {
        // Arrange - line numbers are 1-based
        // Line 1: namespace TestNamespace {
        // Line 2: public class TestClass {
        // Line 3: public void MyMethod() { }
        // Line 4: public void Caller() { MyMethod(); }
        // Line 5: }}
        var code = @"namespace TestNamespace {
public class TestClass {
public void MyMethod() { }
public void Caller() { MyMethod(); }
}}";
        using var builder = new TestSolutionBuilder().AddDocument("Test.cs", code);
        var service = builder.Build();

        // Act - find definition of MyMethod() call at line 4, col 24
        var request = new DefinitionRequest
        {
            FilePath = "Test.cs",
            Line = 4,
            Column = 24
        };
        var result = await service.GetDefinitionAsync(request);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.Location);
        Assert.Equal("MyMethod", result.Data.SymbolName);
        Assert.Equal(3, result.Data.Location.Line);
    }

    [Fact]
    public async Task GetReferencesAsync_WithMethod_ReturnsReferences()
    {
        // Arrange
        var code = @"namespace TestNamespace {
public class TestClass {
public void MyMethod() { }
public void Caller1() { MyMethod(); }
public void Caller2() { MyMethod(); }
}}";
        using var builder = new TestSolutionBuilder().AddDocument("Test.cs", code);
        var service = builder.Build();

        // Act - find references to MyMethod definition at line 3
        var request = new ReferencesRequest
        {
            FilePath = "Test.cs",
            Line = 3,
            Column = 13,
            IncludeDefinition = false
        };
        var result = await service.GetReferencesAsync(request);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Count >= 2, $"Expected at least 2 references, got {result.Data.Count}");
    }

    [Fact]
    public async Task GetImplementationsAsync_WithInterface_ReturnsImplementations()
    {
        // Arrange
        var code = @"namespace TestNamespace {
public interface IMyInterface { }
public class Impl1 : IMyInterface { }
public class Impl2 : IMyInterface { }
}";
        using var builder = new TestSolutionBuilder().AddDocument("Test.cs", code);
        var service = builder.Build();

        // Act - find implementations of IMyInterface at line 2
        var request = new ImplementationsRequest
        {
            FilePath = "Test.cs",
            Line = 2,
            Column = 18
        };
        var result = await service.GetImplementationsAsync(request);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public async Task GetSymbolInfoAsync_WithMethod_ReturnsInfo()
    {
        // Arrange
        var code = @"namespace TestNamespace {
public class TestClass {
public static void MyMethod() { }
}}";
        using var builder = new TestSolutionBuilder().AddDocument("Test.cs", code);
        var service = builder.Build();

        // Act
        var request = new SymbolRequest
        {
            FilePath = "Test.cs",
            Line = 3,
            Column = 20
        };
        var result = await service.GetSymbolInfoAsync(request);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Data);
        Assert.Equal("MyMethod", result.Data.Name);
        Assert.Contains("static", result.Data.Modifiers);
    }

    [Fact]
    public async Task GetCallersAsync_WithMethod_ReturnsCallers()
    {
        // Arrange
        var code = @"namespace TestNamespace {
public class TestClass {
public void Target() { }
public void Caller1() { Target(); }
public void Caller2() { Target(); }
}}";
        using var builder = new TestSolutionBuilder().AddDocument("Test.cs", code);
        var service = builder.Build();

        // Act
        var request = new CallersRequest
        {
            FilePath = "Test.cs",
            Line = 3,
            Column = 13
        };
        var result = await service.GetCallersAsync(request);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public async Task GetCalleesAsync_WithMethod_ReturnsCallees()
    {
        // Arrange
        var code = @"namespace TestNamespace {
public class TestClass {
public void Helper1() { }
public void Helper2() { }
public void Main() { Helper1(); Helper2(); }
}}";
        using var builder = new TestSolutionBuilder().AddDocument("Test.cs", code);
        var service = builder.Build();

        // Act
        var request = new CalleesRequest
        {
            FilePath = "Test.cs",
            Line = 5,
            Column = 13
        };
        var result = await service.GetCalleesAsync(request);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Count >= 2, $"Expected at least 2 callees, got {result.Data.Count}");
    }

    [Fact]
    public async Task GetDiagnosticsAsync_WithErrors_ReturnsErrors()
    {
        // Arrange
        var code = @"namespace TestNamespace {
public class TestClass {
public void Method() { UndefinedMethod(); }
}}";
        using var builder = new TestSolutionBuilder().AddDocument("Test.cs", code);
        var service = builder.Build();

        // Act
        var request = new DiagnosticsRequest
        {
            IncludeWarnings = true
        };
        var result = await service.GetDiagnosticsAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ErrorCount > 0);
    }

    [Fact]
    public async Task GetDefinitionAsync_SymbolNotFound_ReturnsError()
    {
        // Arrange
        var code = @"namespace TestNamespace { }";
        using var builder = new TestSolutionBuilder().AddDocument("Test.cs", code);
        var service = builder.Build();

        // Act - try to find symbol in whitespace
        var request = new DefinitionRequest
        {
            FilePath = "Test.cs",
            Line = 1,
            Column = 1
        };
        var result = await service.GetDefinitionAsync(request);

        // Assert
        Assert.False(result.Success);
    }
}
