using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using RoslynQuery.Core.Contracts;
using RoslynQuery.Core.Contracts.Requests;
using RoslynQuery.Core.Contracts.Responses;
using RoslynQuery.Core.Utilities;

using SourceLocation = RoslynQuery.Core.Contracts.Responses.Location;
using ResponseDiagnosticSeverity = RoslynQuery.Core.Contracts.Responses.DiagnosticSeverity;

namespace RoslynQuery.Core.Tests.Integration;

/// <summary>
/// Helper for creating test solutions with AdhocWorkspace for integration testing.
/// </summary>
public sealed class TestSolutionBuilder : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly string _tempDir;
    private readonly Dictionary<string, DocumentId> _documents = new();
    private ProjectId? _projectId;

    public TestSolutionBuilder()
    {
        _workspace = new AdhocWorkspace();
        _tempDir = Path.Combine(Path.GetTempPath(), $"roslyn-query-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public string SolutionRoot => _tempDir;

    public TestSolutionBuilder AddDocument(string fileName, string code)
    {
        if (_projectId == null)
        {
            _projectId = ProjectId.CreateNewId();
            var projectInfo = ProjectInfo.Create(
                _projectId,
                VersionStamp.Create(),
                "TestProject",
                "TestProject",
                LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                metadataReferences:
                [
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location)
                ]);

            _workspace.AddProject(projectInfo);
        }

        var documentId = DocumentId.CreateNewId(_projectId);
        var filePath = Path.Combine(_tempDir, fileName);

        var documentInfo = DocumentInfo.Create(
            documentId,
            fileName,
            filePath: filePath,
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(code), VersionStamp.Create())));

        _workspace.AddDocument(documentInfo);
        _documents[fileName] = documentId;

        // Write file to disk for path resolution
        File.WriteAllText(filePath, code);

        return this;
    }

    public Solution Solution => _workspace.CurrentSolution;

    public TestQueryService Build()
    {
        return new TestQueryService(_workspace.CurrentSolution, _tempDir);
    }

    public void Dispose()
    {
        _workspace.Dispose();
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }
    }
}

/// <summary>
/// Query service implementation for testing using AdhocWorkspace.
/// </summary>
public class TestQueryService : IQueryService
{
    private readonly Solution _solution;
    private readonly string _solutionRoot;

    public TestQueryService(Solution solution, string solutionRoot)
    {
        _solution = solution;
        _solutionRoot = solutionRoot;
    }

    public async Task<QueryResult<LocationResponse>> GetDefinitionAsync(
        DefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (symbol, _) = await FindSymbolAtPositionAsync(request, cancellationToken);
        if (symbol == null)
        {
            return QueryResult<LocationResponse>.Fail(
                Core.Contracts.Enums.QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        var location = GetSymbolDefinitionLocation(symbol);

        return QueryResult<LocationResponse>.Ok(new LocationResponse
        {
            Location = location,
            SymbolName = symbol.Name,
            SymbolKind = symbol.Kind.ToString()
        });
    }

    public async Task<QueryResult<LocationResponse>> GetBaseDefinitionAsync(
        BaseDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (symbol, _) = await FindSymbolAtPositionAsync(request, cancellationToken);
        if (symbol == null)
        {
            return QueryResult<LocationResponse>.Fail(
                Core.Contracts.Enums.QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        ISymbol? baseSymbol = symbol switch
        {
            IMethodSymbol method => method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            _ => null
        };

        if (baseSymbol == null)
        {
            return QueryResult<LocationResponse>.Fail(
                Core.Contracts.Enums.QueryErrorCode.SymbolNotFound,
                "No base definition found");
        }

        return QueryResult<LocationResponse>.Ok(new LocationResponse
        {
            Location = GetSymbolDefinitionLocation(baseSymbol),
            SymbolName = baseSymbol.Name,
            SymbolKind = baseSymbol.Kind.ToString()
        });
    }

    public async Task<QueryResult<LocationsResponse>> GetImplementationsAsync(
        ImplementationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var (symbol, _) = await FindSymbolAtPositionAsync(request, cancellationToken);
        if (symbol == null)
        {
            return QueryResult<LocationsResponse>.Fail(
                Core.Contracts.Enums.QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        var implementations = await Microsoft.CodeAnalysis.FindSymbols.SymbolFinder
            .FindImplementationsAsync(symbol, _solution, cancellationToken: cancellationToken);

        var locations = implementations
            .Select(impl => new ReferenceLocation
            {
                Location = GetSymbolDefinitionLocation(impl) ?? new SourceLocation { FilePath = "", Line = 0, Column = 0 },
                ContainingType = impl.ContainingType?.Name
            })
            .ToList();

        return QueryResult<LocationsResponse>.Ok(new LocationsResponse
        {
            SymbolName = symbol.Name,
            Locations = locations
        });
    }

    public async Task<QueryResult<LocationsResponse>> GetReferencesAsync(
        ReferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var (symbol, _) = await FindSymbolAtPositionAsync(request, cancellationToken);
        if (symbol == null)
        {
            return QueryResult<LocationsResponse>.Fail(
                Core.Contracts.Enums.QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        var references = await Microsoft.CodeAnalysis.FindSymbols.SymbolFinder
            .FindReferencesAsync(symbol, _solution, cancellationToken: cancellationToken);

        var locations = new List<ReferenceLocation>();
        foreach (var reference in references)
        {
            foreach (var loc in reference.Locations)
            {
                if (loc.Location.IsInSource)
                {
                    var lineSpan = loc.Location.GetLineSpan();
                    locations.Add(new ReferenceLocation
                    {
                        Location = new SourceLocation
                        {
                            FilePath = PathResolver.ToRelativePath(lineSpan.Path, _solutionRoot),
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1
                        }
                    });
                }
            }
        }

        return QueryResult<LocationsResponse>.Ok(new LocationsResponse
        {
            SymbolName = symbol.Name,
            Locations = locations
        });
    }

    public async Task<QueryResult<LocationsResponse>> GetCallersAsync(
        CallersRequest request,
        CancellationToken cancellationToken = default)
    {
        var (symbol, _) = await FindSymbolAtPositionAsync(request, cancellationToken);
        if (symbol == null)
        {
            return QueryResult<LocationsResponse>.Fail(
                Core.Contracts.Enums.QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        var callers = await Microsoft.CodeAnalysis.FindSymbols.SymbolFinder
            .FindCallersAsync(symbol, _solution, cancellationToken: cancellationToken);

        var locations = new List<ReferenceLocation>();
        foreach (var caller in callers)
        {
            foreach (var loc in caller.Locations)
            {
                if (loc.IsInSource)
                {
                    var lineSpan = loc.GetLineSpan();
                    locations.Add(new ReferenceLocation
                    {
                        Location = new SourceLocation
                        {
                            FilePath = PathResolver.ToRelativePath(lineSpan.Path, _solutionRoot),
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1
                        },
                        ContainingMember = caller.CallingSymbol.Name
                    });
                }
            }
        }

        return QueryResult<LocationsResponse>.Ok(new LocationsResponse
        {
            SymbolName = symbol.Name,
            Locations = locations
        });
    }

    public async Task<QueryResult<LocationsResponse>> GetCalleesAsync(
        CalleesRequest request,
        CancellationToken cancellationToken = default)
    {
        var (symbol, document) = await FindSymbolAtPositionAsync(request, cancellationToken);
        if (symbol == null)
        {
            return QueryResult<LocationsResponse>.Fail(
                Core.Contracts.Enums.QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        var syntaxRefs = symbol.DeclaringSyntaxReferences;
        var locations = new List<ReferenceLocation>();

        foreach (var syntaxRef in syntaxRefs)
        {
            var syntaxNode = await syntaxRef.GetSyntaxAsync(cancellationToken);
            var doc = _solution.GetDocument(syntaxNode.SyntaxTree);
            if (doc == null) continue;

            var semanticModel = await doc.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null) continue;

            var collector = new Core.Queries.CalleeCollector(semanticModel);
            collector.Visit(syntaxNode);

            foreach (var (calleeSymbol, _) in collector.Callees)
            {
                var loc = GetSymbolDefinitionLocation(calleeSymbol);
                if (loc != null)
                {
                    locations.Add(new ReferenceLocation
                    {
                        Location = loc,
                        ContainingMember = symbol.Name
                    });
                }
            }
        }

        var distinctLocations = locations
            .GroupBy(l => l.Location?.ToString())
            .Select(g => g.First())
            .ToList();

        return QueryResult<LocationsResponse>.Ok(new LocationsResponse
        {
            SymbolName = symbol.Name,
            Locations = distinctLocations
        });
    }

    public async Task<QueryResult<SymbolInfoResponse>> GetSymbolInfoAsync(
        SymbolRequest request,
        CancellationToken cancellationToken = default)
    {
        var (symbol, _) = await FindSymbolAtPositionAsync(request, cancellationToken);
        if (symbol == null)
        {
            return QueryResult<SymbolInfoResponse>.Fail(
                Core.Contracts.Enums.QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        var modifiers = new List<string>();
        if (symbol.IsStatic) modifiers.Add("static");
        if (symbol.IsAbstract) modifiers.Add("abstract");
        if (symbol.IsVirtual) modifiers.Add("virtual");
        if (symbol is IMethodSymbol { IsAsync: true }) modifiers.Add("async");

        return QueryResult<SymbolInfoResponse>.Ok(new SymbolInfoResponse
        {
            Name = symbol.Name,
            FullName = symbol.ToDisplayString(),
            Kind = Core.Contracts.Enums.SymbolKind.Method,
            Modifiers = modifiers,
            Location = GetSymbolDefinitionLocation(symbol)
        });
    }

    public async Task<QueryResult<DiagnosticsResponse>> GetDiagnosticsAsync(
        DiagnosticsRequest request,
        CancellationToken cancellationToken = default)
    {
        var allDiagnostics = new List<DiagnosticInfo>();

        foreach (var project in _solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation == null) continue;

            foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
            {
                if (diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden) continue;

                SourceLocation? location = null;
                if (diagnostic.Location.IsInSource)
                {
                    var lineSpan = diagnostic.Location.GetLineSpan();
                    location = new SourceLocation
                    {
                        FilePath = PathResolver.ToRelativePath(lineSpan.Path, _solutionRoot),
                        Line = lineSpan.StartLinePosition.Line + 1,
                        Column = lineSpan.StartLinePosition.Character + 1
                    };
                }

                allDiagnostics.Add(new DiagnosticInfo
                {
                    Id = diagnostic.Id,
                    Severity = diagnostic.Severity switch
                    {
                        Microsoft.CodeAnalysis.DiagnosticSeverity.Error => ResponseDiagnosticSeverity.Error,
                        Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => ResponseDiagnosticSeverity.Warning,
                        _ => ResponseDiagnosticSeverity.Info
                    },
                    Message = diagnostic.GetMessage(),
                    Location = location
                });
            }
        }

        return QueryResult<DiagnosticsResponse>.Ok(new DiagnosticsResponse
        {
            Diagnostics = allDiagnostics,
            ErrorCount = allDiagnostics.Count(d => d.Severity == ResponseDiagnosticSeverity.Error),
            WarningCount = allDiagnostics.Count(d => d.Severity == ResponseDiagnosticSeverity.Warning),
            InfoCount = allDiagnostics.Count(d => d.Severity == ResponseDiagnosticSeverity.Info)
        });
    }

    private async Task<(ISymbol? Symbol, Document? Document)> FindSymbolAtPositionAsync(
        PositionRequest request,
        CancellationToken cancellationToken)
    {
        Document? document = null;
        foreach (var project in _solution.Projects)
        {
            foreach (var doc in project.Documents)
            {
                if (doc.Name.Equals(Path.GetFileName(request.FilePath), StringComparison.OrdinalIgnoreCase))
                {
                    document = doc;
                    break;
                }
            }
            if (document != null) break;
        }

        if (document == null)
        {
            return (null, null);
        }

        var sourceText = await document.GetTextAsync(cancellationToken);
        var position = sourceText.Lines.GetPosition(
            new Microsoft.CodeAnalysis.Text.LinePosition(request.Line - 1, request.Column - 1));

        var symbol = await Microsoft.CodeAnalysis.FindSymbols.SymbolFinder
            .FindSymbolAtPositionAsync(document, position, cancellationToken);

        return (symbol, document);
    }

    private SourceLocation? GetSymbolDefinitionLocation(ISymbol symbol)
    {
        var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc == null) return null;

        var lineSpan = loc.GetLineSpan();
        return new SourceLocation
        {
            FilePath = PathResolver.ToRelativePath(lineSpan.Path, _solutionRoot),
            Line = lineSpan.StartLinePosition.Line + 1,
            Column = lineSpan.StartLinePosition.Character + 1
        };
    }
}
