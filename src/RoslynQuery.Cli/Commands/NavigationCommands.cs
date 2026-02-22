using System.CommandLine;
using System.CommandLine.Parsing;
using RoslynQuery.Core.Contracts.Requests;
using RoslynQuery.Core.Contracts.Responses;

namespace RoslynQuery.Cli.Commands;

/// <summary>
/// Navigation commands: definition, base-definition, implementations.
/// </summary>
public class DefinitionCommand : CommandBase
{
    private static readonly Argument<string> FileArg = new("file")
    {
        Description = "Source file path"
    };

    private static readonly Argument<int> LineArg = new("line")
    {
        Description = "Line number (1-based)"
    };

    private static readonly Argument<int> ColumnArg = new("column")
    {
        Description = "Column number (1-based)"
    };

    public DefinitionCommand() : base("definition", "Go to the definition of a symbol")
    {
        Arguments.Add(FileArg);
        Arguments.Add(LineArg);
        Arguments.Add(ColumnArg);

        this.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(FileArg)!;
            var line = parseResult.GetValue(LineArg);
            var column = parseResult.GetValue(ColumnArg);
            var solution = GetSolution(parseResult);
            var json = GetJson(parseResult);
            var verbose = GetVerbose(parseResult);

            return await ExecuteAsync(file, line, column, solution, json, verbose, cancellationToken);
        });
    }

    private static async Task<int> ExecuteAsync(
        string file, int line, int column,
        string? solution, bool json, bool verbose,
        CancellationToken cancellationToken)
    {
        try
        {
            var solutionPath = ResolveSolutionPath(solution);
            WriteVerbose(verbose, $"Using solution: {solutionPath}");

            await using var client = await DaemonManager.GetOrStartDaemonAsync(solutionPath, cancellationToken: cancellationToken);
            WriteVerbose(verbose, "Connected to daemon");

            var request = new DefinitionRequest
            {
                FilePath = file,
                Line = line,
                Column = column
            };

            var result = await SendRequestAsync<QueryResult<LocationResponse>>(
                client, "definition", request, cancellationToken);

            var format = GetOutputFormat(json);
            Console.WriteLine(OutputFormatter.FormatLocation(result?.Data, format));

            return result?.Success == true ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError(ex.Message, GetOutputFormat(json)));
            return 4;
        }
    }
}

public class BaseDefinitionCommand : CommandBase
{
    private static readonly Argument<string> FileArg = new("file")
    {
        Description = "Source file path"
    };

    private static readonly Argument<int> LineArg = new("line")
    {
        Description = "Line number (1-based)"
    };

    private static readonly Argument<int> ColumnArg = new("column")
    {
        Description = "Column number (1-based)"
    };

    public BaseDefinitionCommand() : base("base-definition", "Go to the base/interface definition of a symbol")
    {
        Arguments.Add(FileArg);
        Arguments.Add(LineArg);
        Arguments.Add(ColumnArg);

        this.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(FileArg)!;
            var line = parseResult.GetValue(LineArg);
            var column = parseResult.GetValue(ColumnArg);
            var solution = GetSolution(parseResult);
            var json = GetJson(parseResult);
            var verbose = GetVerbose(parseResult);

            return await ExecuteAsync(file, line, column, solution, json, verbose, cancellationToken);
        });
    }

    private static async Task<int> ExecuteAsync(
        string file, int line, int column,
        string? solution, bool json, bool verbose,
        CancellationToken cancellationToken)
    {
        try
        {
            var solutionPath = ResolveSolutionPath(solution);
            WriteVerbose(verbose, $"Using solution: {solutionPath}");

            await using var client = await DaemonManager.GetOrStartDaemonAsync(solutionPath, cancellationToken: cancellationToken);

            var request = new BaseDefinitionRequest
            {
                FilePath = file,
                Line = line,
                Column = column
            };

            var result = await SendRequestAsync<QueryResult<LocationResponse>>(
                client, "base-definition", request, cancellationToken);

            var format = GetOutputFormat(json);
            Console.WriteLine(OutputFormatter.FormatLocation(result?.Data, format));

            return result?.Success == true ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError(ex.Message, GetOutputFormat(json)));
            return 4;
        }
    }
}

public class ImplementationsCommand : CommandBase
{
    private static readonly Argument<string> FileArg = new("file")
    {
        Description = "Source file path"
    };

    private static readonly Argument<int> LineArg = new("line")
    {
        Description = "Line number (1-based)"
    };

    private static readonly Argument<int> ColumnArg = new("column")
    {
        Description = "Column number (1-based)"
    };

    public ImplementationsCommand() : base("implementations", "Find all implementations of an interface or virtual member")
    {
        Arguments.Add(FileArg);
        Arguments.Add(LineArg);
        Arguments.Add(ColumnArg);

        this.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(FileArg)!;
            var line = parseResult.GetValue(LineArg);
            var column = parseResult.GetValue(ColumnArg);
            var solution = GetSolution(parseResult);
            var json = GetJson(parseResult);
            var verbose = GetVerbose(parseResult);

            return await ExecuteAsync(file, line, column, solution, json, verbose, cancellationToken);
        });
    }

    private static async Task<int> ExecuteAsync(
        string file, int line, int column,
        string? solution, bool json, bool verbose,
        CancellationToken cancellationToken)
    {
        try
        {
            var solutionPath = ResolveSolutionPath(solution);
            WriteVerbose(verbose, $"Using solution: {solutionPath}");

            await using var client = await DaemonManager.GetOrStartDaemonAsync(solutionPath, cancellationToken: cancellationToken);

            var request = new ImplementationsRequest
            {
                FilePath = file,
                Line = line,
                Column = column
            };

            var result = await SendRequestAsync<QueryResult<LocationsResponse>>(
                client, "implementations", request, cancellationToken);

            var format = GetOutputFormat(json);
            Console.WriteLine(OutputFormatter.FormatLocations(result?.Data, format));

            return result?.Success == true ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError(ex.Message, GetOutputFormat(json)));
            return 4;
        }
    }
}
