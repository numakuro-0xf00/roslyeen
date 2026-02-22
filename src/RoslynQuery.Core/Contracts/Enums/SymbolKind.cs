namespace RoslynQuery.Core.Contracts.Enums;

/// <summary>
/// Kind of symbol (simplified from Roslyn's SymbolKind).
/// </summary>
public enum SymbolKind
{
    Unknown,
    Namespace,
    Type,
    Class,
    Struct,
    Interface,
    Enum,
    Delegate,
    Method,
    Property,
    Field,
    Event,
    Parameter,
    Local,
    TypeParameter
}
