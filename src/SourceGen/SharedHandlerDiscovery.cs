using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace SourceGen;

/// <summary>
/// Shared discovery utilities for Roslyn incremental generators in this project.
/// Scans the compilation for types implementing relevant CQRS and pipeline interfaces.
/// </summary>
internal static class SharedHandlerDiscovery
{
    /// <summary>
    /// Configures syntax and semantic transforms that discover types implementing
    /// <c>IQueryHandler</c>, <c>ICommandHandler</c>, <c>IRequestHandler</c>, <c>INotificationHandler</c>, and pipeline behaviors.
    /// </summary>
    /// <param name="context">The incremental generator initialization context.</param>
    /// <returns>
    /// A provider that yields the compilation and the collected handler/behavior symbols.
    /// </returns>
    public static IncrementalValueProvider<(Compilation Compilation, ImmutableArray<ISymbol> QueryHandlers, ImmutableArray<ISymbol> CommandHandlers, ImmutableArray<ISymbol> RequestHandlers, ImmutableArray<ISymbol> NotificationHandlers, ImmutableArray<ISymbol> RequestBehaviors, ImmutableArray<ISymbol> VoidBehaviors, ImmutableArray<ISymbol> NotificationBehaviors)>
        DiscoverHandlers(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<INamedTypeSymbol> candidateTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 } or RecordDeclarationSyntax { BaseList.Types.Count: > 0 },
                static (ctx, ct) => (INamedTypeSymbol)ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct)!
            )
            .Where(static s => s is not null)!;

        IncrementalValueProvider<(Compilation, ImmutableArray<INamedTypeSymbol>)> compilationAndTypes = context.CompilationProvider
            .Combine(candidateTypes.Collect());

        return compilationAndTypes.Select(static (tuple, _) =>
        {
            Compilation compilation = tuple.Item1;
            ImmutableArray<INamedTypeSymbol> types = tuple.Item2;

            ImmutableArray<ISymbol>.Builder queryHandlers = ImmutableArray.CreateBuilder<ISymbol>();
            ImmutableArray<ISymbol>.Builder commandHandlers = ImmutableArray.CreateBuilder<ISymbol>();
            ImmutableArray<ISymbol>.Builder requestHandlers = ImmutableArray.CreateBuilder<ISymbol>();
            ImmutableArray<ISymbol>.Builder notificationHandlers = ImmutableArray.CreateBuilder<ISymbol>();
            ImmutableArray<ISymbol>.Builder requestBehaviors = ImmutableArray.CreateBuilder<ISymbol>();
            ImmutableArray<ISymbol>.Builder voidBehaviors = ImmutableArray.CreateBuilder<ISymbol>();
            ImmutableArray<ISymbol>.Builder notificationBehaviors = ImmutableArray.CreateBuilder<ISymbol>();

            foreach (INamedTypeSymbol type in types)
            {
                foreach (INamedTypeSymbol iface in type.AllInterfaces)
                {
                    string ns = iface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (iface.Name == "IQueryHandler" && ns == "global::Dualis.CQRS")
                    {
                        queryHandlers.Add(type);
                    }
                    else if (iface.Name == "ICommandHandler" && ns == "global::Dualis.CQRS")
                    {
                        commandHandlers.Add(type);
                    }
                    else if (iface.Name == "IRequestHandler" && ns == "global::Dualis.CQRS")
                    {
                        requestHandlers.Add(type);
                    }
                    else if (iface.Name == "INotificationHandler" && ns == "global::Dualis.Notifications")
                    {
                        notificationHandlers.Add(type);
                    }
                    else if (ns == "global::Dualis.Pipeline" && iface.TypeArguments.Length == 2 && (iface.Name == "IPipelineBehavior" || iface.Name == "IPipelineBehaviour"))
                    {
                        requestBehaviors.Add(type);
                    }
                    else if (iface.Name == "IPipelineBehavior" && ns == "global::Dualis.Pipeline" && iface.TypeArguments.Length == 1)
                    {
                        voidBehaviors.Add(type);
                    }
                    else if (iface.Name == "INotificationBehavior" && ns == "global::Dualis.Pipeline" && iface.TypeArguments.Length == 1)
                    {
                        notificationBehaviors.Add(type);
                    }
                }
            }

            return (
                compilation,
                queryHandlers.ToImmutable(),
                commandHandlers.ToImmutable(),
                requestHandlers.ToImmutable(),
                notificationHandlers.ToImmutable(),
                requestBehaviors.ToImmutable(),
                voidBehaviors.ToImmutable(),
                notificationBehaviors.ToImmutable());
        });
    }
}
