using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using RoslynQuery.Core.Contracts;
using RoslynQuery.Core.Contracts.Enums;
using RoslynQuery.Core.Contracts.Requests;
using RoslynQuery.Core.Contracts.Responses;
using RoslynQuery.Core.Utilities;
using RoslynQuery.Core.Workspace;

using SourceLocation = RoslynQuery.Core.Contracts.Responses.Location;
using RoslynLocation = Microsoft.CodeAnalysis.Location;
using SymbolKindEnum = RoslynQuery.Core.Contracts.Enums.SymbolKind;
using RefLocation = RoslynQuery.Core.Contracts.Responses.ReferenceLocation;
using RoslynRefLocation = Microsoft.CodeAnalysis.FindSymbols.ReferenceLocation;

namespace RoslynQuery.Core.Queries;

/// <summary>
/// Executes Roslyn-based code queries against a loaded solution.
/// </summary>
public class QueryExecutor : IQueryService
{
    private readonly SolutionManager _solutionManager;

    public QueryExecutor(SolutionManager solutionManager)
    {
        _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
    }

    public async Task<QueryResult<LocationResponse>> GetDefinitionAsync(
        DefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var (symbol, _) = await FindSymbolAtPositionAsync(request, cancellationToken);
        if (symbol == null)
        {
            return QueryResult<LocationResponse>.Fail(
                QueryErrorCode.SymbolNotFound,
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
                QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        // Find the base/interface definition
        ISymbol? baseSymbol = symbol switch
        {
            IMethodSymbol method => method.OverriddenMethod ?? GetInterfaceImplementation(method),
            IPropertySymbol property => property.OverriddenProperty ?? GetInterfaceImplementation(property),
            IEventSymbol evt => evt.OverriddenEvent ?? GetInterfaceImplementation(evt),
            _ => null
        };

        if (baseSymbol == null)
        {
            return QueryResult<LocationResponse>.Fail(
                QueryErrorCode.SymbolNotFound,
                "No base definition found for the symbol");
        }

        var location = GetSymbolDefinitionLocation(baseSymbol);

        return QueryResult<LocationResponse>.Ok(new LocationResponse
        {
            Location = location,
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
                QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        var solution = _solutionManager.GetSolution();
        var implementations = await SymbolFinder.FindImplementationsAsync(
            symbol, solution, cancellationToken: cancellationToken);

        var locations = implementations
            .Select(impl => CreateReferenceLocation(impl, GetSymbolDefinitionLocation(impl)))
            .Where(loc => loc.Location != null)
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
                QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        var solution = _solutionManager.GetSolution();
        var references = await SymbolFinder.FindReferencesAsync(
            symbol, solution, cancellationToken: cancellationToken);

        var locations = new List<RefLocation>();

        foreach (var reference in references)
        {
            foreach (var refLocation in reference.Locations)
            {
                var loc = await CreateReferenceLocationAsync(refLocation, cancellationToken);
                if (loc != null)
                {
                    locations.Add(loc);
                }
            }

            // Include definition if requested
            if (request.IncludeDefinition)
            {
                var defLoc = GetSymbolDefinitionLocation(reference.Definition);
                if (defLoc != null)
                {
                    locations.Insert(0, CreateReferenceLocation(reference.Definition, defLoc));
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
                QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        var solution = _solutionManager.GetSolution();
        var callers = await SymbolFinder.FindCallersAsync(
            symbol, solution, cancellationToken: cancellationToken);

        var locations = new List<RefLocation>();

        foreach (var caller in callers)
        {
            foreach (var callLocation in caller.Locations)
            {
                var loc = CreateReferenceLocationFromSourceLocation(
                    callLocation,
                    caller.CallingSymbol.Name,
                    caller.CallingSymbol.ContainingType?.Name);

                if (loc != null)
                {
                    locations.Add(loc);
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
        if (symbol == null || document == null)
        {
            return QueryResult<LocationsResponse>.Fail(
                QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        // Get the syntax node for the method/property body
        var syntaxRefs = symbol.DeclaringSyntaxReferences;
        if (syntaxRefs.Length == 0)
        {
            return QueryResult<LocationsResponse>.Ok(new LocationsResponse
            {
                SymbolName = symbol.Name,
                Locations = []
            });
        }

        var locations = new List<RefLocation>();

        foreach (var syntaxRef in syntaxRefs)
        {
            var syntaxNode = await syntaxRef.GetSyntaxAsync(cancellationToken);
            var syntaxTree = syntaxNode.SyntaxTree;

            // Find the document containing this syntax tree
            var doc = _solutionManager.GetSolution().GetDocument(syntaxTree);
            if (doc == null) continue;

            var semanticModel = await doc.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null) continue;

            // Collect callees
            var collector = new CalleeCollector(semanticModel);
            collector.Visit(syntaxNode);

            foreach (var (calleeSymbol, calleeNode) in collector.Callees)
            {
                var loc = GetSymbolDefinitionLocation(calleeSymbol);
                if (loc != null)
                {
                    locations.Add(new RefLocation
                    {
                        Location = loc,
                        ContainingMember = symbol.Name,
                        ContainingType = symbol.ContainingType?.Name
                    });
                }
            }
        }

        // Deduplicate by location
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
                QueryErrorCode.SymbolNotFound,
                "No symbol found at the specified position");
        }

        var location = GetSymbolDefinitionLocation(symbol);
        var documentation = symbol.GetDocumentationCommentXml();

        // Parse summary from XML documentation
        string? summary = null;
        if (!string.IsNullOrEmpty(documentation))
        {
            var summaryStart = documentation.IndexOf("<summary>", StringComparison.Ordinal);
            var summaryEnd = documentation.IndexOf("</summary>", StringComparison.Ordinal);
            if (summaryStart >= 0 && summaryEnd > summaryStart)
            {
                summary = documentation.Substring(summaryStart + 9, summaryEnd - summaryStart - 9).Trim();
            }
        }

        return QueryResult<SymbolInfoResponse>.Ok(new SymbolInfoResponse
        {
            Name = symbol.Name,
            FullName = symbol.ToDisplayString(),
            Kind = MapSymbolKind(symbol.Kind),
            Signature = GetSignature(symbol),
            Documentation = summary,
            ContainingType = symbol.ContainingType?.Name,
            ContainingNamespace = symbol.ContainingNamespace?.ToDisplayString(),
            ReturnType = GetReturnType(symbol),
            Accessibility = symbol.DeclaredAccessibility.ToString().ToLowerInvariant(),
            Modifiers = GetModifiers(symbol),
            Location = location
        });
    }

    public async Task<QueryResult<DiagnosticsResponse>> GetDiagnosticsAsync(
        DiagnosticsRequest request,
        CancellationToken cancellationToken = default)
    {
        var solution = _solutionManager.GetSolution();
        var allDiagnostics = new List<DiagnosticInfo>();

        if (!string.IsNullOrEmpty(request.FilePath))
        {
            // Get diagnostics for a specific file
            var document = _solutionManager.FindDocument(request.FilePath);
            if (document == null)
            {
                return QueryResult<DiagnosticsResponse>.Fail(
                    QueryErrorCode.DocumentNotFound,
                    $"Document not found: {request.FilePath}");
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel != null)
            {
                var diagnostics = semanticModel.GetDiagnostics(cancellationToken: cancellationToken);
                allDiagnostics.AddRange(FilterAndMapDiagnostics(diagnostics, request));
            }
        }
        else
        {
            // Get diagnostics for entire solution
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation != null)
                {
                    var diagnostics = compilation.GetDiagnostics(cancellationToken);
                    allDiagnostics.AddRange(FilterAndMapDiagnostics(diagnostics, request));
                }
            }
        }

        return QueryResult<DiagnosticsResponse>.Ok(new DiagnosticsResponse
        {
            Diagnostics = allDiagnostics,
            ErrorCount = allDiagnostics.Count(d => d.Severity == Contracts.Responses.DiagnosticSeverity.Error),
            WarningCount = allDiagnostics.Count(d => d.Severity == Contracts.Responses.DiagnosticSeverity.Warning),
            InfoCount = allDiagnostics.Count(d => d.Severity == Contracts.Responses.DiagnosticSeverity.Info)
        });
    }

    private async Task<(ISymbol? Symbol, Document? Document)> FindSymbolAtPositionAsync(
        PositionRequest request,
        CancellationToken cancellationToken)
    {
        var document = _solutionManager.FindDocument(request.FilePath);
        if (document == null)
        {
            return (null, null);
        }

        var sourceText = await document.GetTextAsync(cancellationToken);
        var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));

        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position, cancellationToken);

        return (symbol, document);
    }

    private SourceLocation? GetSymbolDefinitionLocation(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (location == null)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        var filePath = PathResolver.ToRelativePath(lineSpan.Path, _solutionManager.SolutionRoot);

        return new SourceLocation
        {
            FilePath = filePath,
            Line = lineSpan.StartLinePosition.Line + 1,
            Column = lineSpan.StartLinePosition.Character + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            EndColumn = lineSpan.EndLinePosition.Character + 1
        };
    }

    private RefLocation CreateReferenceLocation(ISymbol symbol, SourceLocation? location)
    {
        return new RefLocation
        {
            Location = location ?? new SourceLocation { FilePath = "", Line = 0, Column = 0 },
            ContainingMember = symbol.ContainingSymbol?.Name,
            ContainingType = symbol.ContainingType?.Name
        };
    }

    private async Task<RefLocation?> CreateReferenceLocationAsync(
        RoslynRefLocation refLocation,
        CancellationToken cancellationToken)
    {
        if (!refLocation.Location.IsInSource)
        {
            return null;
        }

        var lineSpan = refLocation.Location.GetLineSpan();
        var filePath = PathResolver.ToRelativePath(lineSpan.Path, _solutionManager.SolutionRoot);

        // Get snippet
        string? snippet = null;
        var document = refLocation.Document;
        var sourceText = await document.GetTextAsync(cancellationToken);
        var line = sourceText.Lines[lineSpan.StartLinePosition.Line];
        snippet = line.ToString().Trim();

        // Get containing member
        var tree = await document.GetSyntaxTreeAsync(cancellationToken);
        var root = await tree!.GetRootAsync(cancellationToken);
        var node = root.FindNode(refLocation.Location.SourceSpan);
        var containingMember = node.Ancestors()
            .OfType<MemberDeclarationSyntax>()
            .FirstOrDefault();

        string? memberName = containingMember switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            _ => null
        };

        var containingType = containingMember?.Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault()?.Identifier.Text;

        return new RefLocation
        {
            Location = new SourceLocation
            {
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                EndLine = lineSpan.EndLinePosition.Line + 1,
                EndColumn = lineSpan.EndLinePosition.Character + 1
            },
            Snippet = snippet,
            ContainingMember = memberName,
            ContainingType = containingType
        };
    }

    private RefLocation? CreateReferenceLocationFromSourceLocation(
        RoslynLocation location,
        string? memberName,
        string? typeName)
    {
        if (!location.IsInSource)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        var filePath = PathResolver.ToRelativePath(lineSpan.Path, _solutionManager.SolutionRoot);

        return new RefLocation
        {
            Location = new SourceLocation
            {
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                EndLine = lineSpan.EndLinePosition.Line + 1,
                EndColumn = lineSpan.EndLinePosition.Character + 1
            },
            ContainingMember = memberName,
            ContainingType = typeName
        };
    }

    private static ISymbol? GetInterfaceImplementation(ISymbol symbol)
    {
        var containingType = symbol.ContainingType;
        if (containingType == null)
        {
            return null;
        }

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                var impl = containingType.FindImplementationForInterfaceMember(member);
                if (SymbolEqualityComparer.Default.Equals(impl, symbol))
                {
                    return member;
                }
            }
        }

        return null;
    }

    private static SymbolKindEnum MapSymbolKind(Microsoft.CodeAnalysis.SymbolKind kind) => kind switch
    {
        Microsoft.CodeAnalysis.SymbolKind.Namespace => SymbolKindEnum.Namespace,
        Microsoft.CodeAnalysis.SymbolKind.NamedType => SymbolKindEnum.Type,
        Microsoft.CodeAnalysis.SymbolKind.Method => SymbolKindEnum.Method,
        Microsoft.CodeAnalysis.SymbolKind.Property => SymbolKindEnum.Property,
        Microsoft.CodeAnalysis.SymbolKind.Field => SymbolKindEnum.Field,
        Microsoft.CodeAnalysis.SymbolKind.Event => SymbolKindEnum.Event,
        Microsoft.CodeAnalysis.SymbolKind.Parameter => SymbolKindEnum.Parameter,
        Microsoft.CodeAnalysis.SymbolKind.Local => SymbolKindEnum.Local,
        Microsoft.CodeAnalysis.SymbolKind.TypeParameter => SymbolKindEnum.TypeParameter,
        _ => SymbolKindEnum.Unknown
    };

    private static string? GetSignature(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        IPropertySymbol property => property.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        IFieldSymbol field => field.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        _ => symbol.ToDisplayString()
    };

    private static string? GetReturnType(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        IPropertySymbol property => property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        IFieldSymbol field => field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        _ => null
    };

    private static IReadOnlyList<string> GetModifiers(ISymbol symbol)
    {
        var modifiers = new List<string>();

        if (symbol.IsStatic) modifiers.Add("static");
        if (symbol.IsAbstract) modifiers.Add("abstract");
        if (symbol.IsVirtual) modifiers.Add("virtual");
        if (symbol.IsOverride) modifiers.Add("override");
        if (symbol.IsSealed) modifiers.Add("sealed");

        if (symbol is IMethodSymbol { IsAsync: true }) modifiers.Add("async");

        return modifiers;
    }

    private IEnumerable<DiagnosticInfo> FilterAndMapDiagnostics(
        IEnumerable<Diagnostic> diagnostics,
        DiagnosticsRequest request)
    {
        foreach (var diagnostic in diagnostics)
        {
            var severity = diagnostic.Severity switch
            {
                Microsoft.CodeAnalysis.DiagnosticSeverity.Error => Contracts.Responses.DiagnosticSeverity.Error,
                Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => Contracts.Responses.DiagnosticSeverity.Warning,
                Microsoft.CodeAnalysis.DiagnosticSeverity.Info => Contracts.Responses.DiagnosticSeverity.Info,
                _ => Contracts.Responses.DiagnosticSeverity.Hidden
            };

            // Filter based on request
            if (severity == Contracts.Responses.DiagnosticSeverity.Warning && !request.IncludeWarnings)
                continue;
            if (severity == Contracts.Responses.DiagnosticSeverity.Info && !request.IncludeInfo)
                continue;
            if (severity == Contracts.Responses.DiagnosticSeverity.Hidden)
                continue;

            SourceLocation? location = null;
            if (diagnostic.Location.IsInSource)
            {
                var lineSpan = diagnostic.Location.GetLineSpan();
                location = new SourceLocation
                {
                    FilePath = PathResolver.ToRelativePath(lineSpan.Path, _solutionManager.SolutionRoot),
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1
                };
            }

            yield return new DiagnosticInfo
            {
                Id = diagnostic.Id,
                Severity = severity,
                Message = diagnostic.GetMessage(),
                Location = location
            };
        }
    }
}
