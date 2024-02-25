using Microsoft.CodeAnalysis;
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
}
