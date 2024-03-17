using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Arch.Nipah.Optimizations.SourceGenerator;

public static class SyntaxUtils
{
    public static bool HasAttribute(this LambdaExpressionSyntax lambda, string attributeName, string attributeNms)
        => lambda.AttributeLists.Any(a => a.Attributes.Any(a =>
        {
            var name = a.Name.ToString();
            return name == attributeName || name == $"{attributeName}Attribute"
                || name == $"{attributeNms}.{attributeName}" || name == $"{attributeNms}.{attributeName}Attribute";
        }));

    public static IEnumerable<string> GetAllUsings(this SyntaxNode node)
        => node.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name?.ToString())
            .Where(s => s is not null)
            .Select(s => s!);

    public static IEnumerable<LambdaExpressionParam> ExtractParams(this LambdaExpressionSyntax lambda, SemanticModel sem)
    {
        if (sem.GetSymbolInfo(lambda).Symbol is not IMethodSymbol symbol)
            yield break;

        foreach (var p in symbol.Parameters)
            yield return new LambdaExpressionParam(p.Type.ToDisplayString(), p.Name);
    }

    public static TNode WithEndOfLine<TNode>(this TNode node) where TNode : SyntaxNode
        => node.WithTrailingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, "\n"));

    public static IEnumerable<ExpressionSyntax> FindThrowStatementExpressions(this SyntaxNode node)
        => node.DescendantNodes().OfType<ThrowStatementSyntax>()
            .Where(t => t.Expression is not null)
            .Select(t => t.Expression!);
}
public readonly struct LambdaExpressionParam
{
    public readonly string Type;
    public readonly string Name;
    public readonly bool IsEntity => Type is "Arch.Core.Entity";

    public readonly bool IsValid => Type is not null && Name is not null;

    public LambdaExpressionParam(string type, string name)
    {
        Type = type;
        Name = name;
    }
}
