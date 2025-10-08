using System.Collections.Immutable;
using Dualis.Analyzer.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dualis.Analyzer.Analyzers;

/// <summary>
/// Warns when a notification is published but no handler is found in the compilation.
/// Uses a compilation-start cache for handled notification types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingNotificationHandlerAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Descriptors.DULIS006_MissingNotificationHandler, Descriptors.DULIS007_TokenNotPassed];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            Compilation compilation = compilationContext.Compilation;
            INamedTypeSymbol? inotification = compilation.GetTypeByMetadataName("Dualis.Notifications.INotification");
            INamedTypeSymbol? inotificationHandler = compilation.GetTypeByMetadataName("Dualis.Notifications.INotificationHandler`1");
            INamedTypeSymbol? ipublisher = compilation.GetTypeByMetadataName("Dualis.IPublisher");
            INamedTypeSymbol? idualizor = compilation.GetTypeByMetadataName("Dualis.IDualizor");
            if (inotification is null || inotificationHandler is null || ipublisher is null || idualizor is null)
            {
                return;
            }

            HashSet<ITypeSymbol> handledNotifications = BuildHandledNotificationSet(compilation, inotificationHandler, compilationContext.CancellationToken);

            compilationContext.RegisterSyntaxNodeAction(ctx =>
            {
                if (ctx.Node is not InvocationExpressionSyntax inv)
                {
                    return;
                }

                ISymbol? sym = ctx.SemanticModel.GetSymbolInfo(inv).Symbol;
                if (sym is not IMethodSymbol method)
                {
                    return;
                }

                if (!string.Equals(method.Name, "Publish", StringComparison.Ordinal))
                {
                    return;
                }

                INamedTypeSymbol? containingType = method.ContainingType;
                if (containingType is null)
                {
                    return;
                }

                if (!SymbolEqualityComparer.Default.Equals(containingType, ipublisher)
                    && !SymbolEqualityComparer.Default.Equals(containingType, idualizor))
                {
                    return;
                }

                if (inv.ArgumentList.Arguments.Count == 0)
                {
                    return;
                }

                // Suggest passing token when available
                SuggestTokenIfAvailable(ctx, inv);

                ITypeSymbol? noteType = ctx.SemanticModel.GetTypeInfo(inv.ArgumentList.Arguments[0].Expression).Type;
                if (noteType is null)
                {
                    return;
                }

                // Only notifications are relevant
                bool isNotification = false;
                ImmutableArray<INamedTypeSymbol> ifaces = noteType.AllInterfaces;
                for (int i = 0; i < ifaces.Length; i++)
                {
                    INamedTypeSymbol iface = ifaces[i];
                    if (iface.Equals(inotification, SymbolEqualityComparer.Default))
                    {
                        isNotification = true;
                        break;
                    }
                }
                if (!isNotification)
                {
                    return;
                }

                if (!handledNotifications.Contains(noteType))
                {
                    Location loc = inv.GetLocation();
                    ctx.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS006_MissingNotificationHandler, loc, noteType.Name));
                }
            }, SyntaxKind.InvocationExpression);
        });
    }

    private static HashSet<ITypeSymbol> BuildHandledNotificationSet(
        Compilation compilation,
        INamedTypeSymbol inotificationHandler,
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

                ImmutableArray<INamedTypeSymbol> ifaces = t.AllInterfaces;
                for (int i = 0; i < ifaces.Length; i++)
                {
                    INamedTypeSymbol iface = ifaces[i];
                    if (iface.OriginalDefinition.Equals(inotificationHandler, SymbolEqualityComparer.Default) && iface.TypeArguments.Length == 1)
                    {
                        ITypeSymbol n = iface.TypeArguments[0];
                        if (n is ITypeParameterSymbol)
                        {
                            continue;
                        }
                        set.Add(n);
                    }
                }
            }
        }

        return set;
    }

    private static void SuggestTokenIfAvailable(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax inv)
    {
        SeparatedSyntaxList<ArgumentSyntax> args = inv.ArgumentList.Arguments;
        if (args.Count >= 2)
        {
            ITypeSymbol? argType = context.SemanticModel.GetTypeInfo(args[args.Count - 1].Expression).ConvertedType;
            if (argType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken")
            {
                return; // already passed
            }
        }

        ISymbol? tokenSymbol = context.SemanticModel.LookupSymbols(inv.SpanStart, name: "cancellationToken").FirstOrDefault();
        ITypeSymbol? tokenType = tokenSymbol switch
        {
            ILocalSymbol ls => ls.Type,
            IParameterSymbol ps => ps.Type,
            _ => null,
        };
        if (tokenType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken")
        {
            Location loc = inv.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.DULIS007_TokenNotPassed, loc, "Publish"));
        }
    }
}
