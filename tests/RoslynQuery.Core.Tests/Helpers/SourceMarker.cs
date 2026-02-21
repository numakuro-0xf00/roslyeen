using System.Text.RegularExpressions;

namespace RoslynQuery.Core.Tests.Helpers;

/// <summary>
/// Parses [|marker|] syntax in source code to extract positions without magic numbers.
/// Returns 1-based line and column numbers matching the PositionRequest convention.
/// </summary>
public static partial class SourceMarker
{
    private const string MarkerToken = "[|";
    private const string MarkerEndToken = "|]";

    /// <summary>
    /// Parse source containing a single [|marker|] and return the clean source plus 1-based position.
    /// </summary>
    public static (string Source, int Line, int Column) Parse(string markedSource)
    {
        var markerStart = markedSource.IndexOf(MarkerToken, StringComparison.Ordinal);
        if (markerStart < 0)
            throw new ArgumentException("No [|marker|] found in source");

        var markerEnd = markedSource.IndexOf(MarkerEndToken, markerStart + MarkerToken.Length, StringComparison.Ordinal);
        if (markerEnd < 0)
            throw new ArgumentException("No closing |] found for marker");

        // Remove markers to get clean source
        var clean = markedSource.Remove(markerEnd, MarkerEndToken.Length).Remove(markerStart, MarkerToken.Length);

        // Calculate 1-based line and column from the marker start position in the clean source
        var beforeMarker = clean[..markerStart];
        var lines = beforeMarker.Split('\n');
        var line = lines.Length;
        var column = lines[^1].Length + 1;

        return (clean, line, column);
    }

    /// <summary>
    /// Parse source containing multiple named markers: [|name:text|].
    /// Returns the clean source and a dictionary of marker name to (Line, Column) tuples.
    /// </summary>
    public static (string Source, Dictionary<string, (int Line, int Column)> Markers) ParseMultiple(string markedSource)
    {
        var markers = new Dictionary<string, (int Line, int Column)>();
        var clean = markedSource;

        // Find all [|name:...|] patterns
        var regex = NamedMarkerRegex();
        var matches = regex.Matches(markedSource);

        // Process in reverse order to keep offsets valid
        var orderedMatches = matches.Cast<Match>().OrderByDescending(m => m.Index).ToList();

        foreach (var match in orderedMatches)
        {
            var name = match.Groups[1].Value;
            var markerStart = match.Index;

            // Remove the full marker, keep the inner text
            var innerText = match.Groups[2].Value;
            clean = clean.Remove(match.Index, match.Length).Insert(match.Index, innerText);
        }

        // Now calculate positions in the clean source
        // Re-process in forward order
        var forwardMatches = matches.Cast<Match>().OrderBy(m => m.Index).ToList();
        var cumulativeRemoved = 0;

        foreach (var match in forwardMatches)
        {
            var name = match.Groups[1].Value;
            var posInClean = match.Index - cumulativeRemoved;

            var beforeMarker = clean[..posInClean];
            var lines = beforeMarker.Split('\n');
            var line = lines.Length;
            var column = lines[^1].Length + 1;

            markers[name] = (line, column);

            // "[|name:" = 2 + name.Length + 1 = name.Length + 3, "|]" = 2
            cumulativeRemoved += name.Length + 3 + 2; // [|name: and |]
        }

        return (clean, markers);
    }

    [GeneratedRegex(@"\[\|(\w+):([^|]*?)\|\]")]
    private static partial Regex NamedMarkerRegex();
}
