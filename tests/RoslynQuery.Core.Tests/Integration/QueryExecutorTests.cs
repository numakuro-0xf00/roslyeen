using RoslynQuery.Core.Contracts.Requests;
using RoslynQuery.Core.Queries;
using RoslynQuery.Core.Tests.Helpers;

namespace RoslynQuery.Core.Tests.Integration;

public class QueryExecutorTests
{
    private static (QueryExecutor Executor, TestSolutionProvider Provider) CreateExecutor(string fileName, string code)
    {
        var provider = new TestSolutionProvider().AddDocument(fileName, code);
        var executor = new QueryExecutor(provider);
        return (executor, provider);
    }

    #region GetDefinitionAsync

    [Fact]
    public async Task GetDefinitionAsync_WhenCallingMethod_ReturnsTheMethodDeclarationLocation()
    {
        var (source, markers) = SourceMarker.ParseMultiple(
            """
            namespace TestNamespace {
            public class TestClass {
                public void [|def:MyMethod|]() { }
                public void Caller() { [|call:MyMethod|](); }
            }}
            """);

        var (executor, provider) = CreateExecutor("Test.cs", source);
        using var _ = provider;

        var result = await executor.GetDefinitionAsync(new DefinitionRequest
        {
            FilePath = "Test.cs",
            Line = markers["call"].Line,
            Column = markers["call"].Column
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("MyMethod", result.Data!.SymbolName);
        Assert.Equal(markers["def"].Line, result.Data.Location!.Line);
    }

    [Fact]
    public async Task GetDefinitionAsync_WhenPositionIsOnWhitespace_ReturnsSymbolNotFound()
    {
        var (source, line, column) = SourceMarker.Parse(
            """
            [|  |]namespace TestNamespace { }
            """);

        var (executor, provider) = CreateExecutor("Test.cs", source);
        using var _ = provider;

        var result = await executor.GetDefinitionAsync(new DefinitionRequest
        {
            FilePath = "Test.cs",
            Line = line,
            Column = column
        });

        Assert.False(result.Success);
    }

    #endregion

    #region GetBaseDefinitionAsync

    [Fact]
    public async Task GetBaseDefinitionAsync_WhenImplementingInterfaceMethod_ReturnsInterfaceMethodLocation()
    {
        var (source, markers) = SourceMarker.ParseMultiple(
            """
            namespace TestNamespace {
            public interface IService {
                void [|iface:Execute|]();
            }
            public class Service : IService {
                public void [|impl:Execute|]() { }
            }}
            """);

        var (executor, provider) = CreateExecutor("Test.cs", source);
        using var _ = provider;

        var result = await executor.GetBaseDefinitionAsync(new BaseDefinitionRequest
        {
            FilePath = "Test.cs",
            Line = markers["impl"].Line,
            Column = markers["impl"].Column
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("Execute", result.Data!.SymbolName);
        Assert.Equal(markers["iface"].Line, result.Data.Location!.Line);
    }

    [Fact]
    public async Task GetBaseDefinitionAsync_WhenOverridingBaseClassMethod_ReturnsBaseMethodLocation()
    {
        var (source, markers) = SourceMarker.ParseMultiple(
            """
            namespace TestNamespace {
            public class Base {
                public virtual void [|base:DoWork|]() { }
            }
            public class Derived : Base {
                public override void [|derived:DoWork|]() { }
            }}
            """);

        var (executor, provider) = CreateExecutor("Test.cs", source);
        using var _ = provider;

        var result = await executor.GetBaseDefinitionAsync(new BaseDefinitionRequest
        {
            FilePath = "Test.cs",
            Line = markers["derived"].Line,
            Column = markers["derived"].Column
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("DoWork", result.Data!.SymbolName);
        Assert.Equal(markers["base"].Line, result.Data.Location!.Line);
    }

    #endregion

    #region GetImplementationsAsync

    [Fact]
    public async Task GetImplementationsAsync_WhenInterfaceHasImplementors_ReturnsBothImplementations()
    {
        var (source, markers) = SourceMarker.ParseMultiple(
            """
            namespace TestNamespace {
            public interface [|iface:IMyInterface|] { }
            public class Impl1 : IMyInterface { }
            public class Impl2 : IMyInterface { }
            }
            """);

        var (executor, provider) = CreateExecutor("Test.cs", source);
        using var _ = provider;

        var result = await executor.GetImplementationsAsync(new ImplementationsRequest
        {
            FilePath = "Test.cs",
            Line = markers["iface"].Line,
            Column = markers["iface"].Column
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, result.Data!.Count);
    }

    #endregion

    #region GetReferencesAsync

    [Fact]
    public async Task GetReferencesAsync_WhenMethodCalledTwice_ReturnsTwoReferences()
    {
        var (source, markers) = SourceMarker.ParseMultiple(
            """
            namespace TestNamespace {
            public class TestClass {
                public void [|def:MyMethod|]() { }
                public void Caller1() { MyMethod(); }
                public void Caller2() { MyMethod(); }
            }}
            """);

        var (executor, provider) = CreateExecutor("Test.cs", source);
        using var _ = provider;

        var result = await executor.GetReferencesAsync(new ReferencesRequest
        {
            FilePath = "Test.cs",
            Line = markers["def"].Line,
            Column = markers["def"].Column,
            IncludeDefinition = false
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.Data!.Count >= 2, $"Expected at least 2 references, got {result.Data.Count}");
    }

    #endregion

    #region GetCallersAsync

    [Fact]
    public async Task GetCallersAsync_WhenMethodHasTwoCallers_ReturnsBoth()
    {
        var (source, markers) = SourceMarker.ParseMultiple(
            """
            namespace TestNamespace {
            public class TestClass {
                public void [|target:Target|]() { }
                public void Caller1() { Target(); }
                public void Caller2() { Target(); }
            }}
            """);

        var (executor, provider) = CreateExecutor("Test.cs", source);
        using var _ = provider;

        var result = await executor.GetCallersAsync(new CallersRequest
        {
            FilePath = "Test.cs",
            Line = markers["target"].Line,
            Column = markers["target"].Column
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, result.Data!.Count);
    }

    #endregion

    #region GetCalleesAsync

    [Fact]
    public async Task GetCalleesAsync_WhenMethodCallsTwoHelpers_ReturnsBothCallees()
    {
        var (source, markers) = SourceMarker.ParseMultiple(
            """
            namespace TestNamespace {
            public class TestClass {
                public void Helper1() { }
                public void Helper2() { }
                public void [|main:Main|]() { Helper1(); Helper2(); }
            }}
            """);

        var (executor, provider) = CreateExecutor("Test.cs", source);
        using var _ = provider;

        var result = await executor.GetCalleesAsync(new CalleesRequest
        {
            FilePath = "Test.cs",
            Line = markers["main"].Line,
            Column = markers["main"].Column
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.Data!.Count >= 2, $"Expected at least 2 callees, got {result.Data.Count}");
    }

    #endregion

    #region GetSymbolInfoAsync

    [Fact]
    public async Task GetSymbolInfoAsync_WhenStaticMethod_ReturnsStaticModifier()
    {
        var (source, markers) = SourceMarker.ParseMultiple(
            """
            namespace TestNamespace {
            public class TestClass {
                public static void [|method:MyMethod|]() { }
            }}
            """);

        var (executor, provider) = CreateExecutor("Test.cs", source);
        using var _ = provider;

        var result = await executor.GetSymbolInfoAsync(new SymbolRequest
        {
            FilePath = "Test.cs",
            Line = markers["method"].Line,
            Column = markers["method"].Column
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("MyMethod", result.Data!.Name);
        Assert.Equal(Contracts.Enums.SymbolKind.Method, result.Data.Kind);
        Assert.Contains("static", result.Data.Modifiers);
    }

    [Fact]
    public async Task GetSymbolInfoAsync_WhenProperty_ReturnsPropertyKind()
    {
        var (source, markers) = SourceMarker.ParseMultiple(
            """
            namespace TestNamespace {
            public class TestClass {
                public int [|prop:MyProperty|] { get; set; }
            }}
            """);

        var (executor, provider) = CreateExecutor("Test.cs", source);
        using var _ = provider;

        var result = await executor.GetSymbolInfoAsync(new SymbolRequest
        {
            FilePath = "Test.cs",
            Line = markers["prop"].Line,
            Column = markers["prop"].Column
        });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("MyProperty", result.Data!.Name);
        Assert.Equal(Contracts.Enums.SymbolKind.Property, result.Data.Kind);
    }

    #endregion

    #region GetDiagnosticsAsync

    [Fact]
    public async Task GetDiagnosticsAsync_WhenCodeHasError_ReturnsErrors()
    {
        var code = """
            namespace TestNamespace {
            public class TestClass {
                public void Method() { UndefinedMethod(); }
            }}
            """;

        var (executor, provider) = CreateExecutor("Test.cs", code);
        using var _ = provider;

        var result = await executor.GetDiagnosticsAsync(new DiagnosticsRequest
        {
            IncludeWarnings = true
        });

        Assert.True(result.Success);
        Assert.True(result.Data!.ErrorCount > 0);
    }

    [Fact]
    public async Task GetDiagnosticsAsync_WhenFilteredByFile_ReturnsOnlyThatFilesDiagnostics()
    {
        var goodCode = """
            namespace TestNamespace {
            public class GoodClass {
                public void Method() { }
            }}
            """;
        var badCode = """
            namespace TestNamespace {
            public class BadClass {
                public void Method() { UndefinedMethod(); }
            }}
            """;

        using var provider = new TestSolutionProvider()
            .AddDocument("Good.cs", goodCode)
            .AddDocument("Bad.cs", badCode);
        var executor = new QueryExecutor(provider);

        var result = await executor.GetDiagnosticsAsync(new DiagnosticsRequest
        {
            FilePath = "Good.cs",
            IncludeWarnings = true
        });

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.ErrorCount);
    }

    #endregion
}
