using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Arch.Nipah.Optimizations.SourceGenerator.Models;

public class MethodHeader
{
    public string MethodName { get; }
    public string File { get; }
    public BlockSyntax Scope { get; }
    public SemanticModel Semantics { get; }
    public HashSet<string> Usings { get; }

    public MethodHeader(string methodName, string file, BlockSyntax scope, SemanticModel semantics, HashSet<string> usings)
    {
        MethodName = methodName;
        File = file;
        Scope = scope;
        Semantics = semantics;
        Usings = usings;
    }
}
