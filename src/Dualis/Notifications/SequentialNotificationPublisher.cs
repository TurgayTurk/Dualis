using Microsoft.Extensions.Logging;

namespace Dualis.Notifications;

/// <summary>
/// Invokes handlers one-by-one, honoring the configured failure behavior.
/// </summary>
public sealed class SequentialNotificationPublisher(ILogger<SequentialNotificationPublisher>? logger = null) : INotificationPublisher
{
    /// <summary>
    /// Publishes the notification to all handlers sequentially.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The notification instance to publish.</param>
    /// <param name="handlers">The set of handlers to invoke.</param>
    /// <param name="context">The publish context controlling failure behavior and parallelism.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    public async Task Publish<TNotification>(
        TNotification notification,
        IEnumerable<INotificationHandler<TNotification>> handlers,
        NotificationPublishContext context,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        List<Exception> exceptions = [];

        foreach (INotificationHandler<TNotification> handler in handlers)
        {
            try
            {
                await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (context.FailureBehavior == NotificationFailureBehavior.StopOnFirstException)
                {
                    throw;
                }

                if (context.FailureBehavior == NotificationFailureBehavior.ContinueAndLog && logger is not null)
                {
                    logger.LogError(ex, "Notification handler {Handler} failed for {NotificationType}.",
                        handler.GetType().FullName, typeof(TNotification).FullName);
                }

                if (context.FailureBehavior == NotificationFailureBehavior.ContinueAndAggregate)
                {
                    exceptions.Add(ex);
                }
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}
