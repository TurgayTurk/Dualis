using Dualis.CQRS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dualis;

/// <summary>
/// Provides a fluent API to manually register CQRS handlers (commands and queries).
/// </summary>
public sealed class CqrsRegistry
{
    private Action<IServiceCollection> registrations = static _ => { };

    /// <summary>
    /// Applies the accumulated registrations to the given service collection.
    /// </summary>
    /// <param name="services">The service collection to apply registrations to.</param>
    public void Apply(IServiceCollection services) => registrations(services);

    /// <summary>
    /// Unified registration for a handler implementation (open generic or concrete).
    /// Registers all supported CQRS handler interface mappings implemented by the type.
    /// Supported interfaces:
    ///   IRequestHandler{TCommand, TResponse}, IRequestHandler{TCommand}.
    /// </summary>
    /// <param name="handlerType">The handler type to register.</param>
    /// <returns>The same registry instance for chaining.</returns>
    public CqrsRegistry Register(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        registrations += services =>
        {
            bool isOpenGeneric = handlerType.IsGenericTypeDefinition;
            if (!isOpenGeneric)
            {
                services.TryAddScoped(handlerType);
            }

            RegisterSupportedInterfaceMappings(services, handlerType, isOpenGeneric);
        };

        return this;
    }

    /// <summary>
    /// Unified registration for a concrete handler implementation type.
    /// </summary>
    /// <typeparam name="THandler">The handler concrete type.</typeparam>
    /// <returns>The same registry instance for chaining.</returns>
    public CqrsRegistry Register<THandler>() where THandler : class
    {
        registrations += static services =>
        {
            Type handlerType = typeof(THandler);
            services.TryAddScoped(handlerType);
            RegisterSupportedInterfaceMappings(services, handlerType, isOpenGeneric: false);
        };
        return this;
    }

    private static void RegisterSupportedInterfaceMappings(IServiceCollection services, Type handlerType, bool isOpenGeneric)
    {
        Type[] interfaces = handlerType.GetInterfaces();
        HashSet<Type> unique = [];

        for (int i = 0; i < interfaces.Length; i++)
        {
            Type iface = interfaces[i];
            if (!iface.IsGenericType)
            {
                continue;
            }

            Type def = iface.GetGenericTypeDefinition();

            bool isSupported = def == typeof(IRequestHandler<,>) || def == typeof(IRequestHandler<>);
            if (!isSupported)
            {
                continue;
            }

            Type serviceType = isOpenGeneric ? def : iface;
            if (unique.Add(serviceType))
            {
                AddIfNotPresent(services, serviceType, handlerType);
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

/// <summary>
/// Provides a fluent API to manually register notification handlers.
/// </summary>
public sealed class NotificationRegistry
{
    private Action<IServiceCollection> registrations = static _ => { };

    /// <summary>
    /// Applies the accumulated registrations to the given service collection.
    /// </summary>
    /// <param name="services">The service collection to apply registrations to.</param>
    public void Apply(IServiceCollection services) => registrations(services);

    /// <summary>
    /// Unified registration for a notification handler implementation (open generic or concrete).
    /// Supports INotificationHandler{TNotification}.
    /// </summary>
    /// <param name="handlerType">The handler type to register.</param>
    /// <returns>The same registry instance for chaining.</returns>
    public NotificationRegistry Register(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        registrations += services =>
        {
            bool isOpenGeneric = handlerType.IsGenericTypeDefinition;
            if (!isOpenGeneric)
            {
                services.TryAddScoped(handlerType);
            }

            Type ifaceDef = typeof(Notifications.INotificationHandler<>);
            Type[] interfaces = handlerType.GetInterfaces();
            HashSet<Type> unique = [];
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type iface = interfaces[i];
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == ifaceDef)
                {
                    Type serviceType = isOpenGeneric ? ifaceDef : iface;
                    if (unique.Add(serviceType))
                    {
                        AddIfNotPresent(services, serviceType, handlerType);
                    }
                }
            }
        };

        return this;
    }

    /// <summary>
    /// Unified registration for a concrete notification handler.
    /// </summary>
    /// <typeparam name="THandler">The handler concrete type.</typeparam>
    /// <returns>The same registry instance for chaining.</returns>
    public NotificationRegistry Register<THandler>() where THandler : class
    {
        registrations += static services =>
        {
            Type handlerType = typeof(THandler);
            services.TryAddScoped(handlerType);

            Type ifaceDef = typeof(Notifications.INotificationHandler<>);
            Type[] interfaces = handlerType.GetInterfaces();
            HashSet<Type> unique = [];
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type iface = interfaces[i];
                if (iface.IsGenericType 
                    && iface.GetGenericTypeDefinition() == ifaceDef 
                    && unique.Add(iface))
                {
                    AddIfNotPresent(services, iface, handlerType);
                }
            }
        };
        return this;
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
