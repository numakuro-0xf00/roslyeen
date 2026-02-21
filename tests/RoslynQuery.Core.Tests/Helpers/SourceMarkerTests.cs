namespace RoslynQuery.Core.Tests.Helpers;

public class SourceMarkerTests
{
    [Fact]
    public void Parse_SingleMarkerOnFirstLine_ReturnsCorrectPosition()
    {
        var marked = "class [|MyClass|] { }";
        var (source, line, column) = SourceMarker.Parse(marked);

        Assert.Equal("class MyClass { }", source);
        Assert.Equal(1, line);
        Assert.Equal(7, column);
    }

    [Fact]
    public void Parse_MarkerOnSecondLine_ReturnsCorrectLineAndColumn()
    {
        var marked = "namespace Ns {\nclass [|MyClass|] { }\n}";
        var (source, line, column) = SourceMarker.Parse(marked);

        Assert.Equal("namespace Ns {\nclass MyClass { }\n}", source);
        Assert.Equal(2, line);
        Assert.Equal(7, column);
    }

    [Fact]
    public void Parse_NoMarker_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SourceMarker.Parse("no marker here"));
    }

    [Fact]
    public void Parse_MarkerAtStartOfLine_ReturnsColumn1()
    {
        var marked = "[|class|] MyClass { }";
        var (source, line, column) = SourceMarker.Parse(marked);

        Assert.Equal("class MyClass { }", source);
        Assert.Equal(1, line);
        Assert.Equal(1, column);
    }

    [Fact]
    public void ParseMultiple_TwoNamedMarkers_ReturnsBothPositions()
    {
        var marked = "class C {\nvoid [|def:MyMethod|]() { }\nvoid Caller() { [|call:MyMethod|](); }\n}";
        var (source, markers) = SourceMarker.ParseMultiple(marked);

        Assert.DoesNotContain("[|", source);
        Assert.DoesNotContain("|]", source);
        Assert.Contains("def", markers.Keys);
        Assert.Contains("call", markers.Keys);
        Assert.Equal(2, markers["def"].Line);
        Assert.Equal(3, markers["call"].Line);
    }
}
