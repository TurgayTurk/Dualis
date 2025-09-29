using Microsoft.Extensions.Logging;

namespace Dualis.Notifications;

/// <summary>
/// Invokes handlers in parallel. Honors failure behavior and degree of parallelism.
/// </summary>
public sealed class ParallelWhenAllNotificationPublisher(ILogger<ParallelWhenAllNotificationPublisher>? logger = null, ILoggerFactory? loggerFactory = null) : INotificationPublisher
{
    /// <summary>
    /// Publishes the notification to all handlers using a parallel strategy.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The notification instance to publish.</param>
    /// <param name="handlers">The set of handlers to invoke.</param>
    /// <param name="context">The publish context controlling failure behavior and parallelism.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    public async Task PublishAsync<TNotification>(
        TNotification notification,
        IEnumerable<INotificationHandler<TNotification>> handlers,
        NotificationPublishContext context,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        List<INotificationHandler<TNotification>> handlerList = [.. handlers];

        if (handlerList.Count == 0)
        {
            return;
        }

        // StopOnFirstException must be deterministic; run sequentially to guarantee early stop.
        if (context.FailureBehavior == NotificationFailureBehavior.StopOnFirstException)
        {
            ILogger<SequentialNotificationPublisher>? seqLogger = loggerFactory?.CreateLogger<SequentialNotificationPublisher>();
            var fallback = new SequentialNotificationPublisher(seqLogger);
            await fallback.PublishAsync(notification, handlerList, context, cancellationToken).ConfigureAwait(false);
            return;
        }

        int dop = context.MaxDegreeOfParallelism.GetValueOrDefault(Environment.ProcessorCount);
        if (dop <= 1)
        {
            ILogger<SequentialNotificationPublisher>? seqLogger = loggerFactory?.CreateLogger<SequentialNotificationPublisher>();
            var fallback = new SequentialNotificationPublisher(seqLogger);
            await fallback.PublishAsync(notification, handlerList, context, cancellationToken).ConfigureAwait(false);
            return;
        }

        using SemaphoreSlim gate = new(dop);
        List<Task> tasks = new(handlerList.Count);
        List<Exception> exceptions = [];

        foreach (INotificationHandler<TNotification> h in handlerList)
        {
            tasks.Add(ExecuteAsync(h));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (exceptions.Count > 0 && context.FailureBehavior == NotificationFailureBehavior.ContinueAndAggregate)
        {
            throw new AggregateException(exceptions);
        }

        // ContinueAndLog: exceptions already logged and swallowed.

        async Task ExecuteAsync(INotificationHandler<TNotification> handler)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await handler.HandleAsync(notification, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (context.FailureBehavior == NotificationFailureBehavior.ContinueAndLog && logger is not null)
                {
                    logger.LogError(ex, "Notification handler {Handler} failed for {NotificationType}.",
                        handler.GetType().FullName, typeof(TNotification).FullName);
                }

                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
            finally
            {
                _ = gate.Release();
            }
        }
    }
}
