using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Dualis.SourceGen;

/// <summary>
/// Roslyn incremental generator that emits the <c>AddDualis</c> registration method, which registers handlers,
/// pipeline behaviors, and infrastructure into IServiceCollection.
/// </summary>
[Generator]
public sealed class ServiceCollectionExtensionGenerator : IIncrementalGenerator
{
    private static readonly string[] NewlineSeparators = ["\r\n", "\n", "\r"]; // CA1861

    // Use a display format that preserves full qualification and nullability annotations (e.g., T?).
    private static readonly SymbolDisplayFormat FullyQualifiedWithNullability = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>
    /// Configures the generator pipeline and registers the source output callback.
    /// </summary>
    /// <param name="context">The incremental generator initialization context.</param>
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

                string? line = content
                    .Split(NewlineSeparators, StringSplitOptions.None)
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
            .Select(static (combo, _) =>
            {
                (bool exists, bool isTrue) = combo.Left.Left;
                (bool exists, bool isTrue) fallback = combo.Left.Right;
                bool byAttr = combo.Right;
                bool byProp = exists && isTrue || fallback.exists && fallback.isTrue;
                return byProp || byAttr;
            });

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<ISymbol> RequestHandlers, ImmutableArray<ISymbol> NotificationHandlers, ImmutableArray<ISymbol> RequestBehaviors, ImmutableArray<ISymbol> VoidBehaviors, ImmutableArray<ISymbol> NotificationBehaviors)> items = SharedHandlerDiscovery.DiscoverHandlers(context);

        context.RegisterSourceOutput(items.Combine(enabled), static (spc, tuple) =>
        {
            ((Compilation Compilation,
              ImmutableArray<ISymbol> RequestHandlers,
              ImmutableArray<ISymbol> NotificationHandlers,
              ImmutableArray<ISymbol> RequestBehaviors,
              ImmutableArray<ISymbol> VoidBehaviors,
              ImmutableArray<ISymbol> NotificationBehaviors) source, bool isEnabled) = tuple;
            if (!isEnabled)
            {
                return; // generator disabled in this project
            }

            ImmutableArray<ISymbol> requestHandlers = source.RequestHandlers;
            ImmutableArray<ISymbol> notificationHandlers = source.NotificationHandlers;
            ImmutableArray<ISymbol> requestBehaviors = source.RequestBehaviors;
            ImmutableArray<ISymbol> voidBehaviors = source.VoidBehaviors;

            int estimatedCapacity = 1024
                + requestHandlers.Length * 160
                + notificationHandlers.Length * 120
                + requestBehaviors.Length * 100
                + voidBehaviors.Length * 80;

            CodeWriter w = new(estimatedCapacity);
            w.WriteLine("// <auto-generated />");
            w.WriteLine("#nullable enable");
            w.WriteLine("#pragma warning disable CS1591, CA1812, CA1822, IDE0051, IDE0060");
            w.WriteLine("using System;");
            w.WriteLine("using Microsoft.Extensions.DependencyInjection;");
            w.WriteLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            w.WriteLine("using Microsoft.Extensions.Options;");
            w.WriteLine("using Dualis;");
            w.WriteLine("using Dualis.CQRS;");
            w.WriteLine("using Dualis.Pipeline;");
            w.WriteLine("using Dualis.Notifications;");
            w.WriteLine();
            w.WriteLine("namespace Dualis.Generated;");
            w.WriteLine();
            w.WriteLine("internal static class ServiceCollectionExtensions");
            w.OpenBlock();
            w.WriteLine("/// <summary>");
            w.WriteLine("/// Registers Dualis core services, discovered handlers and pipeline behaviors.");
            w.WriteLine("/// Honors flags configured through <c>DualizorOptions</c>.");
            w.WriteLine("/// </summary>");
            w.WriteLine("/// <param name=\"services\">The DI container.</param>");
            w.WriteLine("/// <param name=\"configure\">Optional configuration callback for <c>DualizorOptions</c>.</param>");
            w.WriteLine("/// <returns>The same <see cref=\"IServiceCollection\"/> for chaining.</returns>");
            w.WriteLine("internal static IServiceCollection AddDualis(Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<DualizorOptions>? configure = null)");
            w.OpenBlock();
            w.WriteLine("// Options");
            w.WriteLine("services.AddOptions<DualizorOptions>();");
            w.WriteLine("if (configure is not null) services.Configure(configure);");
            w.WriteLine();
            w.WriteLine("// Idempotency guard: only register service graph once; allow multiple configure delegates");
            w.WriteLine("bool firstRun = !System.Linq.Enumerable.Any(services, sd => sd.ServiceType == typeof(AddDualisMarker));");
            w.WriteLine("services.TryAddSingleton<AddDualisMarker>();");
            w.WriteLine("if (!firstRun) return services;");
            w.WriteLine();
            w.WriteLine("// Eagerly apply manual registries and compute auto-registration flags");
            w.WriteLine("bool autoRegisterBehaviors = true;");
            w.WriteLine("bool autoRegisterRequestHandlers = true;");
            w.WriteLine("bool autoRegisterNotificationHandlers = true;");
            w.WriteLine("if (configure is not null)");
            w.OpenBlock();
            w.WriteLine("DualizorOptions tmp = new DualizorOptions();");
            w.WriteLine("configure(tmp);");
            w.WriteLine("tmp.Pipelines.Apply(services);");
            w.WriteLine("tmp.CQRS.Apply(services);");
            w.WriteLine("tmp.Notifications.Apply(services);");
            w.WriteLine("autoRegisterBehaviors = tmp.RegisterDiscoveredBehaviors && tmp.Pipelines.AutoRegisterEnabled;");
            w.WriteLine("autoRegisterRequestHandlers = tmp.RegisterDiscoveredCqrsHandlers;");
            w.WriteLine("autoRegisterNotificationHandlers = tmp.RegisterDiscoveredNotificationHandlers;");
            w.CloseBlock();
            w.WriteLine();
            w.WriteLine("// Auto-register discovered pipeline behaviors if enabled");
            w.WriteLine("if (autoRegisterBehaviors)");
            w.OpenBlock();
            w.WriteLine("// Request/Response behaviors");
            AppendRequestBehaviorRegistrations(w, requestBehaviors);
            w.WriteLine("// Void request behaviors");
            AppendVoidBehaviorRegistrations(w, voidBehaviors);
            w.CloseBlock();
            w.WriteLine();
            w.WriteLine("// Generated mediator (single instance per scope) - if available");
            w.WriteLine("Type? dualizorType = Type.GetType(\"Dualis.Dualizor\") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(\"Dualis.Dualizor\", throwOnError: false)).FirstOrDefault(t => t is not null);");
            w.WriteLine("if (dualizorType is not null)");
            w.OpenBlock();
            w.WriteLine("services.TryAdd(new ServiceDescriptor(dualizorType, dualizorType, ServiceLifetime.Scoped));");
            w.WriteLine("services.TryAdd(ServiceDescriptor.Scoped(typeof(IDualizor), sp => (IDualizor)sp.GetRequiredService(dualizorType))); ");
            w.WriteLine("services.TryAdd(ServiceDescriptor.Scoped(typeof(ISender), sp => (ISender)sp.GetRequiredService(dualizorType))); ");
            w.WriteLine("services.TryAdd(ServiceDescriptor.Scoped(typeof(IPublisher), sp => (IPublisher)sp.GetRequiredService(dualizorType))); ");
            w.CloseBlock();
            w.WriteLine();
            w.WriteLine("// Auto-register discovered handlers based on flags");
            w.WriteLine("if (autoRegisterRequestHandlers)");
            w.OpenBlock();
            w.WriteLine("// IRequestHandler registrations");
            AppendRequestHandlerRegistrations(w, requestHandlers);
            w.CloseBlock();
            w.WriteLine();
            w.WriteLine("if (autoRegisterNotificationHandlers)");
            w.OpenBlock();
            w.WriteLine("// INotificationHandler registrations");
            AppendNotificationRegistrations(w, notificationHandlers);
            w.CloseBlock();
            w.WriteLine();
            w.WriteLine("// Infra (publisher + context)");
            w.WriteLine("services.TryAdd(ServiceDescriptor.Scoped<INotificationPublisher>(sp =>");
            w.OpenBlock();
            w.WriteLine("DualizorOptions options = sp.GetRequiredService<IOptions<DualizorOptions>>().Value;");
            w.WriteLine("return options.NotificationPublisherFactory(sp);");
            w.CloseBlock("));");
            w.WriteLine("services.TryAdd(ServiceDescriptor.Scoped(sp =>");
            w.OpenBlock();
            w.WriteLine("DualizorOptions options = sp.GetRequiredService<IOptions<DualizorOptions>>().Value;");
            w.WriteLine("return new NotificationPublishContext(options.NotificationFailureBehavior, options.MaxPublishDegreeOfParallelism);");
            w.CloseBlock("));");
            w.WriteLine("services.TryAddScoped<SequentialNotificationPublisher>();");
            w.WriteLine("services.TryAddScoped<ParallelWhenAllNotificationPublisher>();");
            w.WriteLine();
            w.WriteLine("return services;");
            w.CloseBlock();
            w.WriteLine();
            w.WriteLine("internal sealed class AddDualisMarker { }");
            w.CloseBlock();
            w.WriteLine("#pragma warning restore CS1591, CA1812, CA1822, IDE0051, IDE0060");

            spc.AddSource("ServiceCollectionExtensions.g.cs", w.ToString());
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

    private static bool HasTypeParameters(INamedTypeSymbol iface)
        => iface.TypeArguments.Any(static t => t is ITypeParameterSymbol);

    private static int GetPipelineOrder(INamedTypeSymbol behavior)
    {
        int? min = null;
        foreach (AttributeData attr in behavior.GetAttributes())
        {
            string ns = attr.AttributeClass?.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty;
            string name = attr.AttributeClass?.Name ?? string.Empty;
            if (name == "PipelineOrderAttribute" && ns == "global::Dualis.Pipeline" && attr.ConstructorArguments.Length == 1)
            {
                TypedConstant value = attr.ConstructorArguments[0];
                if (value.Value is int i)
                {
                    min = min is null ? i : Math.Min(min.Value, i);
                }
            }
        }

        return min ?? 0;
    }

    private static void AppendRequestBehaviorRegistrations(CodeWriter w, ImmutableArray<ISymbol> behaviorSymbols)
    {
        HashSet<string> emitted = [];
        foreach (INamedTypeSymbol behavior in behaviorSymbols.OfType<INamedTypeSymbol>()
                     .OrderBy(s => GetPipelineOrder(s))
                     .ThenBy(s => s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
        {
            IEnumerable<INamedTypeSymbol> ifaceMatches = behavior.AllInterfaces
                .Where(i => (i.Name == "IPipelineBehavior" || i.Name == "IPipelineBehaviour")
                            && i.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Dualis.Pipeline"
                            && i.TypeArguments.Length == 2);

            foreach (INamedTypeSymbol iface in ifaceMatches)
            {
                string service;
                string impl;

                if (HasTypeParameters(iface))
                {
                    if (!behavior.IsUnboundGenericType && !behavior.IsGenericType)
                    {
                        continue;
                    }

                    string openService = iface.ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    string openImpl = (behavior.IsUnboundGenericType ? behavior : behavior.ConstructUnboundGenericType()).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    service = $"typeof({openService})";
                    impl = $"typeof({openImpl})";
                }
                else
                {
                    string closedService = iface.ToDisplayString(FullyQualifiedWithNullability);
                    string implType = behavior.ToDisplayString(FullyQualifiedWithNullability);
                    service = $"typeof({closedService})";
                    impl = $"typeof({implType})";
                }

                string key = $"{service}|{impl}";
                if (emitted.Add(key))
                {
                    w.WriteLine($"services.TryAddEnumerable(ServiceDescriptor.Scoped({service}, {impl}));");
                }
            }
        }
    }

    private static void AppendVoidBehaviorRegistrations(CodeWriter w, ImmutableArray<ISymbol> behaviorSymbols)
    {
        HashSet<string> emitted = [];
        foreach (INamedTypeSymbol behavior in behaviorSymbols.OfType<INamedTypeSymbol>()
                     .OrderBy(s => GetPipelineOrder(s))
                     .ThenBy(s => s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
        {
            IEnumerable<INamedTypeSymbol> ifaceMatches = behavior.AllInterfaces
                .Where(i => i.Name == "IPipelineBehavior"
                            && i.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Dualis.Pipeline"
                            && i.TypeArguments.Length == 1);

            foreach (INamedTypeSymbol iface in ifaceMatches)
            {
                string service;
                string impl;

                if (HasTypeParameters(iface))
                {
                    if (!behavior.IsUnboundGenericType && !behavior.IsGenericType)
                    {
                        continue;
                    }

                    string openService = iface.ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    string openImpl = (behavior.IsUnboundGenericType ? behavior : behavior.ConstructUnboundGenericType()).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    service = $"typeof({openService})";
                    impl = $"typeof({openImpl})";
                }
                else
                {
                    string closedService = iface.ToDisplayString(FullyQualifiedWithNullability);
                    string implType = behavior.ToDisplayString(FullyQualifiedWithNullability);
                    service = $"typeof({closedService})";
                    impl = $"typeof({implType})";
                }

                string key = $"{service}|{impl}";
                if (emitted.Add(key))
                {
                    w.WriteLine($"services.TryAddEnumerable(ServiceDescriptor.Scoped({service}, {impl}));");
                }
            }
        }
    }

    private static void AppendRequestHandlerRegistrations(CodeWriter w, ImmutableArray<ISymbol> requestHandlers)
    {
        HashSet<string> emitted = [];
        foreach (INamedTypeSymbol handlerSymbol in requestHandlers.OfType<INamedTypeSymbol>())
        {
            if (handlerSymbol.TypeKind != TypeKind.Class || handlerSymbol.IsAbstract)
            {
                continue;
            }

            IEnumerable<(INamedTypeSymbol Iface, ImmutableArray<ITypeSymbol> TypeArgs)> matches = handlerSymbol.AllInterfaces
                .Where(i => i.Name == "IRequestHandler" && i.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Dualis.CQRS" && (i.TypeArguments.Length == 1 || i.TypeArguments.Length == 2) && !HasTypeParameters(i))
                .Select(i => (i, i.TypeArguments));

            string handlerName = handlerSymbol.ToDisplayString(FullyQualifiedWithNullability);

            foreach ((INamedTypeSymbol Iface, ImmutableArray<ITypeSymbol> TypeArgs) in matches)
            {
                string ifaceNs = Iface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string ifaceFull = ifaceNs + ".IRequestHandler";

                if (TypeArgs.Length == 2)
                {
                    string requestType = TypeArgs[0].ToDisplayString(FullyQualifiedWithNullability);
                    string responseType = TypeArgs[1].ToDisplayString(FullyQualifiedWithNullability);
                    string key = $"R2|{ifaceFull}|{requestType}|{responseType}|{handlerName}";
                    if (emitted.Add(key))
                    {
                        w.WriteLine($"services.TryAddScoped<{ifaceFull}<{requestType}, {responseType}>, {handlerName}>();");
                    }
                }
                else
                {
                    string requestType = TypeArgs[0].ToDisplayString(FullyQualifiedWithNullability);
                    string key = $"R1|{ifaceFull}|{requestType}|{handlerName}";
                    if (emitted.Add(key))
                    {
                        w.WriteLine($"services.TryAddScoped<{ifaceFull}<{requestType}>, {handlerName}>();");
                    }
                }
            }
        }
    }

    private static void AppendNotificationRegistrations(CodeWriter w, ImmutableArray<ISymbol> notificationHandlers)
    {
        HashSet<string> emitted = [];
        foreach (INamedTypeSymbol handlerSymbol in notificationHandlers.OfType<INamedTypeSymbol>())
        {
            if (handlerSymbol.TypeKind != TypeKind.Class || handlerSymbol.IsAbstract)
            {
                continue;
            }

            IEnumerable<ImmutableArray<ITypeSymbol>> interfaceTypeArguments = handlerSymbol.AllInterfaces
                .Where(i =>
                    i.Name == "INotificationHandler" &&
                    i.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Dualis.Notifications" &&
                    i.TypeArguments.Length == 1 &&
                    !HasTypeParameters(i))
                .Select(i => i.TypeArguments);

            string handlerName = handlerSymbol.ToDisplayString(FullyQualifiedWithNullability);

            foreach (ImmutableArray<ITypeSymbol> typeArguments in interfaceTypeArguments)
            {
                string notificationType = typeArguments[0].ToDisplayString(FullyQualifiedWithNullability);
                string key = $"N1|{notificationType}|{handlerName}";
                if (emitted.Add(key))
                {
                    w.WriteLine($"services.TryAddScoped<INotificationHandler<{notificationType}>, {handlerName}>();");
                }
            }
        }
    }
}
