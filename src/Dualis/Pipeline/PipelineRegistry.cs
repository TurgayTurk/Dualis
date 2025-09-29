using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dualis.Pipeline;

/// <summary>
/// Provides a fluent API to register pipeline behaviors into an <see cref="IServiceCollection"/>.
/// Supports request/response and void request pipelines.
/// </summary>
public sealed class PipelineRegistry
{
    private Action<IServiceCollection> registrations = static _ => { };

    /// <summary>
    /// When enabled, the AddDualis generator will auto-register discovered pipeline behaviors.
    /// Defaults to true to preserve existing behavior.
    /// </summary>
    public bool AutoRegisterEnabled { get; set; } = true;

    /// <summary>
    /// Fluent toggle to enable auto-registration in configuration chains.
    /// </summary>
    public PipelineRegistry AutoRegister
    {
        get
        {
            AutoRegisterEnabled = true;
            return this;
        }
    }

    /// <summary>
    /// Applies the accumulated registrations to the given service collection.
    /// </summary>
    /// <param name="services">The DI service collection to modify.</param>
    public void Apply(IServiceCollection services) => registrations(services);

    /// <summary>
    /// Unified registration for pipeline behaviors.
    /// - For open generics: registers mappings for supported pipeline interfaces the type implements.
    /// - For concrete (closed) types: registers the type itself and all supported interface mappings it implements.
    /// Supported interfaces: <see cref="IPipelineBehavior{TRequest, TResponse}"/> and <see cref="IPipelineBehavior{TRequest}"/>.
    /// </summary>
    /// <param name="behaviorType">The behavior type to register.</param>
    /// <returns>The same registry instance for chaining.</returns>
    public PipelineRegistry Register(Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);

        registrations += services =>
        {
            bool isOpenGeneric = behaviorType.IsGenericTypeDefinition;

            if (!isOpenGeneric)
            {
                services.TryAddScoped(behaviorType);
            }

            RegisterSupportedInterfaceMappings(services, behaviorType, isOpenGeneric);
        };

        return this;
    }

    /// <summary>
    /// Unified registration for a concrete behavior type.
    /// Registers the type itself and all supported interface mappings it implements.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior concrete type.</typeparam>
    /// <returns>The same registry instance for chaining.</returns>
    public PipelineRegistry Register<TBehavior>() where TBehavior : class
    {
        registrations += static services =>
        {
            Type behaviorType = typeof(TBehavior);
            services.TryAddScoped(behaviorType);
            RegisterSupportedInterfaceMappings(services, behaviorType, isOpenGeneric: false);
        };

        return this;
    }

    private static void RegisterSupportedInterfaceMappings(IServiceCollection services, Type behaviorType, bool isOpenGeneric)
    {
        Type[] interfaces = behaviorType.GetInterfaces();
        var unique = new HashSet<Type>();

        int implArity = behaviorType.IsGenericTypeDefinition ? behaviorType.GetGenericArguments().Length : -1;

        for (int i = 0; i < interfaces.Length; i++)
        {
            Type iface = interfaces[i];
            if (!iface.IsGenericType)
            {
                continue;
            }

            Type def = iface.GetGenericTypeDefinition();

            bool isSupported = def == typeof(IPipelineBehavior<,>)
                               || def == typeof(IPipelineBehavior<>);

            if (!isSupported)
            {
                continue;
            }

            if (isOpenGeneric)
            {
                int serviceArity = def.GetGenericArguments().Length;
                if (implArity != serviceArity)
                {
                    continue;
                }

                if (unique.Add(def))
                {
                    AddIfNotPresent(services, def, behaviorType);
                }
            }
            else
            {
                if (unique.Add(iface))
                {
                    AddIfNotPresent(services, iface, behaviorType);
                }
            }
        }
    }

    private static void AddIfNotPresent(IServiceCollection services, Type serviceType, Type implementationType)
    {
        for (int i = 0; i < services.Count; i++)
        {
            ServiceDescriptor sd = services[i];
            if (sd.ServiceType == serviceType && sd.ImplementationType == implementationType && sd.Lifetime == ServiceLifetime.Scoped)
            {
                return;
            }
        }

        services.AddScoped(serviceType, implementationType);
    }
}
