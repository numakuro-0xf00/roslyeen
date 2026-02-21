using System.Diagnostics;
using RoslynQuery.Core.IpcProtocol;
using RoslynQuery.Core.Utilities;

namespace RoslynQuery.Cli;

/// <summary>
/// Manages daemon lifecycle - starting, connecting, and checking status.
/// </summary>
public static class DaemonManager
{
    /// <summary>
    /// Get or start a connection to the daemon for the given solution.
    /// </summary>
    public static async Task<IpcClient> GetOrStartDaemonAsync(
        string solutionPath,
        bool autoStart = true,
        int idleTimeoutMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        var socketPath = PathResolver.GetSocketPath(solutionPath);
        var pidPath = PathResolver.GetPidFilePath(solutionPath);

        // Try to connect to existing daemon
        if (File.Exists(socketPath) && await CheckPidFileAsync(pidPath))
        {
            var client = new IpcClient(socketPath);
            try
            {
                await client.ConnectAsync(cancellationToken);
                return client;
            }
            catch
            {
                await client.DisposeAsync();
                // Daemon socket exists but connection failed - cleanup and restart
                CleanupStaleFiles(socketPath, pidPath);
            }
        }

        if (!autoStart)
        {
            throw new InvalidOperationException("Daemon is not running and auto-start is disabled");
        }

        // Start new daemon
        await StartDaemonAsync(solutionPath, idleTimeoutMinutes, cancellationToken);

        // Wait for daemon to be ready
        var client2 = new IpcClient(socketPath);
        var connected = false;
        var maxAttempts = 30; // 30 seconds max

        for (var i = 0; i < maxAttempts && !connected; i++)
        {
            await Task.Delay(1000, cancellationToken);

            try
            {
                await client2.ConnectAsync(cancellationToken);
                connected = true;
            }
            catch
            {
                if (i == maxAttempts - 1)
                {
                    await client2.DisposeAsync();
                    throw new InvalidOperationException("Failed to connect to daemon after starting");
                }
            }
        }

        return client2;
    }

    /// <summary>
    /// Start the daemon for the given solution.
    /// </summary>
    public static async Task StartDaemonAsync(string solutionPath, int idleTimeoutMinutes = 30, CancellationToken cancellationToken = default)
    {
        var daemonPath = GetDaemonPath();

        // Use CLI's deps.json for dependency resolution when Daemon's own deps.json is missing
        // (e.g., when installed as a dotnet tool where Daemon is bundled alongside CLI)
        var daemonDir = Path.GetDirectoryName(daemonPath)!;
        var daemonDepsPath = Path.Combine(daemonDir, "RoslynQuery.Daemon.deps.json");
        var cliDepsPath = Path.Combine(daemonDir, "RoslynQuery.Cli.deps.json");
        var depsArg = !File.Exists(daemonDepsPath) && File.Exists(cliDepsPath)
            ? $"exec --depsfile \"{cliDepsPath}\" \"{daemonPath}\""
            : $"\"{daemonPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"{depsArg} \"{solutionPath}\" --idle-timeout {idleTimeoutMinutes}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start daemon process");
        }

        // Don't wait for the process - it runs in the background
        // Just wait a moment to ensure it starts
        await Task.Delay(500, cancellationToken);

        if (process.HasExited)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Daemon exited immediately: {error}");
        }
    }

    /// <summary>
    /// Check if the daemon is running for the given solution.
    /// </summary>
    public static async Task<bool> IsDaemonRunningAsync(string solutionPath)
    {
        var pidPath = PathResolver.GetPidFilePath(solutionPath);
        return await CheckPidFileAsync(pidPath);
    }

    /// <summary>
    /// Send shutdown request to the daemon.
    /// </summary>
    public static async Task ShutdownDaemonAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        var socketPath = PathResolver.GetSocketPath(solutionPath);

        if (!File.Exists(socketPath))
        {
            return; // Not running
        }

        await using var client = new IpcClient(socketPath);
        try
        {
            await client.ConnectAsync(cancellationToken);
            await client.SendRequestAsync("shutdown", cancellationToken);
        }
        catch
        {
            // Ignore errors - daemon may already be shutting down
        }
    }

    /// <summary>
    /// Get daemon status information.
    /// </summary>
    public static async Task<DaemonStatus> GetStatusAsync(string solutionPath)
    {
        var socketPath = PathResolver.GetSocketPath(solutionPath);
        var pidPath = PathResolver.GetPidFilePath(solutionPath);

        var status = new DaemonStatus
        {
            SolutionPath = solutionPath,
            SocketPath = socketPath,
            PidFilePath = pidPath
        };

        if (!File.Exists(pidPath))
        {
            status.IsRunning = false;
            return status;
        }

        var pidText = await File.ReadAllTextAsync(pidPath);
        if (!int.TryParse(pidText.Trim(), out var pid))
        {
            status.IsRunning = false;
            return status;
        }

        status.ProcessId = pid;

        try
        {
            var process = Process.GetProcessById(pid);
            status.IsRunning = !process.HasExited;
        }
        catch
        {
            status.IsRunning = false;
        }

        // Try to ping if running
        if (status.IsRunning && File.Exists(socketPath))
        {
            await using var client = new IpcClient(socketPath);
            try
            {
                await client.ConnectAsync();
                var response = await client.SendRequestAsync("ping");
                status.IsResponsive = response.IsSuccess;

                if (response.IsSuccess && response.Result.HasValue)
                {
                    var result = response.Result.Value;
                    if (result.TryGetProperty("idleTimeoutMinutes", out var timeoutEl))
                    {
                        status.IdleTimeoutMinutes = timeoutEl.GetDouble();
                    }
                    if (result.TryGetProperty("idleSeconds", out var idleEl))
                    {
                        status.IdleSeconds = idleEl.GetDouble();
                    }
                }
            }
            catch
            {
                status.IsResponsive = false;
            }
        }

        return status;
    }

    private static async Task<bool> CheckPidFileAsync(string pidPath)
    {
        if (!File.Exists(pidPath))
        {
            return false;
        }

        var pidText = await File.ReadAllTextAsync(pidPath);
        if (!int.TryParse(pidText.Trim(), out var pid))
        {
            return false;
        }

        try
        {
            var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static void CleanupStaleFiles(string socketPath, string pidPath)
    {
        try
        {
            if (File.Exists(socketPath)) File.Delete(socketPath);
            if (File.Exists(pidPath)) File.Delete(pidPath);
        }
        catch
        {
            // Ignore
        }
    }

    private static string GetDaemonPath()
    {
        // Get the path to RoslynQuery.Daemon.dll relative to this CLI
        var cliDir = AppContext.BaseDirectory;
        var daemonPath = Path.Combine(cliDir, "..", "RoslynQuery.Daemon", "RoslynQuery.Daemon.dll");

        if (File.Exists(daemonPath))
        {
            return Path.GetFullPath(daemonPath);
        }

        // Try alongside the CLI (same directory)
        daemonPath = Path.Combine(cliDir, "RoslynQuery.Daemon.dll");
        if (File.Exists(daemonPath))
        {
            return daemonPath;
        }

        // Try in development layout
        daemonPath = Path.Combine(cliDir, "..", "..", "..", "..", "RoslynQuery.Daemon", "bin", "Debug", "net8.0", "RoslynQuery.Daemon.dll");
        if (File.Exists(daemonPath))
        {
            return Path.GetFullPath(daemonPath);
        }

        throw new InvalidOperationException("Cannot find RoslynQuery.Daemon.dll");
    }
}

/// <summary>
/// Daemon status information.
/// </summary>
public class DaemonStatus
{
    public required string SolutionPath { get; init; }
    public required string SocketPath { get; init; }
    public required string PidFilePath { get; init; }
    public bool IsRunning { get; set; }
    public bool IsResponsive { get; set; }
    public int? ProcessId { get; set; }
    public double? IdleTimeoutMinutes { get; set; }
    public double? IdleSeconds { get; set; }
}
