using System.Collections.Concurrent;
using System.Collections.Immutable;
using Dualis.Analyzer.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dualis.Analyzer.Analyzers;

/// <summary>
/// Reports when multiple IRequestHandler implementations are discovered for the same request type.
/// Uses symbol actions for live diagnostics and a compilation-end aggregation as a safety net.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateHandlersAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Descriptors.DULIS003_DuplicateHandlers];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            Compilation compilation = compilationContext.Compilation;
            INamedTypeSymbol? handlerT = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequestHandler`2");
            INamedTypeSymbol? handler = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequestHandler`1");

            // Thread-safe aggregation across parallel symbol actions
            var byRequest = new ConcurrentDictionary<ITypeSymbol, ConcurrentBag<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            // Track which handler symbols we've already reported on to avoid duplicate diagnostics
            var reported = new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default);

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
                    bool isReqHandler = handlerT is not null && iface.OriginalDefinition.Equals(handlerT, SymbolEqualityComparer.Default) && iface.TypeArguments.Length == 2
                                        || handler is not null && iface.OriginalDefinition.Equals(handler, SymbolEqualityComparer.Default) && iface.TypeArguments.Length == 1;
                    if (!isReqHandler)
                    {
                        continue;
                    }

                    ITypeSymbol req = iface.TypeArguments[0];
                    if (req is ITypeParameterSymbol)
                    {
                        continue;
                    }

                    ConcurrentBag<INamedTypeSymbol> list = byRequest.GetOrAdd(req, _ => []);
                    list.Add(type);

                    // Live-report duplicate as soon as we observe the second handler for the same request
                    if (list.Count > 1 && reported.TryAdd(type, 0))
                    {
                        Location loc = type.Locations.Length > 0 ? type.Locations[0] : Location.None;
                        string reqName = req.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                        ctx.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS003_DuplicateHandlers, loc, reqName));
                    }
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(ctx =>
            {
                foreach (KeyValuePair<ITypeSymbol, ConcurrentBag<INamedTypeSymbol>> kv in byRequest)
                {
                    INamedTypeSymbol[] list = [.. kv.Value];
                    if (list.Length <= 1)
                    {
                        continue;
                    }

                    // Report on duplicates (keep first occurrence as the implicit winner)
                    for (int i = 1; i < list.Length; i++)
                    {
                        INamedTypeSymbol duplicate = list[i];
                        if (!reported.TryAdd(duplicate, 0))
                        {
                            // Already reported during symbol action
                            continue;
                        }

                        Location loc = duplicate.Locations.Length > 0 ? duplicate.Locations[0] : Location.None;
                        string reqName = kv.Key.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                        ctx.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS003_DuplicateHandlers, loc, reqName));
                    }
                }
            });
        });
    }
}
