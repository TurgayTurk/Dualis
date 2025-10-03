using Dualis.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dualis.DependencyInjection;

/// <summary>
/// DI helpers to register Dualizor and its options.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Dualizor infrastructure and allows configuration via <see cref="DualizorOptions"/>.
    /// Internal on purpose; the generated AddDualis method should be used by consumers.
    /// When source-generated Dualizor is unavailable, falls back to a reflection-based dispatcher.
    /// </summary>
    /// <param name="services">The service collection to modify.</param>
    /// <param name="configure">Optional callback to configure <see cref="DualizorOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    internal static IServiceCollection AddDualizor(this IServiceCollection services, Action<DualizorOptions>? configure = null)
    {
        services.AddOptions<DualizorOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddScoped(sp =>
        {
            DualizorOptions options = sp.GetRequiredService<IOptions<DualizorOptions>>().Value;
            return options.NotificationPublisherFactory(sp);
        });

        services.AddScoped(sp =>
        {
            DualizorOptions options = sp.GetRequiredService<IOptions<DualizorOptions>>().Value;
            return new NotificationPublishContext(options.NotificationFailureBehavior, options.MaxPublishDegreeOfParallelism);
        });

        // Default publishers available for factory selection
        services.AddScoped<SequentialNotificationPublisher>();
        services.AddScoped<ParallelWhenAllNotificationPublisher>();

        // Fallback: if generated Dualizor type is not present, register reflection-based one
        Type? dualizorType = Type.GetType("Dualis.Dualizor") ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Dualis.Dualizor", throwOnError: false)).FirstOrDefault(t => t is not null);
        if (dualizorType is null)
        {
            services.AddScoped<IDualizor, FallbackDualizor>();
            services.AddScoped<ISender, FallbackDualizor>();
            services.AddScoped<IPublisher, FallbackDualizor>();
        }
        else
        {
            // If the generated type is present but the public AddDualis was not invoked,
            // still map the interfaces so runtime path works.
            services.AddScoped(dualizorType, dualizorType);
            services.AddScoped(sp => (IDualizor)sp.GetRequiredService(dualizorType));
            services.AddScoped(sp => (ISender)sp.GetRequiredService(dualizorType));
            services.AddScoped(sp => (IPublisher)sp.GetRequiredService(dualizorType));
        }

        return services;
    }
}
