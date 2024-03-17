using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Arch.Nipah.Optimizations.SourceGenerator;

[Generator]
public class QueryOptimizerGenerator : IIncrementalGenerator
{
    const string InterceptorNamespace = "Arch.Nipah.Optimizations.Interceptors";

    static string GetInterceptorFilePath(SyntaxTree tree, Compilation compilation)
    {
        return compilation.Options.SourceReferenceResolver?.NormalizePath(tree.FilePath, baseFilePath: null) ?? tree.FilePath;
    }
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methods = context.SyntaxProvider.ForAttributeWithMetadataName("Arch.Nipah.Optimizations.OptimizeAttribute",
            (node, ct) =>
            {
                return node is MethodDeclarationSyntax m && m.Body is not null;
            },
            (ctx, ct) =>
            {
                var methodNode = (MethodDeclarationSyntax)ctx.TargetNode;
                var file = GetInterceptorFilePath(methodNode.SyntaxTree, ctx.SemanticModel.Compilation);
                var sem = ctx.SemanticModel;

                var fileUsings = methodNode.SyntaxTree.GetRoot()
                    .GetAllUsings().ToArray();

                // Find namespace of the method
                var namespaceNode = methodNode.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
                var nms = namespaceNode is null ? "" : namespaceNode.Name.ToString();

                // Obtain all the usings for the method
                var usings = new HashSet<string>()
    {
                    "Arch.Core",
                    "System",
                    "System.Collections.Generic",
                    "System.Linq",
                    "System.Text",
                    "System.Runtime.CompilerServices"
                };
                usings.UnionWith(CodeGenUtils.NamespaceAndSubNamespacesFrom(nms));
                usings.UnionWith(fileUsings);

                var header = new MethodHeader(
                    methodName: methodNode.Identifier.Text,
                    file,
                    scope: methodNode.Body!,
                    semantics: sem,
                    usings
                );
                var optimizable = new OptimizableMethodModel(header);

        // Find all method calls of world.Query
                var queryCalls = methodNode.Body!.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression is MemberAccessExpressionSyntax m && m.Name.Identifier.Text == "Query");

        int globalIndex = 0;
        foreach (var call in queryCalls)
        {
            if (call.ArgumentList.Arguments.Count < 2)
                continue;

                    var queryModel = new QueryModel(
                        method: header,
                        globalIndex: globalIndex++,
                        @namespace: nms,
                        queryCall: call
                    );

                    optimizable.Queries.Add(queryModel);

    }

                return optimizable;
            });
    {
        var parts = nms.Split('.');
        for (int i = 0; i < parts.Length; i++)
            yield return string.Join(".", parts.Take(i + 1));
    }

    static void ProduceQueryInterceptor(string methodName, string nms, string file, InvocationExpressionSyntax query, SourceProductionContext ctx, SemanticModel sem, string[] fileUsings, int globalIndex)
    {
        // Get the second argument type for the invocation expression syntax
        if (sem.GetSymbolInfo(query).Symbol is not IMethodSymbol queryInv)
            return;

        var queryType = queryInv.Parameters[1].Type;
        if (queryType is null)
            return;

        // Get the closure body within the second argument
        if (query.ArgumentList.Arguments[1].Expression is not LambdaExpressionSyntax lambda)
        {
            ctx.Info(query, "Only queries called with lambda expressions can be optimized.", 6);
            return;
        }

        if (lambda.HasAttribute("NoOptimizable", "Arch.Nipah.Optimizations"))
            return;

        if (lambda.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) is false)
        {
            ctx.Error(query, "Optimizable queries should be marked as 'static' or with '[NoOptimizable]' attribute.", 1);
            return;
        }
        var queryParams = ExtractParams(lambda, sem);
        var lambdaBody = lambda.Body;
        if (lambdaBody is null)
            return;
        if (lambda.ExpressionBody is not null)
        {
            ctx.Warning(lambda, "Expression bodied lambdas are not supported yet in queries. Make a block expression lambda or mark with '[NoOptimizable]' to suppress this warning.", 2);
            return;
        }

        var loc = query.Expression.DescendantTokens().Last().GetLocation().GetLineSpan();

        var sb = new StringBuilder();
        var usings = new HashSet<string>()
        {
            "Arch.Core",
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Runtime.CompilerServices"
        };
        usings.UnionWith(NamespaceAndSubNamespaces(nms));
        usings.UnionWith(fileUsings);
        foreach (var u in usings)
            sb.AppendLine($"using {u};");

        sb.AppendLine($"namespace {InterceptorNamespace};");

        sb.AppendLine($"public static class {nms.Replace('.', '_')}{methodName}_{globalIndex}_Interceptor");
        sb.AppendLine("{");
        sb.AppendLine($"    [InterceptsLocation(@\"{file}\", {loc.StartLinePosition.Line + 1}, {loc.StartLinePosition.Character + 1})]");
        sb.AppendLine("     [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"    public static void Intercept(this World world, in QueryDescription description, {queryType.ToDisplayString()} _)");
        sb.AppendLine("    {");
        // Write the closure body into the interceptor
        sb.AppendLine(TransformBody(lambdaBody, queryParams, sem, ctx).ToFullString());
        sb.AppendLine("    }");
        sb.AppendLine("}");

        ctx.AddSource($"{nms}.{methodName}Interceptor_{GetHashCode($"{file}_{query.GetLocation().GetLineSpan().StartLinePosition}_{globalIndex}")}", sb.ToString());
    }
    static SyntaxNode TransformBody(CSharpSyntaxNode body, List<QueryParam> queryParams, SemanticModel sem, SourceProductionContext ctx)
    {
        // Let's find all out of scope breaks
        var throws = new HashSet<string>(body.DescendantNodes().OfType<ThrowStatementSyntax>()
            .Where(t => t.Expression is not null)
            .Select(t => t.Expression!)
            .Where(t => sem.GetTypeInfo(t).Type?.ToString() is "Arch.Nipah.Optimizations.Optimizer.Break")
            .Select(t => t.ToFullString()));

        // Let's find all the method calls to Optimizer.OutOfScope
        var outOfScopeCalls = body.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression is MemberAccessExpressionSyntax m && m.Name.Identifier.Text == "OutOfScope");
        // Now we'll pick all the closure bodies (or expression bodies) and store them in a list for later usage and remove from the original body
        var closures = new List<Func<StatementSyntax>>();
        foreach (var call in outOfScopeCalls)
        {
            if (call.ArgumentList.Arguments.Count < 1)
                continue;
            if (call.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax closure)
            {
                ctx.Error(call, "OutOfScope should be called with a lambda expression.", 4);
                continue;
            }
            if (closure is { ExpressionBody: null })
            {
                ctx.Error(closure, "Expression bodied lambdas are not supported yet in 'Optimizer.OutOfScope'.", 3);
                continue;
            }

            if(closure.FirstAncestorOrSelf<VariableDeclaratorSyntax>() is not null and var dec)
            {
                var name = dec.Identifier.Text;
                var varDec = SyntaxFactory.VariableDeclarator(
                    SyntaxFactory.Identifier(name),
                    null,
                    SyntaxFactory.EqualsValueClause(closure.ExpressionBody)
                );

                var varStatement = SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.IdentifierName("var "),
                        SyntaxFactory.SingletonSeparatedList(varDec)
                    )
                );

                closures.Add(() => varStatement);
            }
            else if(closure.FirstAncestorOrSelf<AssignmentExpressionSyntax>() is not null and var assign)
            {
                var left = assign.Left;
                var assignStatement = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        left,
                        closure.ExpressionBody
                    )
                );

                closures.Add(() => assignStatement);
            }
            else
                closures.Add(() => SyntaxFactory.ExpressionStatement(closure.ExpressionBody));
        }
        body = body.RemoveNodes(outOfScopeCalls
            .Select(c => c.FirstAncestorOrSelf<StatementSyntax>()!), SyntaxRemoveOptions.KeepNoTrivia)
            ?? throw new InvalidOperationException("Failed to remove nodes");

        // And we will replace all 'return's with 'continue's to make the code work
        body = body.ReplaceNodes(body.DescendantNodes().OfType<ReturnStatementSyntax>(),
                       (old, _) => SyntaxFactory.ContinueStatement()
                       .WithTrailingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, "\n")));

        // And replace all occurrences of throw Arch.Nipah.Optimizations.Optimizer.OptimizerBreak with a real break statement
        var newThrows = body.DescendantNodes().OfType<ThrowStatementSyntax>()
            .Where(t => t.Expression is not null)
            .Where(t => throws.Contains(t.Expression!.ToString()));
        body = body.ReplaceNodes(newThrows,
                                  (old, _) => SyntaxFactory.BreakStatement()
                                  .WithTrailingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, "\n")));

        // Now, let's pick all the resting body code and transform it this way:
        // foreach(var chunk in world.Query(description))
        // {
        //     foreach(var index in chunk)
        //     {
        //         rest of original body goes here
        //     }
        // }
        // TODO: Get all the params and types and replace them accordingly to make this work
        var entityParam = queryParams.Find(queryParams => queryParams.IsEntity);

        var variableDefinitions = queryParams.Count(qp => qp.IsEntity is false) switch
        {
            0 => "",
            1 => ((Func<string>)(() =>
            {
                var first = queryParams.First(qp => qp.IsEntity is false);
                return $"ref var {first.Name} = ref Unsafe.Add(ref arr, index);";
            }))(),
            _ => string.Join("\n", queryParams
                .Where(qp => qp.IsEntity is false)
                .Select((qp, i) => $"ref var {qp.Name} = ref Unsafe.Add(ref arr.t{i}, index);"))
        };

        var parsed = SyntaxFactory.ParseSyntaxTree($$"""
            {
                foreach(var chunk in world.Query(description).GetChunkIterator())
                {
                    var arr = chunk.GetFirst<{{string.Join(", ", queryParams
                        .Where(qp => qp.IsEntity is false)
                        .Select(qp => qp.Type))}}>();
                    foreach(var index in chunk)
                    {
                        {{(entityParam.IsValid ? $"ref var entity = ref chunk.Entity(index);" : "")}}
                        {{variableDefinitions}}

                        int replace = 0;
                    }
                }
            }
            """).GetRoot();
        var replace = parsed.DescendantNodes().OfType<LocalDeclarationStatementSyntax>()
            .Where(l => l.Declaration.Variables.Any(v => v.Identifier.Text is "replace")).First();
        parsed = parsed.ReplaceNode(replace, body);
        body = parsed as CSharpSyntaxNode ?? throw new InvalidOperationException("Failed to replace node");

        // Now we'll add the closures back to the body
        var bodyBlock = body.DescendantNodesAndSelf().OfType<BlockSyntax>().First();
        if (closures.Count > 0)
        {
            var newBody = bodyBlock!.Statements
                .InsertRange(0, closures.Select(p => p().WithTrailingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, "\n"))));
            body = bodyBlock!.WithStatements(newBody);
        }

        return body;
    }

    static List<QueryParam> ExtractParams(LambdaExpressionSyntax closure, SemanticModel sem)
    {
        var args = new List<QueryParam>(8);

        var symbol = sem.GetSymbolInfo(closure).Symbol as IMethodSymbol;
        if (symbol is null)
            return args;

        foreach (var p in symbol.Parameters)
            args.Add(new QueryParam(p.Type.ToDisplayString(), p.Name));

        return args;
    }

    static int GetHashCode(string str)
    {
        int hash = 0;
        for (int i = 0; i < str.Length; i++)
            hash = (hash << 5) - hash + str[i];
        return hash;
    }
}
readonly struct QueryParam
{
    public readonly string Type;
    public readonly string Name;
    public readonly bool IsEntity => Type is "Arch.Core.Entity";

    public readonly bool IsValid => Type is not null && Name is not null;

    public QueryParam(string type, string name)
    {
        Type = type;
        Name = name;
    }
}
