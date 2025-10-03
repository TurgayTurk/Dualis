using System.Reflection;
using Dualis.CQRS;
using Dualis.Notifications;
using Dualis.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dualis;

/// <summary>
/// Runtime DI extensions available even when the source generator is not active.
/// Prefer the generated AddDualis when available; otherwise call AddDualisRuntime.
/// </summary>
public static class RuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers Dualis core services and mediator implementation without requiring the generator.
    /// - Applies options and manual registries.
    /// - Uses the generated Dualis.Dualizor if present; otherwise falls back to a reflection-based mediator.
    /// - Registers notification publisher infrastructure.
    /// - Optionally performs basic runtime discovery of handlers/behaviors when enabled via options flags.
    /// </summary>
    public static IServiceCollection AddDualisRuntime(this IServiceCollection services, Action<DualizorOptions>? configure = null)
    {
        // Options
        services.AddOptions<DualizorOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        // Idempotency marker
        bool firstRun = !services.Any(sd => sd.ServiceType == typeof(AddDualisRuntimeMarker));
        services.TryAddSingleton<AddDualisRuntimeMarker>();
        if (!firstRun)
        {
            return services;
        }

        // Apply manual registries and honor flags
        bool autoRegisterBehaviors = true;
        bool autoRegisterRequestHandlers = true;
        bool autoRegisterNotificationHandlers = true;
        if (configure is not null)
        {
            var tmp = new DualizorOptions();
            configure(tmp);
            tmp.Pipelines.Apply(services);
            tmp.CQRS.Apply(services);
            tmp.Notifications.Apply(services);
            autoRegisterBehaviors = tmp.RegisterDiscoveredBehaviors && tmp.Pipelines.AutoRegisterEnabled;
            autoRegisterRequestHandlers = tmp.RegisterDiscoveredCqrsHandlers;
            autoRegisterNotificationHandlers = tmp.RegisterDiscoveredNotificationHandlers;
        }

        // Prefer generated Dualis if present
        Type? dualizorType = Type.GetType("Dualis.Dualizor")
            ?? AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(a => a.GetType("Dualis.Dualizor", throwOnError: false))
                .FirstOrDefault(t => t is not null);

        if (dualizorType is not null)
        {
            services.TryAdd(new ServiceDescriptor(dualizorType, dualizorType, ServiceLifetime.Scoped));
            services.TryAdd(ServiceDescriptor.Scoped(typeof(IDualizor), sp => (IDualizor)sp.GetRequiredService(dualizorType)));
            services.TryAdd(ServiceDescriptor.Scoped(typeof(ISender), sp => (ISender)sp.GetRequiredService(dualizorType)));
            services.TryAdd(ServiceDescriptor.Scoped(typeof(IPublisher), sp => (IPublisher)sp.GetRequiredService(dualizorType)));
        }
        else
        {
            services.TryAddScoped<IDualizor, FallbackDualizor>();
            services.TryAddScoped<ISender, FallbackDualizor>();
            services.TryAddScoped<IPublisher, FallbackDualizor>();
        }

        // Infra: publisher + context
        services.TryAdd(ServiceDescriptor.Scoped(sp =>
        {
            DualizorOptions options = sp.GetRequiredService<IOptions<DualizorOptions>>().Value;
            return options.NotificationPublisherFactory(sp);
        }));
        services.TryAdd(ServiceDescriptor.Scoped(sp =>
        {
            DualizorOptions options = sp.GetRequiredService<IOptions<DualizorOptions>>().Value;
            return new NotificationPublishContext(options.NotificationFailureBehavior, options.MaxPublishDegreeOfParallelism);
        }));
        services.TryAddScoped<SequentialNotificationPublisher>();
        services.TryAddScoped<ParallelWhenAllNotificationPublisher>();

        // Optional runtime discovery (basic) to mirror generator behavior if enabled
        IReadOnlyList<Assembly> candidates = GetCandidateAssemblies();
        if (autoRegisterRequestHandlers)
        {
            RegisterRequestHandlersRuntime(services, candidates);
        }
        if (autoRegisterNotificationHandlers)
        {
            RegisterNotificationHandlersRuntime(services, candidates);
        }
        if (autoRegisterBehaviors)
        {
            RegisterPipelineBehaviorsRuntime(services, candidates);
        }

        return services;
    }

    private static Assembly[] GetCandidateAssemblies()
    {
        // Favor entry/calling assemblies plus any assembly that references Dualis
        var entry = Assembly.GetEntryAssembly();
        Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();

        var set = new HashSet<Assembly>(new AssemblyComparer());
        if (entry is not null)
        {
            set.Add(entry);
        }
        for (int i = 0; i < loaded.Length; i++)
        {
            Assembly a = loaded[i];
            if (a.IsDynamic)
            {
                continue;
            }
            try
            {
                if (a == typeof(IDualizor).Assembly)
                {
                    set.Add(a);
                    continue;
                }
                AssemblyName[] refs = a.GetReferencedAssemblies();
                for (int r = 0; r < refs.Length; r++)
                {
                    if (string.Equals(refs[r].Name, "Dualis", StringComparison.Ordinal))
                    {
                        set.Add(a);
                        break;
                    }
                }
            }
            catch
            {
                // ignore reflection load issues
            }
        }
        return [.. set];
    }

    private sealed class AssemblyComparer : IEqualityComparer<Assembly>
    {
        public bool Equals(Assembly? x, Assembly? y) => ReferenceEquals(x, y) || x?.FullName == y?.FullName;
        public int GetHashCode(Assembly obj) => obj.FullName?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }

    private static void RegisterRequestHandlersRuntime(IServiceCollection services, IReadOnlyList<Assembly> assemblies)
    {
        for (int ai = 0; ai < assemblies.Count; ai++)
        {
            Assembly a = assemblies[ai];
            Type[] types;
            try
            {
                types = a.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.Where(t => t is not null).Cast<Type>()];
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                if (t is null || !t.IsClass || t.IsAbstract)
                {
                    continue;
                }

                Type[] ifaces = t.GetInterfaces();
                for (int j = 0; j < ifaces.Length; j++)
                {
                    Type iface = ifaces[j];
                    if (!iface.IsGenericType)
                    {
                        continue;
                    }

                    Type def = iface.GetGenericTypeDefinition();
                    if (def == typeof(IRequestHandler<,>) || def == typeof(IRequestHandler<>))
                    {
                        // Only register closed generics
                        if (iface.ContainsGenericParameters)
                        {
                            continue;
                        }

                        // Avoid duplicates
                        if (!ServiceExists(services, iface, t, ServiceLifetime.Scoped))
                        {
                            services.TryAdd(new ServiceDescriptor(iface, t, ServiceLifetime.Scoped));
                        }
                    }
                }
            }
        }
    }

    private static void RegisterNotificationHandlersRuntime(IServiceCollection services, IReadOnlyList<Assembly> assemblies)
    {
        for (int ai = 0; ai < assemblies.Count; ai++)
        {
            Assembly a = assemblies[ai];
            Type[] types;
            try
            {
                types = a.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.Where(t => t is not null).Cast<Type>()];
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                if (t is null || !t.IsClass || t.IsAbstract)
                {
                    continue;
                }

                Type[] ifaces = t.GetInterfaces();
                for (int j = 0; j < ifaces.Length; j++)
                {
                    Type iface = ifaces[j];
                    if (!iface.IsGenericType)
                    {
                        continue;
                    }

                    Type def = iface.GetGenericTypeDefinition();
                    if (def == typeof(INotificationHandler<>))
                    {
                        if (iface.ContainsGenericParameters)
                        {
                            continue;
                        }

                        // Match generator semantics (single registration per service/impl pair)
                        if (!ServiceExists(services, iface, t, ServiceLifetime.Scoped))
                        {
                            services.TryAdd(new ServiceDescriptor(iface, t, ServiceLifetime.Scoped));
                        }

                        // continue scanning other interfaces of this type (could implement multiple notifications)
                        // removed goto; normal loop continues scanning remaining interfaces
                    }
                }
            }
        }
    }

    private static void RegisterPipelineBehaviorsRuntime(IServiceCollection services, IReadOnlyList<Assembly> assemblies)
    {
        // Collect behaviors first so we can order by PipelineOrderAttribute
        List<(Type Service, Type Implementation, bool IsEnumerable)> items = [];

        for (int ai = 0; ai < assemblies.Count; ai++)
        {
            Assembly a = assemblies[ai];
            Type[] types;
            try
            {
                types = a.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.Where(t => t is not null).Cast<Type>()];
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                if (t is null || !t.IsClass || t.IsAbstract)
                {
                    continue;
                }

                Type[] ifaces = t.GetInterfaces();
                for (int j = 0; j < ifaces.Length; j++)
                {
                    Type iface = ifaces[j];
                    if (!iface.IsGenericType)
                    {
                        continue;
                    }

                    Type def = iface.GetGenericTypeDefinition();
                    bool supported = def == typeof(IPipelineBehavior<,>) || def == typeof(IPipelineBehavior<>) || def == typeof(IPipelineBehaviour<,>);
                    if (!supported)
                    {
                        continue;
                    }

                    if (iface.ContainsGenericParameters)
                    {
                        // open generic mapping: only if implementation is generic with matching arity
                        if (!t.IsGenericTypeDefinition)
                        {
                            continue;
                        }
                        Type service = def;
                        items.Add((service, t, true));
                    }
                    else
                    {
                        // closed mapping
                        items.Add((iface, t, true));
                    }
                }
            }
        }

        // Order by PipelineOrderAttribute (min value), then by name for stability
        items = [.. items
            .OrderBy(tuple => GetPipelineOrder(tuple.Implementation))
            .ThenBy(tuple => tuple.Implementation.FullName, StringComparer.Ordinal)];

        var emitted = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < items.Count; i++)
        {
            (Type service, Type impl, _) = items[i];
            string key = service.AssemblyQualifiedName + "|" + impl.AssemblyQualifiedName;
            if (!emitted.Add(key))
            {
                continue;
            }

            // Register enumerable pipeline behaviors so multiples can coexist
            if (!ServiceExists(services, service, impl, ServiceLifetime.Scoped))
            {
                services.TryAddEnumerable(new ServiceDescriptor(service, impl, ServiceLifetime.Scoped));
            }
        }
    }

    private static int GetPipelineOrder(Type behavior)
    {
        try
        {
            object[] attrs = behavior.GetCustomAttributes(inherit: true);
            int? min = null;
            for (int i = 0; i < attrs.Length; i++)
            {
                object attr = attrs[i];
                Type at = attr.GetType();
                if (string.Equals(at.Name, "PipelineOrderAttribute", StringComparison.Ordinal) && string.Equals(at.Namespace, typeof(IPipelineBehavior<,>).Namespace, StringComparison.Ordinal))
                {
                    PropertyInfo? orderProp = at.GetProperty("Order", BindingFlags.Public | BindingFlags.Instance);
                    if (orderProp is not null)
                    {
                        object? val = orderProp.GetValue(attr);
                        if (val is int oi)
                        {
                            min = min is null ? oi : Math.Min(min.Value, oi);
                        }
                    }
                }
            }
            return min ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static bool ServiceExists(IServiceCollection services, Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        for (int i = 0; i < services.Count; i++)
        {
            ServiceDescriptor sd = services[i];
            if (sd.ServiceType == serviceType && sd.ImplementationType == implementationType && sd.Lifetime == lifetime)
            {
                return true;
            }
        }
        return false;
    }

    internal sealed class AddDualisRuntimeMarker
    {
        public int Version { get; } = 1;
    }
}
