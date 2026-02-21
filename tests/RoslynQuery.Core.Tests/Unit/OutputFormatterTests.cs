using RoslynQuery.Cli;
using RoslynQuery.Cli.Commands;
using RoslynQuery.Core.Contracts.Enums;
using RoslynQuery.Core.Contracts.Responses;

namespace RoslynQuery.Core.Tests.Unit;

public class OutputFormatterTests
{
    #region FormatLocation

    [Fact]
    public void FormatLocation_TextMode_ValidLocation_ReturnsFileLineCol()
    {
        var response = new LocationResponse
        {
            Location = new Location { FilePath = "src/Foo.cs", Line = 42, Column = 15 },
            SymbolName = "MyMethod",
            SymbolKind = "Method"
        };

        var result = OutputFormatter.FormatLocation(response, OutputFormat.Text);

        Assert.Equal("src/Foo.cs:42:15", result);
    }

    [Fact]
    public void FormatLocation_TextMode_NullResponse_ReturnsNull()
    {
        var result = OutputFormatter.FormatLocation(null, OutputFormat.Text);

        Assert.Null(result);
    }

    [Fact]
    public void FormatLocation_TextMode_NullLocation_ReturnsNull()
    {
        var response = new LocationResponse { Location = null };

        var result = OutputFormatter.FormatLocation(response, OutputFormat.Text);

        Assert.Null(result);
    }

    [Fact]
    public void FormatLocation_JsonMode_NullResponse_ReturnsJsonString()
    {
        var result = OutputFormatter.FormatLocation(null, OutputFormat.Json);

        Assert.NotNull(result);
        Assert.Contains("null", result);
    }

    [Fact]
    public void FormatLocation_TextMode_DoesNotContainSymbolKindOrName()
    {
        var response = new LocationResponse
        {
            Location = new Location { FilePath = "src/Foo.cs", Line = 1, Column = 1 },
            SymbolName = "MyMethod",
            SymbolKind = "Method"
        };

        var result = OutputFormatter.FormatLocation(response, OutputFormat.Text);

        Assert.DoesNotContain("MyMethod", result);
        Assert.DoesNotContain("Method", result);
    }

    #endregion

    #region FormatLocations

    [Fact]
    public void FormatLocations_TextMode_MultipleResults_OneLinePerLocation()
    {
        var response = new LocationsResponse
        {
            SymbolName = "Foo",
            Locations =
            [
                new ReferenceLocation
                {
                    Location = new Location { FilePath = "src/A.cs", Line = 10, Column = 5 },
                    Snippet = "var x = Foo();",
                    ContainingMember = "Bar",
                    ContainingType = "MyClass"
                },
                new ReferenceLocation
                {
                    Location = new Location { FilePath = "src/B.cs", Line = 20, Column = 3 }
                }
            ]
        };

        var result = OutputFormatter.FormatLocations(response, OutputFormat.Text);

        Assert.NotNull(result);
        var lines = result!.Split(Environment.NewLine);
        Assert.Equal(2, lines.Length);
        Assert.Equal("src/A.cs:10:5", lines[0]);
        Assert.Equal("src/B.cs:20:3", lines[1]);
    }

    [Fact]
    public void FormatLocations_TextMode_EmptyResponse_ReturnsNull()
    {
        var response = new LocationsResponse { Locations = [] };

        var result = OutputFormatter.FormatLocations(response, OutputFormat.Text);

        Assert.Null(result);
    }

    [Fact]
    public void FormatLocations_TextMode_NullResponse_ReturnsNull()
    {
        var result = OutputFormatter.FormatLocations(null, OutputFormat.Text);

        Assert.Null(result);
    }

    [Fact]
    public void FormatLocations_TextMode_NoHeaderLine()
    {
        var response = new LocationsResponse
        {
            SymbolName = "Foo",
            Locations =
            [
                new ReferenceLocation
                {
                    Location = new Location { FilePath = "src/A.cs", Line = 1, Column = 1 }
                }
            ]
        };

        var result = OutputFormatter.FormatLocations(response, OutputFormat.Text);

        Assert.NotNull(result);
        Assert.DoesNotContain("Found", result);
        Assert.DoesNotContain("location", result!.ToLowerInvariant().Replace("src/a.cs:1:1", ""));
    }

    [Fact]
    public void FormatLocations_TextMode_NoSnippetInOutput()
    {
        var response = new LocationsResponse
        {
            Locations =
            [
                new ReferenceLocation
                {
                    Location = new Location { FilePath = "src/A.cs", Line = 1, Column = 1 },
                    Snippet = "var x = Foo();"
                }
            ]
        };

        var result = OutputFormatter.FormatLocations(response, OutputFormat.Text);

        Assert.NotNull(result);
        Assert.DoesNotContain("var x = Foo();", result);
    }

    #endregion

    #region FormatDiagnostics

    [Fact]
    public void FormatDiagnostics_TextMode_ErrorWithLocation_GccFormat()
    {
        var response = new DiagnosticsResponse
        {
            Diagnostics =
            [
                new DiagnosticInfo
                {
                    Id = "CS0103",
                    Severity = DiagnosticSeverity.Error,
                    Message = "The name 'x' does not exist in the current context",
                    Location = new Location { FilePath = "src/Foo.cs", Line = 10, Column = 5 }
                }
            ]
        };

        var result = OutputFormatter.FormatDiagnostics(response, OutputFormat.Text);

        Assert.NotNull(result);
        Assert.Equal("src/Foo.cs:10:5: error CS0103: The name 'x' does not exist in the current context", result);
    }

    [Fact]
    public void FormatDiagnostics_TextMode_DiagnosticWithoutLocation_IdColonMessage()
    {
        var response = new DiagnosticsResponse
        {
            Diagnostics =
            [
                new DiagnosticInfo
                {
                    Id = "CS5001",
                    Severity = DiagnosticSeverity.Error,
                    Message = "Program does not contain a static 'Main' method",
                    Location = null
                }
            ]
        };

        var result = OutputFormatter.FormatDiagnostics(response, OutputFormat.Text);

        Assert.NotNull(result);
        Assert.Equal("CS5001: Program does not contain a static 'Main' method", result);
    }

    [Fact]
    public void FormatDiagnostics_TextMode_WarningSeverity_Lowercase()
    {
        var response = new DiagnosticsResponse
        {
            Diagnostics =
            [
                new DiagnosticInfo
                {
                    Id = "CS0168",
                    Severity = DiagnosticSeverity.Warning,
                    Message = "The variable 'x' is declared but never used",
                    Location = new Location { FilePath = "src/Foo.cs", Line = 5, Column = 9 }
                }
            ]
        };

        var result = OutputFormatter.FormatDiagnostics(response, OutputFormat.Text);

        Assert.NotNull(result);
        Assert.Contains("warning", result);
        Assert.DoesNotContain("WARNING", result);
        Assert.DoesNotContain("Warning", result);
    }

    [Fact]
    public void FormatDiagnostics_TextMode_EmptyDiagnostics_ReturnsNull()
    {
        var response = new DiagnosticsResponse { Diagnostics = [] };

        var result = OutputFormatter.FormatDiagnostics(response, OutputFormat.Text);

        Assert.Null(result);
    }

    [Fact]
    public void FormatDiagnostics_TextMode_NullResponse_ReturnsNull()
    {
        var result = OutputFormatter.FormatDiagnostics(null, OutputFormat.Text);

        Assert.Null(result);
    }

    [Fact]
    public void FormatDiagnostics_TextMode_NoHeaderOrFooter()
    {
        var response = new DiagnosticsResponse
        {
            Diagnostics =
            [
                new DiagnosticInfo
                {
                    Id = "CS0103",
                    Severity = DiagnosticSeverity.Error,
                    Message = "msg",
                    Location = new Location { FilePath = "src/Foo.cs", Line = 1, Column = 1 }
                }
            ]
        };

        var result = OutputFormatter.FormatDiagnostics(response, OutputFormat.Text);

        Assert.NotNull(result);
        Assert.DoesNotContain("Found", result);
        Assert.DoesNotContain("Errors:", result);
        Assert.DoesNotContain("Warnings:", result);
    }

    [Fact]
    public void FormatDiagnostics_TextMode_MultipleDiagnostics_OneLineEach()
    {
        var response = new DiagnosticsResponse
        {
            Diagnostics =
            [
                new DiagnosticInfo
                {
                    Id = "CS0103",
                    Severity = DiagnosticSeverity.Error,
                    Message = "err1",
                    Location = new Location { FilePath = "a.cs", Line = 1, Column = 1 }
                },
                new DiagnosticInfo
                {
                    Id = "CS0168",
                    Severity = DiagnosticSeverity.Warning,
                    Message = "warn1",
                    Location = new Location { FilePath = "b.cs", Line = 2, Column = 3 }
                }
            ]
        };

        var result = OutputFormatter.FormatDiagnostics(response, OutputFormat.Text);

        Assert.NotNull(result);
        var lines = result!.Split(Environment.NewLine);
        Assert.Equal(2, lines.Length);
    }

    #endregion

    #region FormatSymbolInfo

    [Fact]
    public void FormatSymbolInfo_TextMode_NullResponse_ReturnsNull()
    {
        var result = OutputFormatter.FormatSymbolInfo(null, OutputFormat.Text);

        Assert.Null(result);
    }

    [Fact]
    public void FormatSymbolInfo_TextMode_ValidSymbol_ContainsName()
    {
        var response = new SymbolInfoResponse
        {
            Name = "MyMethod",
            FullName = "MyNamespace.MyClass.MyMethod",
            Kind = SymbolKind.Method
        };

        var result = OutputFormatter.FormatSymbolInfo(response, OutputFormat.Text);

        Assert.NotNull(result);
        Assert.Contains("MyMethod", result);
    }

    #endregion

    #region ResolveSolutionPath (via TestableCommand)

    [Fact]
    public void ResolveSolutionPath_EnvVarSet_ReturnsEnvVarPath()
    {
        var tmpFile = Path.GetTempFileName();
        var slnFile = Path.ChangeExtension(tmpFile, ".sln");
        File.Move(tmpFile, slnFile);

        try
        {
            Environment.SetEnvironmentVariable("ROSLYN_QUERY_SOLUTION", slnFile);
            var result = TestableCommand.ResolveForTest(null);
            Assert.Equal(Path.GetFullPath(slnFile), result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROSLYN_QUERY_SOLUTION", null);
            File.Delete(slnFile);
        }
    }

    [Fact]
    public void ResolveSolutionPath_ExplicitArgTakesPriorityOverEnvVar()
    {
        var tmpFile1 = Path.GetTempFileName();
        var slnFile1 = Path.ChangeExtension(tmpFile1, ".sln");
        File.Move(tmpFile1, slnFile1);

        var tmpFile2 = Path.GetTempFileName();
        var slnFile2 = Path.ChangeExtension(tmpFile2, ".sln");
        File.Move(tmpFile2, slnFile2);

        try
        {
            Environment.SetEnvironmentVariable("ROSLYN_QUERY_SOLUTION", slnFile2);
            var result = TestableCommand.ResolveForTest(slnFile1);
            Assert.Equal(Path.GetFullPath(slnFile1), result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROSLYN_QUERY_SOLUTION", null);
            File.Delete(slnFile1);
            File.Delete(slnFile2);
        }
    }

    [Fact]
    public void ResolveSolutionPath_EnvVarPointsToMissingFile_ThrowsFileNotFoundException()
    {
        Environment.SetEnvironmentVariable("ROSLYN_QUERY_SOLUTION", "/nonexistent/path/missing.sln");
        try
        {
            Assert.Throws<FileNotFoundException>(() => TestableCommand.ResolveForTest(null));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROSLYN_QUERY_SOLUTION", null);
        }
    }

    #endregion
}

internal class TestableCommand : CommandBase
{
    public TestableCommand() : base("test") { }
    public static string ResolveForTest(string? path) => ResolveSolutionPath(path);
}
