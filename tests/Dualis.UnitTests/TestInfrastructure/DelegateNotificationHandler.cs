using Dualis.Notifications;

namespace Dualis.UnitTests.TestInfrastructure;

public sealed class DelegateNotificationHandler<T>(Func<T, CancellationToken, Task> handler) : INotificationHandler<T>
    where T : INotification
{
    public Task HandleAsync(T notification, CancellationToken cancellationToken = default)
        => handler(notification, cancellationToken);
}
