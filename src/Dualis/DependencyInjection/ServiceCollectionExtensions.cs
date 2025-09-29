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

        return services;
    }
}
