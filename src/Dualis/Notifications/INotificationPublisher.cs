namespace Dualis.Notifications;

/// <summary>
/// Strategy for dispatching a notification to its handlers.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes the given notification to all handlers.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The notification instance.</param>
    /// <param name="handlers">The handlers to invoke for the notification.</param>
    /// <param name="context">Context controlling failure behavior and parallelism.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    Task Publish<TNotification>(
        TNotification notification,
        IEnumerable<INotificationHandler<TNotification>> handlers,
        NotificationPublishContext context,
        CancellationToken cancellationToken)
        where TNotification : INotification;
}
