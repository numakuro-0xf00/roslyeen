using System.CommandLine;
using System.CommandLine.Parsing;
using RoslynQuery.Core.Contracts.Enums;
using RoslynQuery.Core.IpcProtocol;
using RoslynQuery.Core.Utilities;

namespace RoslynQuery.Cli.Commands;

/// <summary>
/// Base class for CLI commands with common options.
/// </summary>
public abstract class CommandBase : Command
{
    protected static readonly Option<string?> SolutionOption = new("--solution", "-s")
    {
        Description = "Path to the solution file. If not specified, uses ROSLYN_QUERY_SOLUTION env var, then searches for .sln in current directory and parents."
    };

    protected static readonly Option<bool> JsonOption = new("--json")
    {
        Description = "Output results in JSON format"
    };

    protected static readonly Option<bool> VerboseOption = new("--verbose", "-v")
    {
        Description = "Enable verbose output"
    };

    protected CommandBase(string name, string? description = null)
        : base(name, description)
    {
        Options.Add(SolutionOption);
        Options.Add(JsonOption);
        Options.Add(VerboseOption);
    }

    protected static string ResolveSolutionPath(string? solutionPath)
    {
        if (!string.IsNullOrEmpty(solutionPath))
        {
            if (!File.Exists(solutionPath))
                throw new FileNotFoundException($"Solution file not found: {solutionPath}");
            return Path.GetFullPath(solutionPath);
        }

        var envSolution = Environment.GetEnvironmentVariable("ROSLYN_QUERY_SOLUTION");
        if (!string.IsNullOrEmpty(envSolution))
        {
            if (!File.Exists(envSolution))
                throw new FileNotFoundException($"Solution file from ROSLYN_QUERY_SOLUTION not found: {envSolution}");
            return Path.GetFullPath(envSolution);
        }

        var found = PathResolver.FindSolutionFile(Environment.CurrentDirectory);
        if (found == null)
            throw new FileNotFoundException("No solution file found. Specify --solution, set ROSLYN_QUERY_SOLUTION, or run from a directory with a .sln file.");

        return found;
    }

    protected static OutputFormat GetOutputFormat(bool json) =>
        json ? OutputFormat.Json : OutputFormat.Text;

    protected static void WriteVerbose(bool verbose, string message)
    {
        if (verbose)
        {
            Console.Error.WriteLine(message);
        }
    }

    protected static void WriteOutput(string? output, string noResultMessage, OutputFormat format)
    {
        if (output != null)
        {
            Console.WriteLine(output);
        }
        else if (format != OutputFormat.Json)
        {
            Console.Error.WriteLine(noResultMessage);
        }
    }

    protected static async Task<T?> SendRequestAsync<T>(
        IpcClient client,
        string method,
        object parameters,
        CancellationToken cancellationToken) where T : class
    {
        var response = await client.SendRequestAsync(method, parameters, cancellationToken);

        if (!response.IsSuccess)
        {
            var errorMsg = response.Error?.Message ?? "Unknown error";
            throw new InvalidOperationException(errorMsg);
        }

        if (response.Result == null)
        {
            return null;
        }

        return IpcSerializer.Deserialize<T>(response.Result.Value);
    }

    protected static string? GetSolution(ParseResult parseResult) => parseResult.GetValue(SolutionOption);
    protected static bool GetJson(ParseResult parseResult) => parseResult.GetValue(JsonOption);
    protected static bool GetVerbose(ParseResult parseResult) => parseResult.GetValue(VerboseOption);
}
