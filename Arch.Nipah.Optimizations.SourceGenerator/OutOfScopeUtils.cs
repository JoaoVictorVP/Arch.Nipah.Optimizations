using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace Arch.Nipah.Optimizations.SourceGenerator;

public static class OutOfScopeUtils
{
    public static Func<StatementSyntax> ProduceStatementFromOutOfScopeCall(LambdaExpressionSyntax lambda, ExpressionSyntax lambdaExpressionBody)
    {
        if (lambda.FirstAncestorOrSelf<VariableDeclaratorSyntax>() is not null and var dec)
        {
            var name = dec.Identifier.Text;
            var varDec = SyntaxFactory.VariableDeclarator(
                SyntaxFactory.Identifier(name),
                null,
                SyntaxFactory.EqualsValueClause(lambdaExpressionBody)
            );

            var varStatement = SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName("var "),
                    SyntaxFactory.SingletonSeparatedList(varDec)
                )
            );

            return () => varStatement;
        }
        else if (lambda.FirstAncestorOrSelf<AssignmentExpressionSyntax>() is not null and var assign)
        {
            var left = assign.Left;
            var assignStatement = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    left,
                    lambdaExpressionBody
                )
            );

            return () => assignStatement;
        }
        else
            return () => SyntaxFactory.ExpressionStatement(lambdaExpressionBody);
    }
}
