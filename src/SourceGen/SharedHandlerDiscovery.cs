using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dualis.SourceGen;

/// <summary>
/// Shared discovery utilities for Roslyn incremental generators in this project.
/// Scans the compilation for types implementing relevant CQRS and pipeline interfaces.
/// </summary>
internal static class SharedHandlerDiscovery
{
    /// <summary>
    /// Configures syntax and semantic transforms that discover types implementing
    /// <c>IRequestHandler</c>, <c>INotificationHandler</c>, and pipeline behaviors.
    /// </summary>
    /// <param name="context">The incremental generator initialization context.</param>
    /// <returns>
    /// A provider that yields the compilation and the collected handler/behavior symbols.
    /// </returns>
    public static IncrementalValueProvider<(Compilation Compilation, ImmutableArray<ISymbol> RequestHandlers, ImmutableArray<ISymbol> NotificationHandlers, ImmutableArray<ISymbol> RequestBehaviors, ImmutableArray<ISymbol> VoidBehaviors, ImmutableArray<ISymbol> NotificationBehaviors)>
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

            ImmutableArray<ISymbol>.Builder requestHandlers = ImmutableArray.CreateBuilder<ISymbol>();
            ImmutableArray<ISymbol>.Builder notificationHandlers = ImmutableArray.CreateBuilder<ISymbol>();
            ImmutableArray<ISymbol>.Builder requestBehaviors = ImmutableArray.CreateBuilder<ISymbol>();
            ImmutableArray<ISymbol>.Builder voidBehaviors = ImmutableArray.CreateBuilder<ISymbol>();
            ImmutableArray<ISymbol>.Builder notificationBehaviors = ImmutableArray.CreateBuilder<ISymbol>();

            // In-project discovery (current behavior)
            ClassifyHandlersAndBehaviors(types, requestHandlers,
                notificationHandlers, requestBehaviors, voidBehaviors, notificationBehaviors);

            // Cross-assembly discovery (new): scan referenced assemblies for public, non-abstract classes
            foreach (MetadataReference reference in compilation.References)
            {
                ISymbol? symbol = compilation.GetAssemblyOrModuleSymbol(reference);
                if (symbol is not IAssemblySymbol asm)
                {
                    continue;
                }

                // Skip self if ever encountered
                if (SymbolEqualityComparer.Default.Equals(asm, compilation.Assembly))
                {
                    continue;
                }

                foreach (INamedTypeSymbol type in EnumerateAllTypes(asm))
                {
                    if (type.TypeKind != TypeKind.Class || type.IsAbstract)
                    {
                        continue;
                    }

                    // Only include symbols that are safely accessible from this compilation.
                    // Keep it strict to avoid accessibility errors in generated code: require effective public visibility.
                    if (!IsEffectivelyPublic(type))
                    {
                        continue;
                    }

                    ClassifySingleType(type, requestHandlers,
                        notificationHandlers, requestBehaviors, voidBehaviors, notificationBehaviors);
                }
            }

            return (
                compilation,
                requestHandlers.ToImmutable(),
                notificationHandlers.ToImmutable(),
                requestBehaviors.ToImmutable(),
                voidBehaviors.ToImmutable(),
                notificationBehaviors.ToImmutable());
        });
    }

    private static void ClassifyHandlersAndBehaviors(
        ImmutableArray<INamedTypeSymbol> types,
        ImmutableArray<ISymbol>.Builder requestHandlers,
        ImmutableArray<ISymbol>.Builder notificationHandlers,
        ImmutableArray<ISymbol>.Builder requestBehaviors,
        ImmutableArray<ISymbol>.Builder voidBehaviors,
        ImmutableArray<ISymbol>.Builder notificationBehaviors)
    {
        foreach (INamedTypeSymbol type in types)
        {
            ClassifySingleType(type, requestHandlers,
                notificationHandlers, requestBehaviors, voidBehaviors, notificationBehaviors);
        }
    }

    private static void ClassifySingleType(
        INamedTypeSymbol type,
        ImmutableArray<ISymbol>.Builder requestHandlers,
        ImmutableArray<ISymbol>.Builder notificationHandlers,
        ImmutableArray<ISymbol>.Builder requestBehaviors,
        ImmutableArray<ISymbol>.Builder voidBehaviors,
        ImmutableArray<ISymbol>.Builder notificationBehaviors)
    {
        foreach (INamedTypeSymbol iface in type.AllInterfaces)
        {
            string ns = iface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (iface.Name == "IRequestHandler" && ns == "global::Dualis.CQRS")
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

    private static IEnumerable<INamedTypeSymbol> EnumerateAllTypes(IAssemblySymbol assembly)
    {
        Stack<INamespaceOrTypeSymbol> stack = new();
        stack.Push(assembly.GlobalNamespace);

        while (stack.Count > 0)
        {
            INamespaceOrTypeSymbol current = stack.Pop();

            if (current is INamespaceSymbol ns)
            {
                foreach (ISymbol member in ns.GetMembers())
                {
                    if (member is INamespaceOrTypeSymbol nt)
                    {
                        stack.Push(nt);
                    }
                }
            }
            else if (current is INamedTypeSymbol type)
            {
                yield return type;

                foreach (INamedTypeSymbol nested in type.GetTypeMembers())
                {
                    stack.Push(nested);
                }
            }
        }
    }

    private static bool IsEffectivelyPublic(INamedTypeSymbol type)
    {
        // Public type and all containing types public.
        if (type.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        INamedTypeSymbol? cur = type.ContainingType;
        while (cur is not null)
        {
            if (cur.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            cur = cur.ContainingType;
        }

        return true;
    }
}
