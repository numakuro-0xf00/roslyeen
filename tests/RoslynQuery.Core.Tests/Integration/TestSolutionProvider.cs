using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using RoslynQuery.Core.Utilities;
using RoslynQuery.Core.Workspace;

namespace RoslynQuery.Core.Tests.Integration;

/// <summary>
/// ISolutionProvider implementation backed by AdhocWorkspace for integration testing.
/// Uses the same path normalization logic as the production SolutionManager.
/// </summary>
public sealed class TestSolutionProvider : ISolutionProvider, IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly string _solutionRoot;
    private ProjectId? _projectId;

    public TestSolutionProvider()
    {
        _workspace = new AdhocWorkspace();
        _solutionRoot = Path.Combine(Path.GetTempPath(), $"roslyn-query-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_solutionRoot);
    }

    public string SolutionRoot => _solutionRoot;

    public TestSolutionProvider AddDocument(string fileName, string code)
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
        var filePath = Path.Combine(_solutionRoot, fileName);

        var documentInfo = DocumentInfo.Create(
            documentId,
            fileName,
            filePath: filePath,
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(code), VersionStamp.Create())));

        _workspace.AddDocument(documentInfo);

        // Write file to disk for path resolution
        var dir = Path.GetDirectoryName(filePath);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, code);

        return this;
    }

    public Solution GetSolution() => _workspace.CurrentSolution;

    public Document? FindDocument(string filePath)
    {
        var absolutePath = PathResolver.ToAbsolutePath(filePath, _solutionRoot);

        foreach (var project in _workspace.CurrentSolution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (document.FilePath != null &&
                    string.Equals(PathResolver.NormalizePath(document.FilePath),
                                 PathResolver.NormalizePath(absolutePath),
                                 StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }
        }

        return null;
    }

    public IEnumerable<Project> GetProjects() =>
        _workspace.CurrentSolution.Projects;

    public void Dispose()
    {
        _workspace.Dispose();
        if (Directory.Exists(_solutionRoot))
        {
            try
            {
                Directory.Delete(_solutionRoot, recursive: true);
            }
            catch { }
        }
    }
}
