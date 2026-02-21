using System.Runtime.CompilerServices;
using RoslynQuery.Core.Workspace;

namespace RoslynQuery.Daemon;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: roslyn-queryd <solution-path>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Starts the Roslyn Query daemon for the specified solution.");
            return 1;
        }

        var solutionPath = args[0];

        if (!File.Exists(solutionPath))
        {
            Console.Error.WriteLine($"Solution file not found: {solutionPath}");
            return 2;
        }

        // Parse --idle-timeout option (default: 30 minutes, 0 = disabled)
        TimeSpan? idleTimeout = TimeSpan.FromMinutes(30);
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--idle-timeout" && i + 1 < args.Length
                && int.TryParse(args[i + 1], out var minutes) && minutes >= 0)
            {
                idleTimeout = minutes == 0 ? null : TimeSpan.FromMinutes(minutes);
                i++;
            }
        }

        try
        {
            // CRITICAL: Initialize MSBuild BEFORE any Roslyn types are accessed
            var instance = MsBuildInitializer.EnsureInitialized();
            Console.Error.WriteLine($"Using MSBuild from: {instance.MSBuildPath}");
            Console.Error.WriteLine($"MSBuild version: {instance.Version}");

            return await RunDaemonAsync(solutionPath, idleTimeout);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<int> RunDaemonAsync(string solutionPath, TimeSpan? idleTimeout)
    {
        await using var daemon = await DaemonHost.CreateAndStartAsync(solutionPath, idleTimeout);

        Console.Error.WriteLine("Daemon ready. Waiting for requests...");

        await daemon.WaitForShutdownAsync();

        Console.Error.WriteLine("Daemon shutting down...");

        return 0;
    }
}
