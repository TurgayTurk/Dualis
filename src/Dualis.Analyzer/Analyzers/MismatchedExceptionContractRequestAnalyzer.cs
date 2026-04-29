using System.Collections.Immutable;
using Dualis.Analyzer.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dualis.Analyzer.Analyzers;

/// <summary>
/// Ensures exception contract request types implement the required IRequest shape.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MismatchedExceptionContractRequestAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Descriptors.DULIS014_MismatchedExceptionHandlerRequest];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            Compilation compilation = compilationContext.Compilation;
            INamedTypeSymbol? exceptionHandler = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequestExceptionHandler`3");
            INamedTypeSymbol? exceptionAction = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequestExceptionAction`2");
            INamedTypeSymbol? irequestT = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequest`1");
            INamedTypeSymbol? irequest = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequest");
            if (irequestT is null || irequest is null || exceptionHandler is null && exceptionAction is null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(ctx =>
            {
                if (ctx.Symbol is not INamedTypeSymbol type)
                {
                    return;
                }

                if (type.TypeKind != TypeKind.Class || type.IsAbstract)
                {
                    return;
                }

                ImmutableArray<INamedTypeSymbol> ifaces = type.AllInterfaces;
                for (int i = 0; i < ifaces.Length; i++)
                {
                    INamedTypeSymbol iface = ifaces[i];
                    if (exceptionHandler is not null
                        && iface.OriginalDefinition.Equals(exceptionHandler, SymbolEqualityComparer.Default)
                        && iface.TypeArguments.Length == 3)
                    {
                        ITypeSymbol req = iface.TypeArguments[0];
                        ITypeSymbol res = iface.TypeArguments[1];
                        bool ok = false;
                        ImmutableArray<INamedTypeSymbol> reqIfaces = req.AllInterfaces;
                        for (int r = 0; r < reqIfaces.Length; r++)
                        {
                            INamedTypeSymbol ri = reqIfaces[r];
                            if (ri.OriginalDefinition.Equals(irequestT, SymbolEqualityComparer.Default)
                                && ri.TypeArguments.Length == 1
                                && SymbolEqualityComparer.Default.Equals(ri.TypeArguments[0], res))
                            {
                                ok = true;
                                break;
                            }
                        }

                        if (!ok)
                        {
                            Location loc = type.Locations.Length > 0 ? type.Locations[0] : Location.None;
                            string reqDisplay = req.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                            string needed = $"Dualis.CQRS.IRequest<{res.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}>";
                            ctx.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS014_MismatchedExceptionHandlerRequest, loc, reqDisplay, needed));
                        }
                    }
                    else if (exceptionAction is not null
                             && iface.OriginalDefinition.Equals(exceptionAction, SymbolEqualityComparer.Default)
                             && iface.TypeArguments.Length == 2)
                    {
                        ITypeSymbol req = iface.TypeArguments[0];
                        bool ok = false;
                        ImmutableArray<INamedTypeSymbol> reqIfaces = req.AllInterfaces;
                        for (int r = 0; r < reqIfaces.Length; r++)
                        {
                            INamedTypeSymbol ri = reqIfaces[r];
                            if (ri.Equals(irequest, SymbolEqualityComparer.Default) || ri.OriginalDefinition.Equals(irequestT, SymbolEqualityComparer.Default))
                            {
                                ok = true;
                                break;
                            }
                        }

                        if (!ok)
                        {
                            Location loc = type.Locations.Length > 0 ? type.Locations[0] : Location.None;
                            string reqDisplay = req.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                            ctx.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS014_MismatchedExceptionHandlerRequest, loc, reqDisplay, "Dualis.CQRS.IRequest"));
                        }
                    }
                }
            }, SymbolKind.NamedType);
        });
    }
}
