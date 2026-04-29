using System.Collections.Concurrent;
using System.Collections.Immutable;
using Dualis.Analyzer.Diagnostics;
using Microsoft.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dualis.Analyzer.Analyzers;

/// <summary>
/// Reports when multiple IRequestExceptionHandler implementations are discovered for the same request/response/exception contract.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateExceptionHandlersAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Descriptors.DULIS015_DuplicateExceptionHandlers];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            Compilation compilation = compilationContext.Compilation;
            INamedTypeSymbol? exceptionHandler = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequestExceptionHandler`3");
            if (exceptionHandler is null)
            {
                return;
            }

            var byContract = new ConcurrentDictionary<string, ConcurrentBag<INamedTypeSymbol>>(StringComparer.Ordinal);
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
                    if (!iface.OriginalDefinition.Equals(exceptionHandler, SymbolEqualityComparer.Default)
                        || iface.TypeArguments.Length != 3)
                    {
                        continue;
                    }

                    ITypeSymbol req = iface.TypeArguments[0];
                    ITypeSymbol res = iface.TypeArguments[1];
                    ITypeSymbol ex = iface.TypeArguments[2];

                    if (req is ITypeParameterSymbol || res is ITypeParameterSymbol || ex is ITypeParameterSymbol)
                    {
                        continue;
                    }

                    string reqKey = req.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    string resKey = res.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    string exKey = ex.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    string key = reqKey + "|" + resKey + "|" + exKey;

                    ConcurrentBag<INamedTypeSymbol> list = byContract.GetOrAdd(key, _ => []);
                    list.Add(type);

                    if (list.Count > 1 && reported.TryAdd(type, 0))
                    {
                        Location loc = type.Locations.Length > 0 ? type.Locations[0] : Location.None;
                        string reqName = req.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                        string resName = res.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                        string exName = ex.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                        ctx.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS015_DuplicateExceptionHandlers, loc, reqName, resName, exName));
                    }
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(ctx =>
            {
                foreach (KeyValuePair<string, ConcurrentBag<INamedTypeSymbol>> kv in byContract)
                {
                    INamedTypeSymbol[] list = [.. kv.Value];
                    if (list.Length <= 1)
                    {
                        continue;
                    }

                    INamedTypeSymbol first = list[0];
                    ImmutableArray<INamedTypeSymbol> firstIfaces = first.AllInterfaces;
                    INamedTypeSymbol? firstContract = firstIfaces
                        .FirstOrDefault(i => i.OriginalDefinition.Equals(exceptionHandler, SymbolEqualityComparer.Default) && i.TypeArguments.Length == 3);
                    string reqName = firstContract?.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? "<unknown>";
                    string resName = firstContract?.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? "<unknown>";
                    string exName = firstContract?.TypeArguments[2].ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? "<unknown>";

                    for (int i = 1; i < list.Length; i++)
                    {
                        INamedTypeSymbol duplicate = list[i];
                        if (!reported.TryAdd(duplicate, 0))
                        {
                            continue;
                        }

                        Location loc = duplicate.Locations.Length > 0 ? duplicate.Locations[0] : Location.None;
                        ctx.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS015_DuplicateExceptionHandlers, loc, reqName, resName, exName));
                    }
                }
            });
        });
    }
}
