using RoslynQuery.Core.Utilities;

namespace RoslynQuery.Core.Tests.Unit;

public class PathResolverTests
{
    #region ToRelativePath

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
    public void ToRelativePath_NullAbsolutePath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PathResolver.ToRelativePath(null!, "/root"));
    }

    [Fact]
    public void ToRelativePath_NullRoot_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PathResolver.ToRelativePath("/file.cs", null!));
    }

    #endregion

    #region ToAbsolutePath

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
    public void ToAbsolutePath_NullRelativePath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PathResolver.ToAbsolutePath(null!, "/root"));
    }

    [Fact]
    public void ToAbsolutePath_NullRoot_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PathResolver.ToAbsolutePath("file.cs", null!));
    }

    #endregion

    #region NormalizePath

    [Fact]
    public void NormalizePath_WithMixedSeparators_ReturnsNormalizedPath()
    {
        var path = "/home/user/project\\src/MyClass.cs";

        var normalized = PathResolver.NormalizePath(path);

        Assert.DoesNotContain("\\", normalized);
    }

    [Fact]
    public void NormalizePath_EmptyString_ReturnsEmptyString()
    {
        var result = PathResolver.NormalizePath("");

        Assert.Equal("", result);
    }

    [Fact]
    public void NormalizePath_Null_ReturnsNull()
    {
        var result = PathResolver.NormalizePath(null!);

        Assert.Null(result);
    }

    #endregion

    #region GetSocketPath

    [Fact]
    public void GetSocketPath_ReturnsValidSocketPath()
    {
        var solutionPath = "/home/user/project/MySolution.sln";

        var socketPath = PathResolver.GetSocketPath(solutionPath);

        Assert.EndsWith(".sock", socketPath);
        Assert.Contains("roslyn-query-", socketPath);
    }

    [Fact]
    public void GetSocketPath_SameSolution_ReturnsSamePath()
    {
        var socketPath1 = PathResolver.GetSocketPath("/home/user/project/MySolution.sln");
        var socketPath2 = PathResolver.GetSocketPath("/home/user/project/MySolution.sln");

        Assert.Equal(socketPath1, socketPath2);
    }

    [Fact]
    public void GetSocketPath_DifferentSolutions_ReturnsDifferentPaths()
    {
        var socketPath1 = PathResolver.GetSocketPath("/home/user/project1/MySolution.sln");
        var socketPath2 = PathResolver.GetSocketPath("/home/user/project2/MySolution.sln");

        Assert.NotEqual(socketPath1, socketPath2);
    }

    [Fact]
    public void GetSocketPath_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PathResolver.GetSocketPath(null!));
    }

    #endregion

    #region GetPidFilePath

    [Fact]
    public void GetPidFilePath_ReturnsValidPidPath()
    {
        var solutionPath = "/home/user/project/MySolution.sln";

        var pidPath = PathResolver.GetPidFilePath(solutionPath);

        Assert.EndsWith(".pid", pidPath);
        Assert.Contains("roslyn-query-", pidPath);
    }

    [Fact]
    public void GetPidFilePath_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PathResolver.GetPidFilePath(null!));
    }

    #endregion

    #region GetSolutionRoot

    [Fact]
    public void GetSolutionRoot_ReturnsParentDirectory()
    {
        var solutionPath = "/home/user/project/MySolution.sln";

        var root = PathResolver.GetSolutionRoot(solutionPath);

        Assert.Equal("/home/user/project", root);
    }

    [Fact]
    public void GetSolutionRoot_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PathResolver.GetSolutionRoot(null!));
    }

    #endregion

    #region Trailing slash handling

    [Fact]
    public void ToRelativePath_RootWithTrailingSlash_StillWorks()
    {
        var root = "/home/user/project/";
        var absolute = "/home/user/project/src/MyClass.cs";

        var relative = PathResolver.ToRelativePath(absolute, root);

        Assert.Equal("src/MyClass.cs", relative);
    }

    [Fact]
    public void ToRelativePath_RootWithoutTrailingSlash_StillWorks()
    {
        var root = "/home/user/project";
        var absolute = "/home/user/project/src/MyClass.cs";

        var relative = PathResolver.ToRelativePath(absolute, root);

        Assert.Equal("src/MyClass.cs", relative);
    }

    #endregion
}
