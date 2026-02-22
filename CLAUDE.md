# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Roslyeen (roslyn-query) is a Roslyn-based static analysis CLI tool for C#/.NET Framework development. It enables CLI-based coding agents to access IDE-level code navigation features (go-to-definition, find references, call hierarchy, etc.) via a daemon architecture.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  roslyn-queryd (daemon process)                         │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │ Solution     │  │ FileWatcher  │  │ IPC Server    │  │
│  │ Manager      │  │ (.cs diff,   │  │ (Unix Socket) │  │
│  │              │  │ .csproj full │  │ JSON-RPC      │  │
│  │ SemanticModel│  │  reload)     │  │               │  │
│  └──────────────┘  └──────────────┘  └───────────────┘  │
│  ┌──────────────────────────────────────────────────┐   │
│  │ Query Executor                                    │   │
│  │ definition / references / callers / callees / ... │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
         ▲ Unix Domain Socket
    ┌────┴────┐   ┌─────────┐
    │ CLI     │   │ MCP     │
    │ client  │   │ client  │
    └─────────┘   └─────────┘
```

## Project Structure

```
src/
├── RoslynQuery.Core/           # QueryService, SolutionManager
├── RoslynQuery.Daemon/         # Daemon process
├── RoslynQuery.Cli/            # CLI entry point
└── tests/
    ├── RoslynQuery.Core.Tests/ # Unit + Integration
    │   ├── Unit/
    │   └── Integration/
    ├── RoslynQuery.E2E.Tests/
    └── RoslynQuery.Perf.Tests/
```

## Development Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run unit/integration tests only
dotnet test tests/RoslynQuery.Core.Tests

# Run a single test
dotnet test --filter "FullyQualifiedName~TestName"

# Run E2E tests (requires VS2022/Build Tools)
dotnet test tests/RoslynQuery.E2E.Tests
```

## CLI Command Structure

```
roslyn-query
├── Navigation
│   ├── definition          # Go to definition
│   ├── base-definition     # Override chain (interface/base class)
│   └── implementations     # Find implementations
├── Relationships
│   ├── references          # Find all references
│   ├── callers             # Call hierarchy (incoming)
│   ├── callees             # Call hierarchy (outgoing)
│   └── dependencies        # Type-level dependencies
├── Structure
│   ├── symbol              # Symbol details
│   ├── signature           # Signature + XML doc
│   ├── members             # Member list
│   ├── hierarchy           # Inheritance hierarchy
│   └── overview            # Project bird's eye view
├── Diagnostics
│   ├── diagnostics         # Compile errors/warnings
│   ├── unused              # Unused code detection
│   └── type-check          # Type compatibility check
└── Management
    ├── init                # Start daemon
    ├── status              # Check status
    └── shutdown            # Stop daemon
```

### Global Options

```
--solution, -s <path>    Solution file path
--project, -p <path>     Single project target
--json                   JSON output
--verbose, -v            Diagnostic output to stderr
```

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success with results |
| 1 | Symbol not found |
| 2 | Solution/project load failure |
| 3 | Argument error |
| 4 | Daemon connection failure |

## Key Implementation Notes

### MSBuildLocator (Critical)

`MSBuildLocator.RegisterInstance()` must be called **before** any MSBuild types are accessed. Isolate MSBuild usage in separate methods:

```csharp
static void Main()
{
    MSBuildLocator.RegisterDefaults();
    DoWork();  // Separate method
}

static void DoWork()
{
    using var workspace = MSBuildWorkspace.Create();
    // ...
}
```

For .NET Framework legacy csproj, prefer `VisualStudioSetup` discovery type (VS2022/Build Tools).

### File Change Handling

| Change | Action | Time |
|--------|--------|------|
| `.cs` | `WithDocumentText` incremental | 10-200ms |
| `.csproj` | Full reload | 3-15s |
| `.sln` | Full reload | 3-15s |

Use 300ms debounce for batching rapid changes.

### Path Output

All file paths should be **solution-root relative** by default for agent compatibility. Use `--absolute-paths` to override.

### Symbol Resolution

Use `SymbolFinder.FindSymbolAtPositionAsync` instead of manual `GetSymbolInfo`/`GetDeclaredSymbol` - it handles edge cases (partial classes, lambdas, implicit conversions).

## Testing Strategy

**Ratio: Unit 60% : Integration 30% : E2E 10%**

- **Unit**: Pure logic (JSON-RPC, path resolution, debounce)
- **Integration**: `AdhocWorkspace` with in-memory sources (no MSBuild dependency)
- **E2E**: Real CLI process with actual `.sln` files

Integration tests are critical - use `TestSolutionBuilder` with `AdhocWorkspace` for fast, reproducible Roslyn behavior tests.

## Dependencies

- Microsoft.CodeAnalysis.Workspaces.MSBuild
- Microsoft.Build.Locator
- System.CommandLine (CLI parsing)
