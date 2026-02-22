using System.CommandLine;
using System.CommandLine.Parsing;
using RoslynQuery.Core.Contracts.Requests;
using RoslynQuery.Core.Contracts.Responses;

namespace RoslynQuery.Cli.Commands;

/// <summary>
/// Relationship commands: references, callers, callees.
/// </summary>
public class ReferencesCommand : CommandBase
{
    private static readonly Argument<string> FileArg = new("file") { Description = "Source file path" };
    private static readonly Argument<int> LineArg = new("line") { Description = "Line number (1-based)" };
    private static readonly Argument<int> ColumnArg = new("column") { Description = "Column number (1-based)" };
    private static readonly Option<bool> IncludeDefOption = new("--include-definition")
    {
        Description = "Include the definition location in results"
    };

    public ReferencesCommand() : base("references", "Find all references to a symbol")
    {
        Arguments.Add(FileArg);
        Arguments.Add(LineArg);
        Arguments.Add(ColumnArg);
        Options.Add(IncludeDefOption);

        this.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(FileArg)!;
            var line = parseResult.GetValue(LineArg);
            var column = parseResult.GetValue(ColumnArg);
            var includeDef = parseResult.GetValue(IncludeDefOption);
            var solution = GetSolution(parseResult);
            var json = GetJson(parseResult);
            var verbose = GetVerbose(parseResult);

            return await ExecuteAsync(file, line, column, includeDef, solution, json, verbose, cancellationToken);
        });
    }

    private static async Task<int> ExecuteAsync(
        string file, int line, int column, bool includeDefinition,
        string? solution, bool json, bool verbose,
        CancellationToken cancellationToken)
    {
        try
        {
            var solutionPath = ResolveSolutionPath(solution);
            WriteVerbose(verbose, $"Using solution: {solutionPath}");

            await using var client = await DaemonManager.GetOrStartDaemonAsync(solutionPath, cancellationToken: cancellationToken);

            var request = new ReferencesRequest
            {
                FilePath = file,
                Line = line,
                Column = column,
                IncludeDefinition = includeDefinition
            };

            var result = await SendRequestAsync<QueryResult<LocationsResponse>>(
                client, "references", request, cancellationToken);

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

public class CallersCommand : CommandBase
{
    private static readonly Argument<string> FileArg = new("file") { Description = "Source file path" };
    private static readonly Argument<int> LineArg = new("line") { Description = "Line number (1-based)" };
    private static readonly Argument<int> ColumnArg = new("column") { Description = "Column number (1-based)" };

    public CallersCommand() : base("callers", "Find all callers of a method/property (call hierarchy - incoming)")
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

            var request = new CallersRequest
            {
                FilePath = file,
                Line = line,
                Column = column
            };

            var result = await SendRequestAsync<QueryResult<LocationsResponse>>(
                client, "callers", request, cancellationToken);

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

public class CalleesCommand : CommandBase
{
    private static readonly Argument<string> FileArg = new("file") { Description = "Source file path" };
    private static readonly Argument<int> LineArg = new("line") { Description = "Line number (1-based)" };
    private static readonly Argument<int> ColumnArg = new("column") { Description = "Column number (1-based)" };

    public CalleesCommand() : base("callees", "Find all methods/properties called by a method (call hierarchy - outgoing)")
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

            var request = new CalleesRequest
            {
                FilePath = file,
                Line = line,
                Column = column
            };

            var result = await SendRequestAsync<QueryResult<LocationsResponse>>(
                client, "callees", request, cancellationToken);

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

public class SymbolCommand : CommandBase
{
    private static readonly Argument<string> FileArg = new("file") { Description = "Source file path" };
    private static readonly Argument<int> LineArg = new("line") { Description = "Line number (1-based)" };
    private static readonly Argument<int> ColumnArg = new("column") { Description = "Column number (1-based)" };

    public SymbolCommand() : base("symbol", "Get detailed information about a symbol")
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

            var request = new SymbolRequest
            {
                FilePath = file,
                Line = line,
                Column = column
            };

            var result = await SendRequestAsync<QueryResult<SymbolInfoResponse>>(
                client, "symbol", request, cancellationToken);

            var format = GetOutputFormat(json);
            Console.WriteLine(OutputFormatter.FormatSymbolInfo(result?.Data, format));

            return result?.Success == true ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError(ex.Message, GetOutputFormat(json)));
            return 4;
        }
    }
}

public class DiagnosticsCommand : CommandBase
{
    private static readonly Argument<string?> FileArg = new("file")
    {
        Description = "Source file path (optional, defaults to entire solution)",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static readonly Option<bool> IncludeWarningsOption = new("--warnings")
    {
        Description = "Include warnings",
        DefaultValueFactory = _ => true
    };

    private static readonly Option<bool> IncludeInfoOption = new("--info")
    {
        Description = "Include info-level diagnostics"
    };

    public DiagnosticsCommand() : base("diagnostics", "Get compiler diagnostics (errors/warnings)")
    {
        Arguments.Add(FileArg);
        Options.Add(IncludeWarningsOption);
        Options.Add(IncludeInfoOption);

        this.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(FileArg);
            var includeWarnings = parseResult.GetValue(IncludeWarningsOption);
            var includeInfo = parseResult.GetValue(IncludeInfoOption);
            var solution = GetSolution(parseResult);
            var json = GetJson(parseResult);
            var verbose = GetVerbose(parseResult);

            return await ExecuteAsync(file, includeWarnings, includeInfo, solution, json, verbose, cancellationToken);
        });
    }

    private static async Task<int> ExecuteAsync(
        string? file, bool includeWarnings, bool includeInfo,
        string? solution, bool json, bool verbose,
        CancellationToken cancellationToken)
    {
        try
        {
            var solutionPath = ResolveSolutionPath(solution);
            WriteVerbose(verbose, $"Using solution: {solutionPath}");

            await using var client = await DaemonManager.GetOrStartDaemonAsync(solutionPath, cancellationToken: cancellationToken);

            var request = new DiagnosticsRequest
            {
                FilePath = file,
                IncludeWarnings = includeWarnings,
                IncludeInfo = includeInfo
            };

            var result = await SendRequestAsync<QueryResult<DiagnosticsResponse>>(
                client, "diagnostics", request, cancellationToken);

            var format = GetOutputFormat(json);
            Console.WriteLine(OutputFormatter.FormatDiagnostics(result?.Data, format));

            return result?.Success == true ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(OutputFormatter.FormatError(ex.Message, GetOutputFormat(json)));
            return 4;
        }
    }
}
