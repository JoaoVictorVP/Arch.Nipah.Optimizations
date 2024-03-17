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

        context.RegisterSourceOutput(methods, (ctx, optimizable) =>
    {
            foreach(var query in optimizable.Queries)
                ProduceQueryInterceptor(query, ctx);
        });
    }

    static void ProduceQueryInterceptor(QueryModel optimizableQuery, SourceProductionContext ctx)
    {
        var query = optimizableQuery.QueryCall;
        var sem = optimizableQuery.Method.Semantics;
        var usings = optimizableQuery.Method.Usings;
        var methodName = optimizableQuery.Method.MethodName;
        var file = optimizableQuery.Method.File;
        var nms = optimizableQuery.Namespace;
        var globalIndex = optimizableQuery.GlobalIndex;

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
        var queryParams = ExtractECSParams(lambda, sem);
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
        
        foreach (var u in usings)
            sb.AppendLine($"using {u};");

        sb.AppendLine($"namespace {InterceptorNamespace};");

        sb.AppendLine($"public static class {nms.Replace('.', '_')}{methodName}_{globalIndex}_Interceptor");
        sb.AppendLine("{");

        // Attribute to hint the compiler to intercept the method
        // first argument is the file path
        // second argument is the line number offset by 1
        sb.Indent().WithAttribute("InterceptsLocation")
            .Argument($"@\"{file}\"")
            .Argument(loc.StartLinePosition.Line + 1)
            .Into();

        // Attribute to hint the JIT to inline the interceptor (if possible)
        sb.Indent().WithAttribute("MethodImpl")
            .Argument("MethodImplOptions.AggressiveInlining")
            .Into();

        // The interceptor method
        sb.Indent().AppendLine($"public static void Intercept(this World world, in QueryDescription description, {queryType.ToDisplayString()} _)");
        sb.Indent().AppendLine("{");

        // Write the closure body into the interceptor
        var transformedBody = TransformBody(lambdaBody, queryParams, sem, ctx).ToFullString();
        sb.AppendLine(transformedBody);

        sb.Indent().AppendLine("}");
        sb.AppendLine("}");

        // Hash to differentiate the interceptor files
        var hash = $"{file}_{query.GetLocation().GetLineSpan().StartLinePosition}_{globalIndex}".GetDeterministicHashCode();
        ctx.AddSource($"{nms}.{methodName}Interceptor_{hash}", sb.ToString());
    }
    static SyntaxNode TransformBody(CSharpSyntaxNode body, List<QueryParam> queryParams, SemanticModel sem, SourceProductionContext ctx)
    {
        // Let's find all out of scope breaks
        var throws = new HashSet<string>(body.FindThrowStatementExpressions()
            .Where(t => sem.GetTypeInfo(t).Type?.ToString() is "Arch.Nipah.Optimizations.Optimizer.Break")
            .Select(t => t.ToFullString()));

        // Let's find all the method calls to Optimizer.OutOfScope
        var outOfScopeCalls = body.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression is MemberAccessExpressionSyntax m
                && m.Name.Identifier.Text == "OutOfScope");
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

            var producer = OutOfScopeUtils.ProduceStatementFromOutOfScopeCall(
                closure,
                lambdaExpressionBody: closure.ExpressionBody
                );
            closures.Add(producer);
            }
        body = body.RemoveNodes(outOfScopeCalls
            .Select(c => c.FirstAncestorOrSelf<StatementSyntax>()!), SyntaxRemoveOptions.KeepNoTrivia)
            ?? throw new InvalidOperationException("Failed to remove nodes");

        // And we will replace all 'return's with 'continue's to make the code work
        body = body.ReplaceNodes(
            nodes: body.DescendantNodes().OfType<ReturnStatementSyntax>(),
            computeReplacementNode: (_, _) => SyntaxFactory.ContinueStatement().WithEndOfLine()
        );

        // And replace all occurrences of throw Arch.Nipah.Optimizations.Optimizer.OptimizerBreak with a real break statement
        var newThrows = body.DescendantNodes().OfType<ThrowStatementSyntax>()
            .Where(t => t.Expression is not null)
            .Where(t => throws.Contains(t.Expression!.ToString()));
        body = body.ReplaceNodes(
            nodes: newThrows,
            computeReplacementNode: (_, _) => SyntaxFactory.BreakStatement().WithEndOfLine()
        );

        // Now, let's pick all the resting body code and transform it this way:
        // foreach(var chunk in world.Query(description))
        // {
        //     foreach(var index in chunk)
        //     {
        //         rest of original body goes here
        //     }
        // }
        var entityParam = queryParams.Find(queryParams => queryParams.IsEntity);

        string variableDefinitions = BuildVariableDefinitionsForQuery(queryParams);

        // Define the manual query iteration
        var optimizedQueryLoop = SyntaxFactory.ParseSyntaxTree($$"""
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

        // Replace the body with the new parsed body
        var replace = optimizedQueryLoop.DescendantNodes()
            .OfType<LocalDeclarationStatementSyntax>()
            .Where(l => l.Declaration.Variables.Any(v => v.Identifier.Text is "replace"))
            .First();
        optimizedQueryLoop = optimizedQueryLoop.ReplaceNode(replace, body);
        body = optimizedQueryLoop as CSharpSyntaxNode ?? throw new InvalidOperationException("Failed to replace node");

        // Now we'll add the closures back to the body
        var bodyBlock = body.DescendantNodesAndSelf().OfType<BlockSyntax>().First();
        if (closures.Count > 0)
        {
            var newBody = bodyBlock!.Statements
                .InsertRange(0, closures.Select(p => p().WithEndOfLine()));
            body = bodyBlock!.WithStatements(newBody);
        }

        return body;
    }

    static string BuildVariableDefinitionsForQuery(List<ECSQueryParam> queryParams)
    {
        var nonEntityParams = queryParams.Where(qp => qp.IsEntity is false);

        string variableDefinitions;
        switch (nonEntityParams.Count())
        {
            case 0:
            {
                variableDefinitions = "";
                break;
            }
            case 1:
            {
                var first = nonEntityParams.First();
                variableDefinitions = $"ref var {first.Name} = ref Unsafe.Add(ref arr, index);";
                break;
    }
            default:
    {
                variableDefinitions = string.Join("\n", nonEntityParams
                    .Select((qp, i) => $"ref var {qp.Name} = ref Unsafe.Add(ref arr.t{i}, index);"));
                break;
            }
        };
        return variableDefinitions;
    }

    static List<ECSQueryParam> ExtractECSParams(LambdaExpressionSyntax closure, SemanticModel sem)
        => closure.ExtractParams(sem).Select(p => new ECSQueryParam(p)).ToList();
}
readonly struct ECSQueryParam
{
    readonly LambdaExpressionParam param;
    public readonly string Type => param.Type;
    public readonly string Name => param.Name;
    public readonly bool IsEntity => Type is "Arch.Core.Entity";

    public readonly bool IsValid => Type is not null && Name is not null;

    public ECSQueryParam(LambdaExpressionParam param)
        => this.param = param;
}
