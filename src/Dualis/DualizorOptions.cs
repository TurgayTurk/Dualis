using Dualis.Notifications;
using Dualis.Pipeline;

namespace Dualis;

/// <summary>
/// Configures Dualizor behaviors that can be customized via DI.
/// </summary>
public sealed class DualizorOptions
{
    /// <summary>
    /// Factory used to resolve the <see cref="INotificationPublisher"/> implementation.
    /// Defaults to <see cref="SequentialNotificationPublisher"/>.
    /// </summary>
    public Func<IServiceProvider, INotificationPublisher> NotificationPublisherFactory { get; set; }
        = static _ => new SequentialNotificationPublisher();

    /// <summary>
    /// Determines how notification publishing deals with handler failures.
    /// Defaults to <see cref="NotificationFailureBehavior.ContinueAndAggregate"/>.
    /// </summary>
    public NotificationFailureBehavior NotificationFailureBehavior { get; set; }
        = NotificationFailureBehavior.ContinueAndAggregate;

    /// <summary>
    /// Maximum degree of parallelism for notification publishing when applicable.
    /// If <c>null</c>, a sensible default is chosen by the publisher (typically Environment.ProcessorCount).
    /// </summary>
    public int? MaxPublishDegreeOfParallelism { get; set; }

    /// <summary>
    /// If true (default), discovered pipeline behaviors are auto-registered by AddDualis.
    /// </summary>
    public bool RegisterDiscoveredBehaviors { get; set; } = true;

    /// <summary>
    /// If true (default), discovered ICommand and IQuery handlers are auto-registered by AddDualis.
    /// </summary>
    public bool RegisterDiscoveredCqrsHandlers { get; set; } = true;

    /// <summary>
    /// If true (default), discovered INotification handlers are auto-registered by AddDualis.
    /// </summary>
    public bool RegisterDiscoveredNotificationHandlers { get; set; } = true;

    /// <summary>
    /// Optional manual registration hook for pipeline behaviors.
    /// </summary>
    public PipelineRegistry Pipelines { get; } = new PipelineRegistry();

    /// <summary>
    /// Optional manual registration hook for CQRS handlers.
    /// </summary>
    public CqrsRegistry CQRS { get; } = new CqrsRegistry();

    /// <summary>
    /// Optional manual registration hook for Notification handlers.
    /// </summary>
    public NotificationRegistry Notifications { get; } = new NotificationRegistry();
}
