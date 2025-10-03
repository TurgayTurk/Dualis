using System.Collections.Immutable;
using System.Text;
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
    /// Configures the incremental generator pipeline: reads gating, discovers handlers/behaviors, and emits the Dualizor implementation.
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

            StringBuilder sb = new();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("#pragma warning disable CS1591, CA1812, CA1822, IDE0051, IDE0060, CS9113, CS1998");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Collections.Concurrent;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Dualis;");
            sb.AppendLine("using Dualis.CQRS;");
            sb.AppendLine("using Dualis.Pipeline;");
            sb.AppendLine("using Dualis.Notifications;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine();
            sb.AppendLine("namespace Dualis;");
            sb.AppendLine();
            sb.AppendLine("public sealed class Dualizor(IServiceProvider serviceProvider, INotificationPublisher publisher, NotificationPublishContext publishContext) : IDualizor, ISender, IPublisher");
            sb.AppendLine("{");
            sb.AppendLine("    private readonly ConcurrentDictionary<Type, object> behaviorCache = new();");
            sb.AppendLine();
            sb.AppendLine("    private static T[] ToArrayCached<T>(IEnumerable<T> source) => source is T[] arr ? arr : System.Linq.Enumerable.ToArray(source);");
            sb.AppendLine();

            // IRequest<TResponse>
            sb.AppendLine("    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        switch (request)");
            sb.AppendLine("        {");
            foreach (INamedTypeSymbol handler in source.RequestHandlers.OfType<INamedTypeSymbol>())
            {
                foreach (INamedTypeSymbol iface in handler.AllInterfaces)
                {
                    if (iface.Name == "IRequestHandler" && iface.TypeArguments.Length == 2)
                    {
                        // Skip open generic shapes
                        bool hasTp = iface.TypeArguments.Any(static t => t is ITypeParameterSymbol);
                        if (hasTp)
                        {
                            continue;
                        }
                        string req = iface.TypeArguments[0].ToDisplayString(FullyQualifiedWithNullability);
                        string res = iface.TypeArguments[1].ToDisplayString(FullyQualifiedWithNullability);
                        sb.AppendLine($"            case {req} r:");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                IRequestHandler<{req}, {res}> h = serviceProvider.GetRequiredService<IRequestHandler<{req}, {res}>>();");
                        sb.AppendLine($"                Type spBehType = typeof(IPipelineBehavior<{req}, {res}>);");
                        sb.AppendLine($"                IPipelineBehavior<{req}, {res}>[] behaviors;");
                        sb.AppendLine($"                if (!behaviorCache.TryGetValue(spBehType, out object? bCached)) behaviors = ToArrayCached(serviceProvider.GetServices<IPipelineBehavior<{req}, {res}>>()); else behaviors = (IPipelineBehavior<{req}, {res}>[])bCached;");
                        sb.AppendLine($"                Type unBehType = typeof(IPipelineBehaviour<{req}, {res}>);");
                        sb.AppendLine($"                IPipelineBehaviour<{req}, {res}>[] unified;");
                        sb.AppendLine($"                if (!behaviorCache.TryGetValue(unBehType, out object? ubCached)) unified = ToArrayCached(serviceProvider.GetServices<IPipelineBehaviour<{req}, {res}>>()); else unified = (IPipelineBehaviour<{req}, {res}>[])ubCached;");
                        sb.AppendLine("                if (behaviors.Length == 0 && unified.Length == 0)");
                        sb.AppendLine("                {");
                        sb.AppendLine("                    var r0 = await h.Handle(r, cancellationToken);");
                        sb.AppendLine("                    return (TResponse)(object)r0!;");
                        sb.AppendLine("                }");
                        sb.AppendLine($"                RequestHandlerDelegate<{res}> next = ct => h.Handle(r, ct);");
                        sb.AppendLine("                for (int i = behaviors.Length - 1; i >= 0; i--) { var b = behaviors[i]; var current = next; next = ct => b.Handle(r, current, ct); }");
                        sb.AppendLine("                for (int i = unified.Length - 1; i >= 0; i--) { var b = unified[i]; var current = next; next = ct => b.Handle(r, current, ct); }");
                        sb.AppendLine("                var result = await next(cancellationToken);");
                        sb.AppendLine("                return (TResponse)(object)result!;");
                        sb.AppendLine("            }");
                    }
                }
            }
            sb.AppendLine("            default:");
            sb.AppendLine("                throw new InvalidOperationException($\"Unknown request type: {request.GetType().FullName}\");");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // IRequest (void)
            sb.AppendLine("    public async Task Send(IRequest request, CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        switch (request)");
            sb.AppendLine("        {");
            foreach (INamedTypeSymbol handler in source.RequestHandlers.OfType<INamedTypeSymbol>())
            {
                foreach (INamedTypeSymbol iface in handler.AllInterfaces)
                {
                    if (iface.Name == "IRequestHandler" && iface.TypeArguments.Length == 1)
                    {
                        // Skip open generic shapes
                        bool hasTp = iface.TypeArguments.Any(static t => t is ITypeParameterSymbol);
                        if (hasTp)
                        {
                            continue;
                        }
                        string req = iface.TypeArguments[0].ToDisplayString(FullyQualifiedWithNullability);
                        sb.AppendLine($"            case {req} r:");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                IRequestHandler<{req}> h = serviceProvider.GetRequiredService<IRequestHandler<{req}>>();");
                        sb.AppendLine($"                Type spBehType = typeof(IPipelineBehavior<{req}>);");
                        sb.AppendLine($"                IPipelineBehavior<{req}>[] behaviors;");
                        sb.AppendLine($"                if (!behaviorCache.TryGetValue(spBehType, out object? bCached)) behaviors = ToArrayCached(serviceProvider.GetServices<IPipelineBehavior<{req}>>()); else behaviors = (IPipelineBehavior<{req}>[])bCached;");
                        sb.AppendLine($"                Type unBehType = typeof(IPipelineBehaviour<{req}, Unit>);");
                        sb.AppendLine($"                IPipelineBehaviour<{req}, Unit>[] unified;");
                        sb.AppendLine($"                if (!behaviorCache.TryGetValue(unBehType, out object? ubCached)) unified = ToArrayCached(serviceProvider.GetServices<IPipelineBehaviour<{req}, Unit>>()); else unified = (IPipelineBehaviour<{req}, Unit>[])ubCached;");
                        sb.AppendLine("                if (behaviors.Length == 0 && unified.Length == 0)");
                        sb.AppendLine("                {");
                        sb.AppendLine("                    await h.Handle(r, cancellationToken);");
                        sb.AppendLine("                    return;");
                        sb.AppendLine("                }");
                        sb.AppendLine($"                RequestHandlerDelegate next = ct => h.Handle(r, ct);");
                        sb.AppendLine("                for (int i = behaviors.Length - 1; i >= 0; i--) { var b = behaviors[i]; var current = next; next = ct => b.Handle(r, current, ct); }");
                        sb.AppendLine("                for (int i = unified.Length - 1; i >= 0; i--) { var b = unified[i]; RequestHandlerDelegate<Unit> current = ct => { next(ct); return Task.FromResult(Unit.Value); }; RequestHandlerDelegate<Unit> wrapped = ct => b.Handle(r, current, ct); next = async ct => { await wrapped(ct); }; }");
                        sb.AppendLine("                await next(cancellationToken);");
                        sb.AppendLine("                return;");
                        sb.AppendLine("            }");
                    }
                }
            }
            sb.AppendLine("            default:");
            sb.AppendLine("                throw new InvalidOperationException($\"Unknown request type: {request.GetType().FullName}\");");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Publish
            sb.AppendLine("    public async Task Publish(INotification notification, CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        _ = publisher; _ = publishContext;");
            if (source.NotificationHandlers.Length > 0)
            {
                sb.AppendLine("        switch (notification)");
                sb.AppendLine("        {");

                // Emit a single case per unique notification type to avoid duplicate/unreachable cases
                HashSet<string> emittedNotes = [];
                foreach (INamedTypeSymbol handler in source.NotificationHandlers.OfType<INamedTypeSymbol>())
                {
                    foreach (INamedTypeSymbol iface in handler.AllInterfaces)
                    {
                        if (iface.Name == "INotificationHandler" && iface.TypeArguments.Length == 1)
                        {
                            // Skip open generic shapes
                            bool hasTp = iface.TypeArguments.Any(static t => t is ITypeParameterSymbol);
                            if (hasTp)
                            {
                                continue;
                            }
                            string note = iface.TypeArguments[0].ToDisplayString(FullyQualifiedWithNullability);
                            if (!emittedNotes.Add(note))
                            {
                                continue; // already generated a case for this notification type
                            }
                            sb.AppendLine($"            case {note} n:");
                            sb.AppendLine("            {");
                            sb.AppendLine($"                IEnumerable<INotificationHandler<{note}>> handlers = serviceProvider.GetServices<INotificationHandler<{note}>>();");
                            sb.AppendLine($"                Type unBehType = typeof(IPipelineBehaviour<{note}, Unit>);");
                            sb.AppendLine($"                IPipelineBehaviour<{note}, Unit>[] unified;");
                            sb.AppendLine($"                if (!behaviorCache.TryGetValue(unBehType, out object? ubCached)) unified = ToArrayCached(serviceProvider.GetServices<IPipelineBehaviour<{note}, Unit>>()); else unified = (IPipelineBehaviour<{note}, Unit>[])ubCached;");
                            sb.AppendLine("                if (unified.Length == 0)");
                            sb.AppendLine("                {");
                            sb.AppendLine("                    await publisher.Publish(n, handlers, publishContext, cancellationToken);");
                            sb.AppendLine("                    return;");
                            sb.AppendLine("                }");
                            sb.AppendLine("                NotificationPublishDelegate next = ct => publisher.Publish(n, handlers, publishContext, ct);");
                            sb.AppendLine("                for (int i = unified.Length - 1; i >= 0; i--)");
                            sb.AppendLine("                { var b = unified[i]; RequestHandlerDelegate<Unit> current = ct => { var t = next(ct); return t.ContinueWith(_ => Unit.Value, ct); }; RequestHandlerDelegate<Unit> wrapped = ct => b.Handle(n, current, ct); next = async ct => { await wrapped(ct); }; }");
                            sb.AppendLine("                await next(cancellationToken);");
                            sb.AppendLine("                return;");
                            sb.AppendLine("            }");
                        }
                    }
                }
                sb.AppendLine("            default:");
                sb.AppendLine("                // Unknown notification type: no handlers discovered; no-op to avoid unexpected exceptions.");
                sb.AppendLine("                return;");
                sb.AppendLine("        }");
            }
            else
            {
                sb.AppendLine("        await Task.CompletedTask;");
            }
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("}");
            sb.AppendLine("#pragma warning restore CS1591, CA1812, CA1822, IDE0051, IDE0060, CS9113, CS1998");

            spc.AddSource("Dualizor.g.cs", sb.ToString());
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
