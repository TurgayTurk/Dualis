using System.Collections.Concurrent;
using System.Collections.Immutable;
using Dualis.Analyzer.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Dualis.Analyzer.Analyzers;

/// <summary>
/// Warns when a request is sent without a matching IRequestHandler registered.
/// Works in build and IDE by tracking discovered IRequestHandler implementations and seeding from a fast compilation scan.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingHandlerAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Descriptors.DULIS002_MissingHandler];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            Compilation compilation = compilationContext.Compilation;

            INamedTypeSymbol? irequestT = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequest`1");
            INamedTypeSymbol? irequest = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequest");
            INamedTypeSymbol? handlerT = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequestHandler`2");
            INamedTypeSymbol? handler = compilation.GetTypeByMetadataName("Dualis.CQRS.IRequestHandler`1");
            INamedTypeSymbol? isender = compilation.GetTypeByMetadataName("Dualis.ISender");
            INamedTypeSymbol? idualizor = compilation.GetTypeByMetadataName("Dualis.IDualizor");
            if (irequestT is null || irequest is null || handlerT is null || handler is null || isender is null || idualizor is null)
            {
                return;
            }

            // Seed with a quick scan so IDE analysis has an initial complete view
            ConcurrentDictionary<ITypeSymbol, byte> handledRequests = new(SymbolEqualityComparer.Default);
            foreach (ITypeSymbol req in BuildHandledRequestSet(compilation, handlerT, handler, compilationContext.CancellationToken))
            {
                handledRequests.TryAdd(req, 0);
            }

            // Avoid double-reporting: dedupe by location
            ConcurrentDictionary<(SyntaxTree Tree, TextSpan Span), byte> reported = new();

            // Incrementally update as symbols are edited/added
            compilationContext.RegisterSymbolAction(ctx =>
            {
                if (ctx.Symbol is not INamedTypeSymbol type || type.TypeKind != TypeKind.Class || type.IsAbstract)
                {
                    return;
                }

                foreach (INamedTypeSymbol iface in type.AllInterfaces)
                {
                    bool isReqHandler =
                        iface.OriginalDefinition.Equals(handlerT, SymbolEqualityComparer.Default) && iface.TypeArguments.Length == 2
                        || iface.OriginalDefinition.Equals(handler, SymbolEqualityComparer.Default) && iface.TypeArguments.Length == 1;
                    if (!isReqHandler)
                    {
                        continue;
                    }

                    ITypeSymbol req = iface.TypeArguments[0];
                    if (req is ITypeParameterSymbol)
                    {
                        continue;
                    }

                    handledRequests.TryAdd(req, 0);
                }
            }, SymbolKind.NamedType);

            // Operation path
            compilationContext.RegisterOperationAction(ctx =>
            {
                if (ctx.Operation is not IInvocationOperation op)
                {
                    return;
                }

                if (!string.Equals(op.TargetMethod.Name, "Send", StringComparison.Ordinal))
                {
                    return;
                }

                // Gating to Dualis dispatcher
                INamedTypeSymbol? containing = op.TargetMethod.ContainingType;
                if (containing is null || !SymbolEqualityComparer.Default.Equals(containing, isender) && !SymbolEqualityComparer.Default.Equals(containing, idualizor))
                {
                    return;
                }

                if (op.Arguments.Length == 0)
                {
                    return;
                }

                ITypeSymbol? reqType = GetUnderlyingArgType(op.Arguments[0].Value);
                if (reqType is null)
                {
                    return;
                }

                bool isReq = reqType.AllInterfaces.Any(i => i.OriginalDefinition.Equals(irequestT, SymbolEqualityComparer.Default) || i.Equals(irequest, SymbolEqualityComparer.Default));
                if (!isReq)
                {
                    return;
                }

                if (!handledRequests.ContainsKey(reqType))
                {
                    (SyntaxTree Tree, TextSpan Span) key = (op.Syntax.SyntaxTree, op.Syntax.Span);
                    if (reported.TryAdd(key, 0))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS002_MissingHandler, op.Syntax.GetLocation(), reqType.Name));
                    }
                }
            }, OperationKind.Invocation);

            // Syntax fallback (for unbound invocations)
            compilationContext.RegisterSyntaxNodeAction(ctx =>
            {
                if (ctx.Node is not InvocationExpressionSyntax inv)
                {
                    return;
                }

                // If bound to a symbol, let operation path handle it
                if (ctx.SemanticModel.GetSymbolInfo(inv, ctx.CancellationToken).Symbol is IMethodSymbol method)
                {
                    if (!string.Equals(method.Name, "Send", StringComparison.Ordinal))
                    {
                        return;
                    }

                    INamedTypeSymbol? containing = method.ContainingType;
                    if (containing is null || !SymbolEqualityComparer.Default.Equals(containing, isender) && !SymbolEqualityComparer.Default.Equals(containing, idualizor))
                    {
                        return;
                    }
                }
                else
                {
                    // Not bound: try gate by receiver type
                    if (inv.Expression is MemberAccessExpressionSyntax mae)
                    {
                        ITypeSymbol? recvType = ctx.SemanticModel.GetTypeInfo(mae.Expression, ctx.CancellationToken).Type;
                        if (recvType is null || !SymbolEqualityComparer.Default.Equals(recvType, isender) && !SymbolEqualityComparer.Default.Equals(recvType, idualizor))
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                if (inv.ArgumentList is null || inv.ArgumentList.Arguments.Count == 0)
                {
                    return;
                }

                ITypeSymbol? argType = ctx.SemanticModel.GetTypeInfo(inv.ArgumentList.Arguments[0].Expression, ctx.CancellationToken).Type
                    ?? ctx.SemanticModel.GetTypeInfo(inv.ArgumentList.Arguments[0].Expression, ctx.CancellationToken).ConvertedType;
                if (argType is null)
                {
                    return;
                }

                bool isReq2 = argType.AllInterfaces.Any(i => i.OriginalDefinition.Equals(irequestT, SymbolEqualityComparer.Default) || i.Equals(irequest, SymbolEqualityComparer.Default));
                if (!isReq2)
                {
                    return;
                }

                if (!handledRequests.ContainsKey(argType))
                {
                    (SyntaxTree Tree, TextSpan Span) key = (inv.SyntaxTree, inv.Span);
                    if (reported.TryAdd(key, 0))
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS002_MissingHandler, inv.GetLocation(), argType.Name));
                    }
                }
            }, SyntaxKind.InvocationExpression);
        });
    }

    private static HashSet<ITypeSymbol> BuildHandledRequestSet(
        Compilation compilation,
        INamedTypeSymbol handlerT,
        INamedTypeSymbol handler,
        CancellationToken cancellationToken)
    {
        HashSet<ITypeSymbol> set = new(SymbolEqualityComparer.Default);
        Queue<INamespaceSymbol> q = new();
        q.Enqueue(compilation.GlobalNamespace);

        while (q.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            INamespaceSymbol ns = q.Dequeue();
            foreach (INamespaceSymbol child in ns.GetNamespaceMembers())
            {
                q.Enqueue(child);
            }

            foreach (INamedTypeSymbol t in ns.GetTypeMembers())
            {
                if (t.TypeKind != TypeKind.Class || t.IsAbstract)
                {
                    continue;
                }

                foreach (INamedTypeSymbol iface in t.AllInterfaces)
                {
                    bool isReqHandler =
                        iface.OriginalDefinition.Equals(handlerT, SymbolEqualityComparer.Default) && iface.TypeArguments.Length == 2
                        || iface.OriginalDefinition.Equals(handler, SymbolEqualityComparer.Default) && iface.TypeArguments.Length == 1;
                    if (!isReqHandler)
                    {
                        continue;
                    }

                    ITypeSymbol req = iface.TypeArguments[0];
                    if (req is ITypeParameterSymbol)
                    {
                        continue;
                    }

                    set.Add(req);
                }
            }
        }

        return set;
    }

    private static ITypeSymbol? GetUnderlyingArgType(IOperation value)
    {
        if (value is IConversionOperation conv && conv.Operand is not null)
        {
            return conv.Operand.Type ?? conv.Type;
        }

        if (value is IArgumentOperation arg)
        {
            return GetUnderlyingArgType(arg.Value);
        }

        return value.Type;
    }
}
