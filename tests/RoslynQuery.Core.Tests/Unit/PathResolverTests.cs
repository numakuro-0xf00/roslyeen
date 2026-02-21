using RoslynQuery.Core.Utilities;

namespace RoslynQuery.Core.Tests.Unit;

public class PathResolverTests
{
    // Build platform-appropriate test paths using temp directory as a stable rooted base.
    private static readonly string TempBase = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
    private static readonly string ProjectRoot = Path.Combine(TempBase, "user", "project");
    private static readonly string FileUnderRoot = Path.Combine(ProjectRoot, "src", "MyClass.cs");
    private static readonly string OtherRoot = Path.Combine(TempBase, "other");
    private static readonly string FileOutsideRoot = Path.Combine(OtherRoot, "path", "MyClass.cs");
    private static readonly string SolutionFile = Path.Combine(ProjectRoot, "MySolution.sln");

    #region ToRelativePath

    [Fact]
    public void ToRelativePath_WithFileUnderRoot_ReturnsRelativePath()
    {
        var relative = PathResolver.ToRelativePath(FileUnderRoot, ProjectRoot);

        Assert.Equal(Path.Combine("src", "MyClass.cs"), relative);
    }

    [Fact]
    public void ToRelativePath_WithFileNotUnderRoot_ReturnsOriginalPath()
    {
        var relative = PathResolver.ToRelativePath(FileOutsideRoot, ProjectRoot);

        // When not under root, the original (unnormalized) input is returned
        Assert.Equal(FileOutsideRoot, relative);
    }

    [Fact]
    public void ToRelativePath_NullAbsolutePath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PathResolver.ToRelativePath(null!, ProjectRoot));
    }

    [Fact]
    public void ToRelativePath_NullRoot_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PathResolver.ToRelativePath(FileUnderRoot, null!));
    }

    #endregion

    #region ToAbsolutePath

    [Fact]
    public void ToAbsolutePath_WithRelativePath_ReturnsAbsolutePath()
    {
        var relative = Path.Combine("src", "MyClass.cs");

        var absolute = PathResolver.ToAbsolutePath(relative, ProjectRoot);

        Assert.Equal(PathResolver.NormalizePath(FileUnderRoot), absolute);
    }

    [Fact]
    public void ToAbsolutePath_WithAbsolutePath_ReturnsSamePath()
    {
        var result = PathResolver.ToAbsolutePath(FileOutsideRoot, ProjectRoot);

        Assert.Equal(PathResolver.NormalizePath(FileOutsideRoot), result);
    }

    [Fact]
    public void ToAbsolutePath_NullRelativePath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PathResolver.ToAbsolutePath(null!, ProjectRoot));
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
    public void NormalizePath_WithMixedSeparators_UnifiestoNativeSeparator()
    {
        // Build a path with the non-native separator mixed in
        var nativeSep = Path.DirectorySeparatorChar;
        var foreignSep = nativeSep == '/' ? '\\' : '/';
        var path = ProjectRoot + foreignSep + "src" + nativeSep + "MyClass.cs";

        var normalized = PathResolver.NormalizePath(path);

        Assert.DoesNotContain(foreignSep.ToString(), normalized);
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
        var socketPath = PathResolver.GetSocketPath(SolutionFile);

        Assert.EndsWith(".sock", socketPath);
        Assert.Contains("roslyn-query-", socketPath);
    }

    [Fact]
    public void GetSocketPath_SameSolution_ReturnsSamePath()
    {
        var socketPath1 = PathResolver.GetSocketPath(SolutionFile);
        var socketPath2 = PathResolver.GetSocketPath(SolutionFile);

        Assert.Equal(socketPath1, socketPath2);
    }

    [Fact]
    public void GetSocketPath_DifferentSolutions_ReturnsDifferentPaths()
    {
        var solution1 = Path.Combine(TempBase, "project1", "MySolution.sln");
        var solution2 = Path.Combine(TempBase, "project2", "MySolution.sln");

        var socketPath1 = PathResolver.GetSocketPath(solution1);
        var socketPath2 = PathResolver.GetSocketPath(solution2);

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
        var pidPath = PathResolver.GetPidFilePath(SolutionFile);

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
        var root = PathResolver.GetSolutionRoot(SolutionFile);

        Assert.Equal(PathResolver.NormalizePath(ProjectRoot), root);
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
        var rootWithSlash = ProjectRoot + Path.DirectorySeparatorChar;

        var relative = PathResolver.ToRelativePath(FileUnderRoot, rootWithSlash);

        Assert.Equal(Path.Combine("src", "MyClass.cs"), relative);
    }

    [Fact]
    public void ToRelativePath_RootWithoutTrailingSlash_StillWorks()
    {
        var relative = PathResolver.ToRelativePath(FileUnderRoot, ProjectRoot);

        Assert.Equal(Path.Combine("src", "MyClass.cs"), relative);
    }

    #endregion
}
