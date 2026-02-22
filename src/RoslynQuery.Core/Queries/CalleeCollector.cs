using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynQuery.Core.Queries;

/// <summary>
/// Syntax walker that collects all method/property invocations within a member.
/// </summary>
public sealed class CalleeCollector : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly List<(ISymbol Symbol, SyntaxNode Node)> _callees = [];

    public CalleeCollector(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    /// <summary>
    /// Gets the collected callees.
    /// </summary>
    public IReadOnlyList<(ISymbol Symbol, SyntaxNode Node)> Callees => _callees;

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
        {
            _callees.Add((methodSymbol, node));
        }
        else if (symbolInfo.CandidateSymbols.Length > 0)
        {
            // Ambiguous - take first candidate
            var candidate = symbolInfo.CandidateSymbols[0];
            if (candidate is IMethodSymbol)
            {
                _callees.Add((candidate, node));
            }
        }

        base.VisitInvocationExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Handle property accesses
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
        {
            // Check if this is a property access (not as part of an invocation)
            if (node.Parent is not InvocationExpressionSyntax)
            {
                _callees.Add((propertySymbol, node));
            }
        }

        base.VisitMemberAccessExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is IMethodSymbol constructorSymbol)
        {
            _callees.Add((constructorSymbol, node));
        }

        base.VisitObjectCreationExpression(node);
    }

    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is IMethodSymbol constructorSymbol)
        {
            _callees.Add((constructorSymbol, node));
        }

        base.VisitImplicitObjectCreationExpression(node);
    }

    public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
    {
        // Handle indexer accesses
        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        if (symbolInfo.Symbol is IPropertySymbol indexerSymbol)
        {
            _callees.Add((indexerSymbol, node));
        }

        base.VisitElementAccessExpression(node);
    }
}
