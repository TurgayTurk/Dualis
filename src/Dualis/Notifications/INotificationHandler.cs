namespace Dualis.Notifications;

/// <summary>
/// Handles a notification of type <typeparamref name="TNotification"/>.
/// Multiple handlers can be registered for the same notification type.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the notification.
    /// </summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken = default);
}
