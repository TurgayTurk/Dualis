using Dualis.Notifications;

namespace Dualis;

/// <summary>
/// Mediator interface dedicated to publishing notifications, similar to MediatR's <c>IPublisher</c>.
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Publishes a notification to all registered handlers.
    /// </summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);
}
