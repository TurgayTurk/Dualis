using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
            INamedTypeSymbol? exceptionHandler = compilation.GetTypeByMetadataName("Dualis.Pipeline.IRequestExceptionHandler`3");
            INamedTypeSymbol? exceptionAction = compilation.GetTypeByMetadataName("Dualis.Pipeline.IRequestExceptionAction`2");
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
                        foreach (INamedTypeSymbol ri in GetEffectiveInterfaces(req))
                        {
                            if (!ri.OriginalDefinition.Equals(irequestT, SymbolEqualityComparer.Default)
                                || ri.TypeArguments.Length != 1)
                            {
                                continue;
                            }

                            ITypeSymbol riArg = ri.TypeArguments[0];
                            if (SymbolEqualityComparer.Default.Equals(riArg, res))
                            {
                                ok = true;
                                break;
                            }

                            // TResponse may be an independently-constrained type parameter (e.g. `where TResponse : Result<int>`)
                            // rather than being literally `TResponse` in `TRequest : IRequest<TResponse>`. Accept when TRequest's
                            // IRequest<T> argument matches one of TResponse's own constraint types.
                            if (res is ITypeParameterSymbol resTp
                                && resTp.ConstraintTypes.Any(constraint => SymbolEqualityComparer.Default.Equals(riArg, constraint)))
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
                        bool ok = GetEffectiveInterfaces(req).Any(ri =>
                            ri.Equals(irequest, SymbolEqualityComparer.Default) || ri.OriginalDefinition.Equals(irequestT, SymbolEqualityComparer.Default));

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

    /// <summary>
    /// Returns the interfaces reachable from <paramref name="type"/>. For ordinary types this is
    /// <see cref="ITypeSymbol.AllInterfaces"/>. Type parameters report an empty <c>AllInterfaces</c>
    /// even when constrained by an interface (e.g. <c>where TRequest : IQuery&lt;Result&lt;int&gt;&gt;</c>),
    /// so for those we flatten through <see cref="ITypeParameterSymbol.ConstraintTypes"/> instead.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> GetEffectiveInterfaces(ITypeSymbol type)
    {
        if (type is not ITypeParameterSymbol typeParameter)
        {
            return type.AllInterfaces;
        }

        return typeParameter.ConstraintTypes
            .SelectMany(constraint => constraint is INamedTypeSymbol { TypeKind: TypeKind.Interface } namedConstraint
                ? namedConstraint.AllInterfaces.Append(namedConstraint)
                : constraint.AllInterfaces);
    }
}
