using Dualis.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dualis.UnitTests;

/// <summary>
/// Notification type used to validate publisher failure behaviors.
/// </summary>
public sealed record N() : INotification;

/// <summary>
/// Handler that completes successfully.
/// </summary>
public sealed class Good : INotificationHandler<N>
{
    /// <inheritdoc />
    public Task Handle(N notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Handler that always throws to exercise failure paths.
/// </summary>
public sealed class Bad : INotificationHandler<N>
{
    /// <inheritdoc />
    public Task Handle(N notification, CancellationToken cancellationToken) => Task.FromException(new InvalidOperationException("bad"));
}

/// <summary>
/// Tests for notification publisher semantics across failure modes.
/// </summary>
public sealed class NotificationPublisherBehaviorTests
{
    /// <summary>
    /// Ensures sequential publisher aggregates exceptions when configured with ContinueAndAggregate.
    /// </summary>
    [Fact]
    public async Task Sequential_ContinueAndAggregate_Aggregates()
    {
        ServiceCollection services = new();
        services.AddDualis(opts =>
        {
            opts.RegisterDiscoveredBehaviors = false;
            opts.NotificationFailureBehavior = NotificationFailureBehavior.ContinueAndAggregate;
            opts.Notifications.Register<Good>();
            opts.Notifications.Register<Bad>();
        });

        IServiceProvider sp = services.BuildServiceProvider();
        IPublisher publisher = sp.GetRequiredService<IPublisher>();

        await Assert.ThrowsAsync<AggregateException>(() => publisher.Publish(new N()));
    }
}
