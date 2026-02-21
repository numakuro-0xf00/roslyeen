using RoslynQuery.Core.Utilities;

namespace RoslynQuery.Core.Tests.Unit;

public class PathResolverTests
{
    [Fact]
    public void ToRelativePath_WithFileUnderRoot_ReturnsRelativePath()
    {
        var root = "/home/user/project";
        var absolute = "/home/user/project/src/MyClass.cs";

        var relative = PathResolver.ToRelativePath(absolute, root);

        Assert.Equal("src/MyClass.cs", relative);
    }

    [Fact]
    public void ToRelativePath_WithFileNotUnderRoot_ReturnsOriginalPath()
    {
        var root = "/home/user/project";
        var absolute = "/other/path/MyClass.cs";

        var relative = PathResolver.ToRelativePath(absolute, root);

        Assert.Equal("/other/path/MyClass.cs", relative);
    }

    [Fact]
    public void ToAbsolutePath_WithRelativePath_ReturnsAbsolutePath()
    {
        var root = "/home/user/project";
        var relative = "src/MyClass.cs";

        var absolute = PathResolver.ToAbsolutePath(relative, root);

        Assert.Equal("/home/user/project/src/MyClass.cs", absolute);
    }

    [Fact]
    public void ToAbsolutePath_WithAbsolutePath_ReturnsSamePath()
    {
        var root = "/home/user/project";
        var absolute = "/other/path/MyClass.cs";

        var result = PathResolver.ToAbsolutePath(absolute, root);

        Assert.Equal("/other/path/MyClass.cs", result);
    }

    [Fact]
    public void NormalizePath_WithMixedSeparators_ReturnsNormalizedPath()
    {
        var path = "/home/user/project\\src/MyClass.cs";

        var normalized = PathResolver.NormalizePath(path);

        Assert.DoesNotContain("\\", normalized);
    }

    [Fact]
    public void GetSocketPath_ReturnsValidSocketPath()
    {
        var solutionPath = "/home/user/project/MySolution.sln";

        var socketPath = PathResolver.GetSocketPath(solutionPath);

        Assert.EndsWith(".sock", socketPath);
        Assert.Contains("roslyn-query-", socketPath);
    }

    [Fact]
    public void GetPidFilePath_ReturnsValidPidPath()
    {
        var solutionPath = "/home/user/project/MySolution.sln";

        var pidPath = PathResolver.GetPidFilePath(solutionPath);

        Assert.EndsWith(".pid", pidPath);
        Assert.Contains("roslyn-query-", pidPath);
    }

    [Fact]
    public void GetSocketPath_SameSolution_ReturnsSamePath()
    {
        var solutionPath1 = "/home/user/project/MySolution.sln";
        var solutionPath2 = "/home/user/project/MySolution.sln";

        var socketPath1 = PathResolver.GetSocketPath(solutionPath1);
        var socketPath2 = PathResolver.GetSocketPath(solutionPath2);

        Assert.Equal(socketPath1, socketPath2);
    }

    [Fact]
    public void GetSocketPath_DifferentSolutions_ReturnsDifferentPaths()
    {
        var solutionPath1 = "/home/user/project1/MySolution.sln";
        var solutionPath2 = "/home/user/project2/MySolution.sln";

        var socketPath1 = PathResolver.GetSocketPath(solutionPath1);
        var socketPath2 = PathResolver.GetSocketPath(solutionPath2);

        Assert.NotEqual(socketPath1, socketPath2);
    }

    [Fact]
    public void GetSolutionRoot_ReturnsParentDirectory()
    {
        var solutionPath = "/home/user/project/MySolution.sln";

        var root = PathResolver.GetSolutionRoot(solutionPath);

        Assert.Equal("/home/user/project", root);
    }
}
