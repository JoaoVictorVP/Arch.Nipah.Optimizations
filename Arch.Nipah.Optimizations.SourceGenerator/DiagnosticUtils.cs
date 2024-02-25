using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Arch.Nipah.Optimizations.SourceGenerator;

public static class DiagnosticUtils
{
    public static void Error(this SourceProductionContext ctx, SyntaxNode at, string message)
    {
        ctx.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id: "NIP001",
                title: message,
                messageFormat: message,
                category: "Arch.Nipah.Optimizations",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: message
            ),
            location: at.GetLocation()));
    }
    public static void Warning(this SourceProductionContext ctx, SyntaxNode at, string message)
    {
        ctx.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id: "NIP001",
                title: message,
                messageFormat: message,
                category: "Arch.Nipah.Optimizations",
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true
            ),
            location: at.GetLocation()));
    }
    public static void Info(this SourceProductionContext ctx, SyntaxNode at, string message)
    {
        ctx.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id: "NIP001",
                title: message,
                messageFormat: message,
                category: "Arch.Nipah.Optimizations",
                defaultSeverity: DiagnosticSeverity.Info,
                isEnabledByDefault: true
            ),
            location: at.GetLocation()));
    }

    public static void Error(this SourceProductionContext ctx, ISymbol at, string message)
    {
        ctx.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id: "NIP001",
                title: message,
                messageFormat: message,
                category: "Arch.Nipah.Optimizations",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true
            ),
            location: at.Locations[0]));
    }
    public static void Warning(this SourceProductionContext ctx, ISymbol at, string message)
    {
        ctx.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id: "NIP001",
                title: message,
                messageFormat: message,
                category: "Arch.Nipah.Optimizations",
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true
            ),
            location: at.Locations[0]));
    }
    public static void Info(this SourceProductionContext ctx, ISymbol at, string message)
    {
        ctx.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id: "NIP001",
                title: message,
                messageFormat: message,
                category: "Arch.Nipah.Optimizations",
                defaultSeverity: DiagnosticSeverity.Info,
                isEnabledByDefault: true
            ),
            location: at.Locations[0]));
    }
}
