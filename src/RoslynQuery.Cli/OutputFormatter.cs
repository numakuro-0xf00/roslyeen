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
            return format == OutputFormat.Json ? "{\"location\": null}" : null;
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
            return format == OutputFormat.Json ? "{\"locations\": [], \"count\": 0}" : null;
        }

        if (format == OutputFormat.Json)
        {
            return IpcSerializer.Serialize(response);
        }

        return string.Join(Environment.NewLine, response.Locations.Select(loc => loc.Location.ToString()));
    }

    /// <summary>
    /// Format a symbol info response.
    /// </summary>
    public static string? FormatSymbolInfo(SymbolInfoResponse? response, OutputFormat format)
    {
        if (response == null)
        {
            return format == OutputFormat.Json ? "{\"symbol\": null}" : null;
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
        if (response == null)
        {
            return format == OutputFormat.Json ? "{\"diagnostics\": []}" : null;
        }

        if (format == OutputFormat.Json)
        {
            return IpcSerializer.Serialize(response);
        }

        if (response.Diagnostics.Count == 0)
        {
            return null;
        }

        var lines = response.Diagnostics.Select(diag =>
        {
            var severity = diag.Severity.ToString().ToLowerInvariant();
            return diag.Location != null
                ? $"{diag.Location.FilePath}:{diag.Location.Line}:{diag.Location.Column}: {severity} {diag.Id}: {diag.Message}"
                : $"{diag.Id}: {diag.Message}";
        });
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

            if (status.IdleTimeoutMinutes.HasValue)
            {
                var timeoutDisplay = status.IdleTimeoutMinutes.Value == 0
                    ? "Disabled"
                    : $"{status.IdleTimeoutMinutes.Value:F0} minutes";
                lines.Add($"Idle Timeout: {timeoutDisplay}");
            }

            if (status.IdleSeconds.HasValue)
            {
                var ts = TimeSpan.FromSeconds(status.IdleSeconds.Value);
                var idleDisplay = ts.TotalMinutes >= 1
                    ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
                    : $"{ts.TotalSeconds:F0}s";
                lines.Add($"Idle: {idleDisplay}");
            }
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
