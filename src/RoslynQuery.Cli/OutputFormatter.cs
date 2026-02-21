using System.Text.Json;
using RoslynQuery.Core.Contracts.Enums;
using RoslynQuery.Core.Contracts.Responses;
using RoslynQuery.Core.IpcProtocol;

namespace RoslynQuery.Cli;

/// <summary>
/// Formats query results for output.
/// </summary>
public static class OutputFormatter
{
    /// <summary>
    /// Format a location response.
    /// </summary>
    public static string? FormatLocation(LocationResponse? response, OutputFormat format)
    {
        if (response?.Location == null)
        {
            if (format == OutputFormat.Json)
                return "{\"location\": null}";
            return null;
        }

        if (format == OutputFormat.Json)
        {
            return IpcSerializer.Serialize(response);
        }

        return response.Location.ToString();
    }

    /// <summary>
    /// Format a locations response.
    /// </summary>
    public static string? FormatLocations(LocationsResponse? response, OutputFormat format)
    {
        if (response == null || response.Locations.Count == 0)
        {
            if (format == OutputFormat.Json)
                return "{\"locations\": [], \"count\": 0}";
            return null;
        }

        if (format == OutputFormat.Json)
        {
            return IpcSerializer.Serialize(response);
        }

        var lines = response.Locations.Select(loc => loc.Location.ToString());
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Format a symbol info response.
    /// </summary>
    public static string? FormatSymbolInfo(SymbolInfoResponse? response, OutputFormat format)
    {
        if (response == null)
        {
            if (format == OutputFormat.Json)
                return "{\"symbol\": null}";
            return null;
        }

        if (format == OutputFormat.Json)
        {
            return IpcSerializer.Serialize(response);
        }

        var lines = new List<string>
        {
            $"Symbol: {response.Name}",
            $"Kind: {response.Kind}",
            $"Full Name: {response.FullName}"
        };

        if (!string.IsNullOrEmpty(response.Signature))
        {
            lines.Add($"Signature: {response.Signature}");
        }

        if (!string.IsNullOrEmpty(response.ReturnType))
        {
            lines.Add($"Return Type: {response.ReturnType}");
        }

        if (!string.IsNullOrEmpty(response.Accessibility))
        {
            lines.Add($"Accessibility: {response.Accessibility}");
        }

        if (response.Modifiers.Count > 0)
        {
            lines.Add($"Modifiers: {string.Join(", ", response.Modifiers)}");
        }

        if (!string.IsNullOrEmpty(response.ContainingNamespace))
        {
            lines.Add($"Namespace: {response.ContainingNamespace}");
        }

        if (!string.IsNullOrEmpty(response.ContainingType))
        {
            lines.Add($"Containing Type: {response.ContainingType}");
        }

        if (response.Location != null)
        {
            lines.Add($"Location: {response.Location}");
        }

        if (!string.IsNullOrEmpty(response.Documentation))
        {
            lines.Add($"Documentation: {response.Documentation}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Format a diagnostics response.
    /// </summary>
    public static string? FormatDiagnostics(DiagnosticsResponse? response, OutputFormat format)
    {
        if (response == null || response.Diagnostics.Count == 0)
        {
            if (format == OutputFormat.Json)
                return "{\"diagnostics\": []}";
            return null;
        }

        if (format == OutputFormat.Json)
        {
            return IpcSerializer.Serialize(response);
        }

        var lines = new List<string>();

        foreach (var diag in response.Diagnostics)
        {
            var severity = diag.Severity.ToString().ToLowerInvariant();
            if (diag.Location != null)
            {
                lines.Add($"{diag.Location}: {severity} {diag.Id}: {diag.Message}");
            }
            else
            {
                lines.Add($"{diag.Id}: {diag.Message}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Format daemon status.
    /// </summary>
    public static string FormatStatus(DaemonStatus status, OutputFormat format)
    {
        if (format == OutputFormat.Json)
        {
            return IpcSerializer.Serialize(status);
        }

        var lines = new List<string>
        {
            $"Solution: {status.SolutionPath}",
            $"Socket: {status.SocketPath}",
            $"PID File: {status.PidFilePath}",
            $"Running: {(status.IsRunning ? "Yes" : "No")}"
        };

        if (status.IsRunning)
        {
            lines.Add($"Process ID: {status.ProcessId}");
            lines.Add($"Responsive: {(status.IsResponsive ? "Yes" : "No")}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Format an error.
    /// </summary>
    public static string FormatError(string message, OutputFormat format)
    {
        if (format == OutputFormat.Json)
        {
            return IpcSerializer.Serialize(new { error = message });
        }

        return $"Error: {message}";
    }
}
