using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Arch.Nipah.Optimizations.SourceGenerator;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CSProjAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor noInterceptorsPreviewNamespaces = new(
        id: "NIP000",
        title: "No InterceptorsPreviewNamespaces",
        messageFormat: "No InterceptorsPreviewNamespaces found in the .csproj file. Insert \"<InterceptorsPreviewNamespaces>$(InterceptorsPreviewNamespaces);Arch.Nipah.Optimizations.Interceptors</InterceptorsPreviewNamespaces>\".",
        category: "Arch.Nipah.Optimizations",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "No InterceptorsPreviewNamespaces found in the .csproj file.",
        customTags: "CompilationEnd"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        noInterceptorsPreviewNamespaces
    );

    [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035", Justification = "<Needed>")]
    static string? FindCsProjPath(string syntaxTreeFilePath)
    {
        var path = syntaxTreeFilePath;
        while (path.Length > 0)
        {
            path = System.IO.Path.GetDirectoryName(path);
            var files = System.IO.Directory.GetFiles(path, "*.csproj");
            if (files.Length > 0)
                return files[0];
        }
        return null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035", Justification = "<Needed>")]
    public static string ReadCsProj(string path)
        => System.IO.File.ReadAllText(path);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035", Justification = "<Needed>")]
    public static void WriteCsProj(string path, string content)
        => System.IO.File.WriteAllText(path, content);

    static int CountLines(string text, int untilIndex)
    {
        var count = 0;
        for (var i = 0; i < untilIndex; i++)
            if (text[i] == '\n')
                count++;
        return count;
    }

    static readonly Regex csProjRegex = new(@"\<InterceptorsPreviewNamespaces\>\$\(InterceptorsPreviewNamespaces\);.*Arch\.Nipah\.Optimizations\.Interceptors.*\<\/InterceptorsPreviewNamespaces\>");

    public static readonly Regex PropertyGroupRegex = new(@"\<\/PropertyGroup\>");

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationAction(ctx =>
        {
            var tree = ctx.Compilation.SyntaxTrees.FirstOrDefault();
            if (tree is null)
                return;

            var path = FindCsProjPath(tree.FilePath);
            if (path is null)
                return;

            var csproj = ReadCsProj(path);

            if(csProjRegex.IsMatch(csproj) is false)
            {
                var match = PropertyGroupRegex.Match(csproj);
                if (match.Success is false)
                    return;

                var firstDeclaredType = ctx.Compilation.SyntaxTrees
                    .SelectMany(s => s.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>())
                    .FirstOrDefault();

                if (firstDeclaredType is null)
                    return;

                var lineStart = CountLines(csproj, match.Index);
                var lineEnd = CountLines(csproj, match.Index + match.Length);

                var line = new LinePositionSpan(new LinePosition(lineStart, 0), new LinePosition(lineEnd, 0));

                var location = Location.Create(path, new TextSpan(match.Index, match.Length), line);

                ctx.ReportDiagnostic(Diagnostic.Create(noInterceptorsPreviewNamespaces, location));
            }
        });
    }
}
