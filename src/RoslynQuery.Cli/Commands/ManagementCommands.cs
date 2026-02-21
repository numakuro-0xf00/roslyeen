using System.CommandLine;
using System.CommandLine.Parsing;

namespace RoslynQuery.Cli.Commands;

/// <summary>
/// Management commands: init, status, shutdown.
/// </summary>
public class InitCommand : CommandBase
{
    public InitCommand() : base("init", "Start the daemon for a solution")
    {
        this.SetAction(async (parseResult, cancellationToken) =>
        {
            var solution = GetSolution(parseResult);
            var json = GetJson(parseResult);
            var verbose = GetVerbose(parseResult);

            return await ExecuteAsync(solution, json, verbose, cancellationToken);
        });
    }

    private static async Task<int> ExecuteAsync(
        string? solution, bool json, bool verbose,
        CancellationToken cancellationToken)
    {
        try
        {
            var solutionPath = ResolveSolutionPath(solution);
            WriteVerbose(verbose, $"Using solution: {solutionPath}");

            // Check if already running
            var status = await DaemonManager.GetStatusAsync(solutionPath);
            if (status.IsRunning && status.IsResponsive)
            {
                var msg = $"Daemon already running for {solutionPath} (PID: {status.ProcessId})";
                if (json)
                {
                    Console.WriteLine($"{{\"status\": \"already_running\", \"pid\": {status.ProcessId}}}");
                }
                else
                {
                    Console.WriteLine(msg);
                }
                return 0;
            }

            // Start daemon
            WriteVerbose(verbose, "Starting daemon...");
            await DaemonManager.StartDaemonAsync(solutionPath, cancellationToken);

            // Wait for it to be ready
            var maxWait = 30;
            for (var i = 0; i < maxWait; i++)
            {
                await Task.Delay(1000, cancellationToken);
                status = await DaemonManager.GetStatusAsync(solutionPath);
                if (status.IsRunning && status.IsResponsive)
                {
                    break;
                }
                WriteVerbose(verbose, "Waiting for daemon to be ready...");
            }

            if (!status.IsRunning || !status.IsResponsive)
            {
                Console.Error.WriteLine("Failed to start daemon");
                return 4;
            }

            if (json)
            {
                Console.WriteLine($"{{\"status\": \"started\", \"pid\": {status.ProcessId}}}");
            }
            else
            {
                Console.WriteLine($"Daemon started for {solutionPath} (PID: {status.ProcessId})");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError(ex.Message, GetOutputFormat(json)));
            return 4;
        }
    }
}

public class StatusCommand : CommandBase
{
    public StatusCommand() : base("status", "Check daemon status")
    {
        this.SetAction(async (parseResult, _) =>
        {
            var solution = GetSolution(parseResult);
            var json = GetJson(parseResult);
            var verbose = GetVerbose(parseResult);

            return await ExecuteAsync(solution, json, verbose);
        });
    }

    private static async Task<int> ExecuteAsync(string? solution, bool json, bool verbose)
    {
        try
        {
            var solutionPath = ResolveSolutionPath(solution);
            WriteVerbose(verbose, $"Using solution: {solutionPath}");

            var status = await DaemonManager.GetStatusAsync(solutionPath);

            Console.WriteLine(OutputFormatter.FormatStatus(status, GetOutputFormat(json)));

            return status.IsRunning ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError(ex.Message, GetOutputFormat(json)));
            return 4;
        }
    }
}

public class ShutdownCommand : CommandBase
{
    public ShutdownCommand() : base("shutdown", "Stop the daemon")
    {
        this.SetAction(async (parseResult, cancellationToken) =>
        {
            var solution = GetSolution(parseResult);
            var json = GetJson(parseResult);
            var verbose = GetVerbose(parseResult);

            return await ExecuteAsync(solution, json, verbose, cancellationToken);
        });
    }

    private static async Task<int> ExecuteAsync(
        string? solution, bool json, bool verbose,
        CancellationToken cancellationToken)
    {
        try
        {
            var solutionPath = ResolveSolutionPath(solution);
            WriteVerbose(verbose, $"Using solution: {solutionPath}");

            var status = await DaemonManager.GetStatusAsync(solutionPath);
            if (!status.IsRunning)
            {
                if (json)
                {
                    Console.WriteLine("{\"status\": \"not_running\"}");
                }
                else
                {
                    Console.WriteLine("Daemon is not running");
                }
                return 0;
            }

            WriteVerbose(verbose, $"Shutting down daemon (PID: {status.ProcessId})...");
            await DaemonManager.ShutdownDaemonAsync(solutionPath, cancellationToken);

            // Wait for shutdown
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(500, cancellationToken);
                status = await DaemonManager.GetStatusAsync(solutionPath);
                if (!status.IsRunning)
                {
                    break;
                }
            }

            if (status.IsRunning)
            {
                Console.Error.WriteLine("Daemon did not shut down gracefully");
                return 1;
            }

            if (json)
            {
                Console.WriteLine("{\"status\": \"stopped\"}");
            }
            else
            {
                Console.WriteLine("Daemon stopped");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError(ex.Message, GetOutputFormat(json)));
            return 4;
        }
    }
}
