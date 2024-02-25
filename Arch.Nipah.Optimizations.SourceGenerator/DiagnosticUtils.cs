using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Arch.Nipah.Optimizations.SourceGenerator;

public static class DiagnosticUtils
{
    public static void Error(this SourceProductionContext ctx, Location at, string message, int code)
    {
        ctx.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id: "NIP" + code.ToString("000"),
                title: message,
                messageFormat: message,
                category: "Arch.Nipah.Optimizations",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: message
            ),
            location: at));
    }
    public static void Warning(this SourceProductionContext ctx, Location at, string message, int code)
    {
        ctx.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id: "NIP" + code.ToString("000"),
                title: message,
                messageFormat: message,
                category: "Arch.Nipah.Optimizations",
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true
            ),
            location: at));
    }
    public static void Info(this SourceProductionContext ctx, Location at, string message, int code)
    {
        ctx.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id: "NIP" + code.ToString("000"),
                title: message,
                messageFormat: message,
                category: "Arch.Nipah.Optimizations",
                defaultSeverity: DiagnosticSeverity.Info,
                isEnabledByDefault: true
            ),
            location: at));
    }

    public static void Error(this SourceProductionContext ctx, SyntaxNode at, string message, int code)
        => ctx.Error(at.GetLocation(), message, code);
    public static void Warning(this SourceProductionContext ctx, SyntaxNode at, string message, int code)
        => ctx.Warning(at.GetLocation(), message, code);
    public static void Info(this SourceProductionContext ctx, SyntaxNode at, string message, int code)
        => ctx.Info(at.GetLocation(), message, code);

    public static void Error(this SourceProductionContext ctx, ISymbol at, string message, int code)
        => ctx.Error(at.Locations[0], message, code);
    public static void Warning(this SourceProductionContext ctx, ISymbol at, string message, int code)
        => ctx.Warning(at.Locations[0], message, code);
    public static void Info(this SourceProductionContext ctx, ISymbol at, string message, int code)
        => ctx.Info(at.Locations[0], message, code);
}
