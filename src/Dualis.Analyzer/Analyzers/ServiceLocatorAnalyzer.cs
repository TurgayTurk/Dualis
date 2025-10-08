using System.Collections.Immutable;
using Dualis.Analyzer.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dualis.Analyzer.Analyzers;

/// <summary>
/// Suggests avoiding resolving Dualis services via IServiceProvider (service locator).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceLocatorAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Descriptors.DULIS013_ServiceLocatorUsage];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax inv)
        {
            return;
        }

        if (inv.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        string name = memberAccess.Name.Identifier.ValueText;
        if (name is not ("GetService" or "GetRequiredService"))
        {
            return;
        }

        // infer the generic type argument if present: sp.GetRequiredService<ISender>()
        if (context.SemanticModel.GetSymbolInfo(memberAccess.Expression).Symbol is not ILocalSymbol and not IParameterSymbol and not IPropertySymbol)
        {
            // we only care about obvious IServiceProvider instances, don't overreach
            return;
        }

        ITypeSymbol? svType = context.SemanticModel.GetTypeInfo(memberAccess.Expression).Type;
        if (svType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::System.IServiceProvider")
        {
            return;
        }

        ITypeSymbol? requested = null;
        if (memberAccess.Name is GenericNameSyntax g && g.TypeArgumentList.Arguments.Count == 1)
        {
            requested = context.SemanticModel.GetTypeInfo(g.TypeArgumentList.Arguments[0]).Type;
        }
        else if (inv.ArgumentList.Arguments is { Count: 1 } && inv.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax tof)
        {
            requested = context.SemanticModel.GetTypeInfo(tof.Type).Type;
        }

        if (requested is null)
        {
            return;
        }

        string fq = requested.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fq is "global::Dualis.IDualizor" or "global::Dualis.ISender" or "global::Dualis.IPublisher")
        {
            Location loc = inv.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS013_ServiceLocatorUsage, loc, requested.Name));
        }
    }
}
