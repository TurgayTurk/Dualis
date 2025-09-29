using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Dualis.Notifications;

/// <summary>
/// Bounded-concurrency notification publisher using a Channel and background workers.
/// Suitable for high-throughput scenarios. Handlers for each notification are invoked
/// in parallel by a limited number of background workers.
/// </summary>
public sealed class ChannelNotificationPublisher : INotificationPublisher, IAsyncDisposable
{
    private readonly ILogger<ChannelNotificationPublisher>? logger;
    private readonly Channel<Func<CancellationToken, Task>> channel;
    private readonly List<Task> workers = [];
    private readonly NotificationFailureBehavior failureBehavior;

    /// <summary>
    /// Creates a new <see cref="ChannelNotificationPublisher"/>.
    /// </summary>
    /// <param name="capacity">Channel capacity (bounded).</param>
    /// <param name="maxConcurrency">Maximum number of background workers.</param>
    /// <param name="fullMode">Channel full-mode behavior.</param>
    /// <param name="failureBehavior">Failure behavior applied when handlers throw.</param>
    /// <param name="logger">Optional logger.</param>
    public ChannelNotificationPublisher(
        int capacity = 10_000,
        int maxConcurrency = 4,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
        NotificationFailureBehavior failureBehavior = NotificationFailureBehavior.ContinueAndAggregate,
        ILogger<ChannelNotificationPublisher>? logger = null)
    {
        this.logger = logger;
        this.failureBehavior = failureBehavior;

        BoundedChannelOptions options = new(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = fullMode,
            AllowSynchronousContinuations = false
        };
        channel = Channel.CreateBounded<Func<CancellationToken, Task>>(options);

        for (int i = 0; i < maxConcurrency; i++)
        {
            workers.Add(RunAsync());
        }
    }

    /// <summary>
    /// Publishes the specified notification by enqueueing handler work into the channel
    /// and awaiting completion across background workers.
    /// </summary>
    /// <typeparam name="TNotification">Notification type.</typeparam>
    /// <param name="notification">Notification instance.</param>
    /// <param name="handlers">Resolved handlers.</param>
    /// <param name="context">Publish context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PublishAsync<TNotification>(
        TNotification notification,
        IEnumerable<INotificationHandler<TNotification>> handlers,
        NotificationPublishContext context,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        List<INotificationHandler<TNotification>> list = [.. handlers];
        if (list.Count == 0)
        {
            return;
        }

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int remaining = list.Count;
        List<Exception> exceptions = [];

        foreach (INotificationHandler<TNotification> h in list)
        {
            async Task work(CancellationToken ct)
            {
                try
                {
                    await h.HandleAsync(notification, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (failureBehavior == NotificationFailureBehavior.ContinueAndLog && logger is not null)
                    {
                        logger.LogError(ex, "Notification handler {Handler} failed for {NotificationType}.", h.GetType().FullName, typeof(TNotification).FullName);
                    }

                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
                finally
                {
                    if (Interlocked.Decrement(ref remaining) == 0)
                    {
                        tcs.TrySetResult();
                    }
                }
            }

            await channel.Writer.WriteAsync(work, cancellationToken).ConfigureAwait(false);
        }

        await tcs.Task.ConfigureAwait(false);

        if (exceptions.Count > 0 && failureBehavior == NotificationFailureBehavior.ContinueAndAggregate)
        {
            throw new AggregateException(exceptions);
        }

        if (exceptions.Count > 0 && failureBehavior == NotificationFailureBehavior.StopOnFirstException)
        {
            throw exceptions[0];
        }
    }

    private async Task RunAsync()
    {
        try
        {
            while (await channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out Func<CancellationToken, Task>? work))
                {
                    try
                    {
                        await work(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (logger is not null)
                    {
                        // Best-effort worker loop: individual handler exceptions have been captured per item;
                        // log unexpected execution failures here and continue.
                        logger.LogError(ex, "Unhandled exception in ChannelNotificationPublisher worker loop.");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Reader canceled or shutdown requested. Safe to ignore during disposal.
        }
    }

    /// <summary>
    /// Completes the channel and awaits all workers.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        channel.Writer.TryComplete();
        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch (Exception ex) when (logger is not null)
        {
            // Swallow worker completion exceptions on disposal; workers may fail due to in-flight handler errors already surfaced above.
            logger.LogDebug(ex, "Ignoring worker exceptions during ChannelNotificationPublisher.DisposeAsync.");
        }
    }
}
