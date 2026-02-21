using System.CommandLine;
using RoslynQuery.Cli.Commands;

namespace RoslynQuery.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Roslyn Query - CLI tool for C# code navigation and analysis")
        {
            // Navigation commands
            new DefinitionCommand(),
            new BaseDefinitionCommand(),
            new ImplementationsCommand(),

            // Relationship commands
            new ReferencesCommand(),
            new CallersCommand(),
            new CalleesCommand(),
            new SymbolCommand(),
            new DiagnosticsCommand(),

            // Management commands
            new InitCommand(),
            new StatusCommand(),
            new ShutdownCommand()
        };

        return rootCommand.Parse(args).InvokeAsync();
    }
}
