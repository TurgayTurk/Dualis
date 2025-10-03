using System.Collections.Immutable;
using System.Text;
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

            StringBuilder sb = new();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("#pragma warning disable CS1591, CA1812, CA1822, IDE0051, IDE0060");
            sb.AppendLine("using System;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            sb.AppendLine("using Microsoft.Extensions.Options;");
            sb.AppendLine("using Dualis;");
            sb.AppendLine("using Dualis.CQRS;");
            sb.AppendLine("using Dualis.Pipeline;");
            sb.AppendLine("using Dualis.Notifications;");
            sb.AppendLine();
            // Place generated method under a non-imported namespace to avoid extension method ambiguity with the public entry point.
            sb.AppendLine("namespace Dualis.Generated;");
            sb.AppendLine();
            // Internal so it is not exported across assemblies; different namespace to avoid being in scope by default.
            sb.AppendLine("internal static class ServiceCollectionExtensions");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Registers Dualis core services, discovered handlers and pipeline behaviors.");
            sb.AppendLine("    /// Honors flags configured through <c>DualizorOptions</c>.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"services\">The DI container.</param>");
            sb.AppendLine("    /// <param name=\"configure\">Optional configuration callback for <c>DualizorOptions</c>.</param>");
            sb.AppendLine("    /// <returns>The same <see cref=\"IServiceCollection\"/> for chaining.</returns>");
            // IMPORTANT: non-extension (no 'this') and internal
            sb.AppendLine("    internal static IServiceCollection AddDualis(Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<DualizorOptions>? configure = null)");
            sb.AppendLine("    {");
            sb.AppendLine("        // Options");
            sb.AppendLine("        services.AddOptions<DualizorOptions>();");
            sb.AppendLine("        if (configure is not null) services.Configure(configure);");
            sb.AppendLine();
            sb.AppendLine("        // Idempotency guard: only register service graph once; allow multiple configure delegates");
            sb.AppendLine("        bool firstRun = !System.Linq.Enumerable.Any(services, sd => sd.ServiceType == typeof(AddDualisMarker));");
            sb.AppendLine("        services.TryAddSingleton<AddDualisMarker>();");
            sb.AppendLine("        if (!firstRun) return services;");
            sb.AppendLine();
            sb.AppendLine("        // Eagerly apply manual registries and compute auto-registration flags");
            sb.AppendLine("        bool autoRegisterBehaviors = true;");
            sb.AppendLine("        bool autoRegisterRequestHandlers = true;");
            sb.AppendLine("        bool autoRegisterNotificationHandlers = true;");
            sb.AppendLine("        if (configure is not null)");
            sb.AppendLine("        {");
            sb.AppendLine("            DualizorOptions tmp = new DualizorOptions();");
            sb.AppendLine("            configure(tmp);");
            sb.AppendLine("            tmp.Pipelines.Apply(services);");
            sb.AppendLine("            tmp.CQRS.Apply(services);");
            sb.AppendLine("            tmp.Notifications.Apply(services);");
            sb.AppendLine("            autoRegisterBehaviors = tmp.RegisterDiscoveredBehaviors && tmp.Pipelines.AutoRegisterEnabled;");
            sb.AppendLine("            autoRegisterRequestHandlers = tmp.RegisterDiscoveredCqrsHandlers;");
            sb.AppendLine("            autoRegisterNotificationHandlers = tmp.RegisterDiscoveredNotificationHandlers;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Auto-register discovered pipeline behaviors if enabled");
            sb.AppendLine("        if (autoRegisterBehaviors)");
            sb.AppendLine("        {");
            // Request/Response
            sb.AppendLine("            // Request/Response behaviors");
            AppendRequestBehaviorRegistrations(sb, requestBehaviors);
            // Void
            sb.AppendLine("            // Void request behaviors");
            AppendVoidBehaviorRegistrations(sb, voidBehaviors);
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Generated mediator (single instance per scope) - if available");
            sb.AppendLine("        Type? dualizorType = Type.GetType(\"Dualis.Dualizor\") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(\"Dualis.Dualizor\", throwOnError: false)).FirstOrDefault(t => t is not null);");
            sb.AppendLine("        if (dualizorType is not null)");
            sb.AppendLine("        {");
            sb.AppendLine("            services.TryAdd(new ServiceDescriptor(dualizorType, dualizorType, ServiceLifetime.Scoped));");
            sb.AppendLine("            services.TryAdd(ServiceDescriptor.Scoped(typeof(IDualizor), sp => (IDualizor)sp.GetRequiredService(dualizorType)));");
            sb.AppendLine("            services.TryAdd(ServiceDescriptor.Scoped(typeof(ISender), sp => (ISender)sp.GetRequiredService(dualizorType)));");
            sb.AppendLine("            services.TryAdd(ServiceDescriptor.Scoped(typeof(IPublisher), sp => (IPublisher)sp.GetRequiredService(dualizorType)));");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Auto-register discovered handlers based on flags");
            sb.AppendLine("        if (autoRegisterRequestHandlers)");
            sb.AppendLine("        {");
            sb.AppendLine("            // IRequestHandler registrations");
            AppendRequestHandlerRegistrations(sb, requestHandlers);
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        if (autoRegisterNotificationHandlers)");
            sb.AppendLine("        {");
            sb.AppendLine("            // INotificationHandler registrations");
            AppendNotificationRegistrations(sb, notificationHandlers);
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // Infra (publisher + context)");
            sb.AppendLine("        services.TryAdd(ServiceDescriptor.Scoped<INotificationPublisher>(sp =>");
            sb.AppendLine("        {");
            sb.AppendLine("            DualizorOptions options = sp.GetRequiredService<IOptions<DualizorOptions>>().Value;");
            sb.AppendLine("            return options.NotificationPublisherFactory(sp);");
            sb.AppendLine("        }));");
            sb.AppendLine("        services.TryAdd(ServiceDescriptor.Scoped(sp =>");
            sb.AppendLine("        {");
            sb.AppendLine("            DualizorOptions options = sp.GetRequiredService<IOptions<DualizorOptions>>().Value;");
            sb.AppendLine("            return new NotificationPublishContext(options.NotificationFailureBehavior, options.MaxPublishDegreeOfParallelism);");
            sb.AppendLine("        }));");
            sb.AppendLine("        services.TryAddScoped<SequentialNotificationPublisher>();");
            sb.AppendLine("        services.TryAddScoped<ParallelWhenAllNotificationPublisher>();");
            sb.AppendLine();
            sb.AppendLine("        return services;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    internal sealed class AddDualisMarker { }");
            sb.AppendLine("}");
            sb.AppendLine("#pragma warning restore CS1591, CA1812, CA1822, IDE0051, IDE0060");

            spc.AddSource("ServiceCollectionExtensions.g.cs", sb.ToString());
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

    private static void AppendRequestBehaviorRegistrations(StringBuilder sb, ImmutableArray<ISymbol> behaviorSymbols)
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
                    sb.AppendLine($"            services.TryAddEnumerable(ServiceDescriptor.Scoped({service}, {impl}));");
                }
            }
        }
    }

    private static void AppendVoidBehaviorRegistrations(StringBuilder sb, ImmutableArray<ISymbol> behaviorSymbols)
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
                    sb.AppendLine($"            services.TryAddEnumerable(ServiceDescriptor.Scoped({service}, {impl}));");
                }
            }
        }
    }

    private static void AppendRequestHandlerRegistrations(StringBuilder sb, ImmutableArray<ISymbol> requestHandlers)
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
                        sb.AppendLine($"        services.TryAddScoped<{ifaceFull}<{requestType}, {responseType}>, {handlerName}>();");
                    }
                }
                else
                {
                    string requestType = TypeArgs[0].ToDisplayString(FullyQualifiedWithNullability);
                    string key = $"R1|{ifaceFull}|{requestType}|{handlerName}";
                    if (emitted.Add(key))
                    {
                        sb.AppendLine($"        services.TryAddScoped<{ifaceFull}<{requestType}>, {handlerName}>();");
                    }
                }
            }
        }
    }

    private static void AppendNotificationRegistrations(StringBuilder sb, ImmutableArray<ISymbol> notificationHandlers)
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
                    sb.AppendLine($"        services.TryAddScoped<INotificationHandler<{notificationType}>, {handlerName}>();");
                }
            }
        }
    }
}
