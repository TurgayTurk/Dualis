using Dualis.Notifications;

namespace Dualis;

/// <summary>
/// Mediator interface dedicated to publishing notifications.
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Publishes a notification to all registered handlers.
    /// </summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Publish(INotification notification, CancellationToken cancellationToken = default);
}
