using Dualis.Notifications;

namespace Dualis.UnitTests.TestInfrastructure;

/// <summary>
/// Test helper that adapts a provided delegate to an <see cref="INotificationHandler{TNotification}"/> implementation.
/// </summary>
/// <typeparam name="T">The notification type.</typeparam>
public sealed class DelegateNotificationHandler<T>(Func<T, CancellationToken, Task> handler) : INotificationHandler<T>
    where T : INotification
{
    /// <summary>
    /// Forwards the notification to the supplied delegate.
    /// </summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">A token to observe while waiting for completion.</param>
    public Task HandleAsync(T notification, CancellationToken cancellationToken = default)
        => handler(notification, cancellationToken);
}
