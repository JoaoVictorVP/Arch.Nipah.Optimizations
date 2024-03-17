using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace Arch.Nipah.Optimizations.SourceGenerator.Models;

public readonly struct QueryModel
{
    public MethodHeader Method { get; }
    public int GlobalIndex { get; }
    public string Namespace { get; }
    public InvocationExpressionSyntax QueryCall { get; }

    public QueryModel(MethodHeader method, int globalIndex, string @namespace, InvocationExpressionSyntax queryCall)
    {
        Method = method;
        GlobalIndex = globalIndex;
        Namespace = @namespace;
        QueryCall = queryCall;
    }
}
