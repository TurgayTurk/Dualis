using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Dualis.SourceGen;

/// <summary>
/// Roslyn incremental generator that emits the default Dualizor mediator implementation for IRequest/IRequestHandler and notifications.
/// </summary>
[Generator]
public sealed class DualisGenerator : IIncrementalGenerator
{
    private static readonly string[] NewlineSeparators = ["\r\n", "\n", "\r"]; // CA1861

    // Preserve full qualification and nullability (e.g., T?) in generated type names
    private static readonly SymbolDisplayFormat FullyQualifiedWithNullability = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>
    /// Configures the incremental generator pipeline, including gating via properties/attributes,
    /// discovery of handlers/behaviors, and emission of the generated Dualizor implementation.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<(bool exists, bool isTrue)> prop = context.AnalyzerConfigOptionsProvider
            .Select(static (opts, _) =>
            {
                if (opts.GlobalOptions.TryGetValue("build_property.DualisEnableGenerator", out string? v))
                {
                    return (true, string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
                }
                return (false, false);
            });

        IncrementalValueProvider<(bool exists, bool isTrue)> propFromTexts = context.AdditionalTextsProvider
            .Where(static t => t.Path.EndsWith(".globalconfig", StringComparison.OrdinalIgnoreCase) || t.Path.EndsWith(".editorconfig", StringComparison.OrdinalIgnoreCase))
            .Select(static (t, ct) => t.GetText(ct)?.ToString() ?? string.Empty)
            .Collect()
            .Select(static (texts, _) =>
            {
                const string key = "build_property.DualisEnableGenerator";
                string? content = texts.FirstOrDefault(c => c.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
                if (content is null)
                {
                    return (false, false);
                }

                string? line = content.Split(NewlineSeparators, StringSplitOptions.None)
                    .FirstOrDefault(l => l.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
                if (line is null)
                {
                    return (false, false);
                }

                int eq = line.IndexOf('=');
                if (eq < 0)
                {
                    return (false, false);
                }

                string rhs = line.Substring(eq + 1).Trim();
                bool val = string.Equals(rhs, "true", StringComparison.OrdinalIgnoreCase);
                return (true, val);
            });

        IncrementalValueProvider<bool> enabledByAttr = context.CompilationProvider
            .Select(static (comp, _) => HasEnableAttribute(comp));

        IncrementalValueProvider<bool> enabled = prop.Combine(propFromTexts).Combine(enabledByAttr)
            .Select(static (triple, _) =>
            {
                (bool exists, bool isTrue) = triple.Left.Left;
                (bool exists, bool isTrue) p2 = triple.Left.Right;
                bool byAttr = triple.Right;
                bool byProp = exists && isTrue || p2.exists && p2.isTrue;
                return byProp || byAttr;
            });

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<ISymbol> RequestHandlers, ImmutableArray<ISymbol> NotificationHandlers, ImmutableArray<ISymbol> RequestBehaviors, ImmutableArray<ISymbol> VoidBehaviors, ImmutableArray<ISymbol> NotificationBehaviors)> handlers = SharedHandlerDiscovery.DiscoverHandlers(context);

        context.RegisterSourceOutput(handlers.Combine(enabled), static (spc, tuple) =>
        {
            ((Compilation Compilation,
              ImmutableArray<ISymbol> RequestHandlers,
              ImmutableArray<ISymbol> NotificationHandlers,
              ImmutableArray<ISymbol> RequestBehaviors,
              ImmutableArray<ISymbol> VoidBehaviors,
              ImmutableArray<ISymbol> NotificationBehaviors) source, bool isEnabled) = tuple;
            if (!isEnabled)
            {
                return;
            }

            if (source.Compilation.GetTypeByMetadataName("Dualis.Dualizor") is not null)
            {
                return;
            }

            // Require IRequest/IRequestHandler presence
            if (source.Compilation.GetTypeByMetadataName("Dualis.CQRS.IRequest") is null ||
                source.Compilation.GetTypeByMetadataName("Dualis.CQRS.IRequestHandler`2") is null)
            {
                return;
            }

            int estimatedCapacity = 2048
                                    + source.RequestHandlers.Length * 220
                                    + source.NotificationHandlers.Length * 160;

            CodeWriter w = new(estimatedCapacity);
            w.WriteLine("// <auto-generated />");
            w.WriteLine("#nullable enable");
            w.WriteLine("#pragma warning disable CS1591, CA1812, CA1822, IDE0051, IDE0060, CS9113, CS1998");
            w.WriteLine("using System;");
            w.WriteLine("using System.Collections.Generic;");
            w.WriteLine("using System.Collections.Concurrent;");
            w.WriteLine("using System.Threading;");
            w.WriteLine("using System.Threading.Tasks;");
            w.WriteLine("using Dualis;");
            w.WriteLine("using Dualis.CQRS;");
            w.WriteLine("using Dualis.Pipeline;");
            w.WriteLine("using Dualis.Notifications;");
            w.WriteLine("using Microsoft.Extensions.DependencyInjection;");
            w.WriteLine();
            w.WriteLine("namespace Dualis;");
            w.WriteLine();
            w.WriteLine("public sealed class Dualizor(IServiceProvider serviceProvider, INotificationPublisher publisher, NotificationPublishContext publishContext) : IDualizor, ISender, IPublisher");
            w.OpenBlock();
            w.WriteLine("private readonly ConcurrentDictionary<Type, object> behaviorCache = new();");
            w.WriteLine();
            w.WriteLine("private static T[] ToArrayCached<T>(IEnumerable<T> source) => source is T[] arr ? arr : System.Linq.Enumerable.ToArray(source);");
            w.WriteLine();

            // IRequest<TResponse>
            w.WriteLine("public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)");
            w.OpenBlock();
            w.WriteLine("switch (request)");
            w.OpenBlock();
            foreach (INamedTypeSymbol handler in source.RequestHandlers.OfType<INamedTypeSymbol>())
            {
                foreach (INamedTypeSymbol iface in handler.AllInterfaces)
                {
                    if (iface.Name == "IRequestHandler" && iface.TypeArguments.Length == 2)
                    {
                        bool hasTp = iface.TypeArguments.Any(static t => t is ITypeParameterSymbol);
                        if (hasTp)
                        {
                            continue;
                        }
                        string req = iface.TypeArguments[0].ToDisplayString(FullyQualifiedWithNullability);
                        string res = iface.TypeArguments[1].ToDisplayString(FullyQualifiedWithNullability);
                        w.WriteLine($"case {req} r:");
                        w.OpenBlock();
                        w.WriteLine($"IRequestHandler<{req}, {res}> h = serviceProvider.GetRequiredService<IRequestHandler<{req}, {res}>>();");
                        w.WriteLine($"Type spBehType = typeof(IPipelineBehavior<{req}, {res}>);");
                        w.WriteLine($"IPipelineBehavior<{req}, {res}>[] behaviors;");
                        w.WriteLine($"if (!behaviorCache.TryGetValue(spBehType, out object? bCached)) {{ behaviors = ToArrayCached(serviceProvider.GetServices<IPipelineBehavior<{req}, {res}>>()); behaviorCache.TryAdd(spBehType, behaviors); }} else behaviors = (IPipelineBehavior<{req}, {res}>[])bCached;");
                        w.WriteLine($"Type unBehType = typeof(IPipelineBehaviour<{req}, {res}>);");
                        w.WriteLine($"IPipelineBehaviour<{req}, {res}>[] unified;");
                        w.WriteLine($"if (!behaviorCache.TryGetValue(unBehType, out object? ubCached)) {{ unified = ToArrayCached(serviceProvider.GetServices<IPipelineBehaviour<{req}, {res}>>()); behaviorCache.TryAdd(unBehType, unified); }} else unified = (IPipelineBehaviour<{req}, {res}>[])ubCached;");
                        w.WriteLine("if (behaviors.Length == 0 && unified.Length == 0)");
                        w.OpenBlock();
                        w.WriteLine("var r0 = await h.Handle(r, cancellationToken);");
                        w.WriteLine("return (TResponse)(object)r0!;");
                        w.CloseBlock();
                        w.WriteLine($"RequestHandlerDelegate<{res}> next = ct => h.Handle(r, ct);");
                        w.WriteLine("for (int i = behaviors.Length - 1; i >= 0; i--) { var b = behaviors[i]; var current = next; next = ct => b.Handle(r, current, ct); }");
                        w.WriteLine("for (int i = unified.Length - 1; i >= 0; i--) { var b = unified[i]; var current = next; next = ct => b.Handle(r, current, ct); }");
                        w.WriteLine("var result = await next(cancellationToken);");
                        w.WriteLine("return (TResponse)(object)result!;");
                        w.CloseBlock();
                    }
                }
            }
            w.WriteLine("default:");
            w.WriteLine("throw new InvalidOperationException($\"Unknown request type: {request.GetType().FullName}\");");
            w.CloseBlock();
            w.CloseBlock();
            w.WriteLine();

            // IRequest (void)
            w.WriteLine("public async Task Send(IRequest request, CancellationToken cancellationToken = default)");
            w.OpenBlock();
            w.WriteLine("switch (request)");
            w.OpenBlock();
            foreach (INamedTypeSymbol handler in source.RequestHandlers.OfType<INamedTypeSymbol>())
            {
                foreach (INamedTypeSymbol iface in handler.AllInterfaces)
                {
                    if (iface.Name == "IRequestHandler" && iface.TypeArguments.Length == 1)
                    {
                        bool hasTp = iface.TypeArguments.Any(static t => t is ITypeParameterSymbol);
                        if (hasTp)
                        {
                            continue;
                        }
                        string req = iface.TypeArguments[0].ToDisplayString(FullyQualifiedWithNullability);
                        w.WriteLine($"case {req} r:");
                        w.OpenBlock();
                        w.WriteLine($"IRequestHandler<{req}> h = serviceProvider.GetRequiredService<IRequestHandler<{req}>>();");
                        w.WriteLine($"Type spBehType = typeof(IPipelineBehavior<{req}>);");
                        w.WriteLine($"IPipelineBehavior<{req}>[] behaviors;");
                        w.WriteLine($"if (!behaviorCache.TryGetValue(spBehType, out object? bCached)) {{ behaviors = ToArrayCached(serviceProvider.GetServices<IPipelineBehavior<{req}>>()); behaviorCache.TryAdd(spBehType, behaviors); }} else behaviors = (IPipelineBehavior<{req}>[])bCached;");
                        w.WriteLine($"Type unBehType = typeof(IPipelineBehaviour<{req}, Unit>);");
                        w.WriteLine($"IPipelineBehaviour<{req}, Unit>[] unified;");
                        w.WriteLine($"if (!behaviorCache.TryGetValue(unBehType, out object? ubCached)) {{ unified = ToArrayCached(serviceProvider.GetServices<IPipelineBehaviour<{req}, Unit>>()); behaviorCache.TryAdd(unBehType, unified); }} else unified = (IPipelineBehaviour<{req}, Unit>[])ubCached;");
                        w.WriteLine("if (behaviors.Length == 0 && unified.Length == 0)");
                        w.OpenBlock();
                        w.WriteLine("await h.Handle(r, cancellationToken);");
                        w.WriteLine("return;");
                        w.CloseBlock();
                        w.WriteLine($"RequestHandlerDelegate next = ct => h.Handle(r, ct);");
                        w.WriteLine("for (int i = behaviors.Length - 1; i >= 0; i--) { var b = behaviors[i]; var current = next; next = ct => b.Handle(r, current, ct); }");
                        w.WriteLine("for (int i = unified.Length - 1; i >= 0; i--) { var b = unified[i]; RequestHandlerDelegate<Unit> current = async ct => { await next(ct); return Unit.Value; }; RequestHandlerDelegate<Unit> wrapped = ct => b.Handle(r, current, ct); next = async ct => { await wrapped(ct); }; }");
                        w.WriteLine("await next(cancellationToken);");
                        w.WriteLine("return;");
                        w.CloseBlock();
                    }
                }
            }
            w.WriteLine("default:");
            w.WriteLine("throw new InvalidOperationException($\"Unknown request type: {request.GetType().FullName}\");");
            w.CloseBlock();
            w.CloseBlock();
            w.WriteLine();

            // Publish
            w.WriteLine("public async Task Publish(INotification notification, CancellationToken cancellationToken = default)");
            w.OpenBlock();
            w.WriteLine("_ = publisher; _ = publishContext;");
            if (source.NotificationHandlers.Length > 0)
            {
                w.WriteLine("switch (notification)");
                w.OpenBlock();
                HashSet<string> emittedNotes = [];
                foreach (INamedTypeSymbol handler in source.NotificationHandlers.OfType<INamedTypeSymbol>())
                {
                    foreach (INamedTypeSymbol iface in handler.AllInterfaces)
                    {
                        if (iface.Name == "INotificationHandler" && iface.TypeArguments.Length == 1)
                        {
                            bool hasTp = iface.TypeArguments.Any(static t => t is ITypeParameterSymbol);
                            if (hasTp)
                            {
                                continue;
                            }
                            string note = iface.TypeArguments[0].ToDisplayString(FullyQualifiedWithNullability);
                            if (!emittedNotes.Add(note))
                            {
                                continue;
                            }
                            w.WriteLine($"case {note} n:");
                            w.OpenBlock();
                            w.WriteLine($"IEnumerable<INotificationHandler<{note}>> handlers = serviceProvider.GetServices<INotificationHandler<{note}>>();");
                            w.WriteLine($"Type unBehType = typeof(IPipelineBehaviour<{note}, Unit>);");
                            w.WriteLine($"IPipelineBehaviour<{note}, Unit>[] unified;");
                            w.WriteLine($"if (!behaviorCache.TryGetValue(unBehType, out object? ubCached)) {{ unified = ToArrayCached(serviceProvider.GetServices<IPipelineBehaviour<{note}, Unit>>()); behaviorCache.TryAdd(unBehType, unified); }} else unified = (IPipelineBehaviour<{note}, Unit>[])ubCached;");
                            w.WriteLine("if (unified.Length == 0)");
                            w.OpenBlock();
                            w.WriteLine("await publisher.Publish(n, handlers, publishContext, cancellationToken);");
                            w.WriteLine("return;");
                            w.CloseBlock();
                            w.WriteLine("NotificationPublishDelegate next = ct => publisher.Publish(n, handlers, publishContext, ct);");
                            w.WriteLine("for (int i = unified.Length - 1; i >= 0; i--)");
                            w.OpenBlock();
                            w.WriteLine("var b = unified[i]; RequestHandlerDelegate<Unit> current = async ct => { await next(ct); return Unit.Value; }; RequestHandlerDelegate<Unit> wrapped = ct => b.Handle(n, current, ct); next = async ct => { await wrapped(ct); };");
                            w.CloseBlock();
                            w.WriteLine("await next(cancellationToken);");
                            w.WriteLine("return;");
                            w.CloseBlock();
                        }
                    }
                }
                w.WriteLine("default:");
                w.WriteLine("// Unknown notification type: no handlers discovered; no-op to avoid unexpected exceptions.");
                w.WriteLine("return;");
                w.CloseBlock();
            }
            else
            {
                w.WriteLine("await Task.CompletedTask;");
            }
            w.CloseBlock();
            w.WriteLine();

            w.CloseBlock();
            w.WriteLine("#pragma warning restore CS1591, CA1812, CA1822, IDE0051, IDE0060, CS9113, CS1998");

            spc.AddSource("Dualizor.g.cs", w.ToString());
        });
    }

    private static bool HasEnableAttribute(Compilation comp) => comp.Assembly.GetAttributes()
        .Select(a => a.AttributeClass)
        .Any(cls =>
        {
            if (cls is null)
            {
                return false;
            }

            string name = cls.Name;
            string ns = cls.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return (name == "EnableDualisGenerationAttribute" || name == "EnableDualisGeneration") && ns == "global::Dualis";
        });
}
