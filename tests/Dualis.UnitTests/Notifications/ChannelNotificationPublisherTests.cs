using Dualis.Notifications;
using Dualis.UnitTests.TestInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dualis.UnitTests.Notifications;

/// <summary>
/// Tests for <see cref="ChannelNotificationPublisher"/> covering fan-out behavior and failure handling policies.
/// </summary>
public sealed class ChannelNotificationPublisherTests
{
    private sealed record TestNote(int Value) : INotification;

    /// <summary>
    /// Verifies that all registered handlers are invoked once for a published notification.
    /// </summary>
    /// <remarks>
    /// Arrange: Create a publisher and three delegate handlers that increment a shared counter.
    /// Act: Publish a single <see cref="TestNote"/> with the <c>ContinueAndAggregate</c> policy.
    /// Assert: The counter equals the number of handlers (3).
    /// </remarks>
    [Fact]
    public async Task Publishes_to_all_handlers()
    {
        int calls = 0;

        await using ChannelNotificationPublisher publisher = new(
            capacity: 64,
            maxConcurrency: 4,
            fullMode: System.Threading.Channels.BoundedChannelFullMode.Wait,
            failureBehavior: NotificationFailureBehavior.ContinueAndAggregate,
            logger: NullLogger<ChannelNotificationPublisher>.Instance);

        List<INotificationHandler<TestNote>> handlers =
        [
            new DelegateNotificationHandler<TestNote>((_, _) => { Interlocked.Increment(ref calls); return Task.CompletedTask; }),
            new DelegateNotificationHandler<TestNote>((_, _) => { Interlocked.Increment(ref calls); return Task.CompletedTask; }),
            new DelegateNotificationHandler<TestNote>((_, _) => { Interlocked.Increment(ref calls); return Task.CompletedTask; }),
        ];

        NotificationPublishContext context = new(NotificationFailureBehavior.ContinueAndAggregate, maxDegreeOfParallelism: 4);

        await publisher.PublishAsync(new TestNote(10), handlers, context, CancellationToken.None);

        calls.Should().Be(3);
    }

    /// <summary>
    /// Ensures the <c>ContinueAndAggregate</c> policy throws an <see cref="AggregateException"/> with all inner exceptions.
    /// </summary>
    /// <remarks>
    /// Arrange: Use two failing handlers and one successful handler.
    /// Act: Publish a notification under the <c>ContinueAndAggregate</c> policy.
    /// Assert: An <see cref="AggregateException"/> is thrown that contains both failures.
    /// </remarks>
    [Fact]
    public async Task ContinueAndAggregate_throws_with_all_inner_exceptions()
    {
        await using ChannelNotificationPublisher publisher = new(
            capacity: 32,
            maxConcurrency: 2,
            fullMode: System.Threading.Channels.BoundedChannelFullMode.Wait,
            failureBehavior: NotificationFailureBehavior.ContinueAndAggregate,
            logger: NullLogger<ChannelNotificationPublisher>.Instance);

        List<INotificationHandler<TestNote>> handlers =
        [
            new DelegateNotificationHandler<TestNote>((_, _) => throw new InvalidOperationException("A")),
            new DelegateNotificationHandler<TestNote>((_, _) => Task.CompletedTask),
            new DelegateNotificationHandler<TestNote>((_, _) => throw new ApplicationException("B")),
        ];

        NotificationPublishContext context = new(NotificationFailureBehavior.ContinueAndAggregate, maxDegreeOfParallelism: 2);

        Func<Task> act = () => publisher.PublishAsync(new TestNote(10), handlers, context, CancellationToken.None);

        FluentAssertions.Specialized.ExceptionAssertions<AggregateException> ex = await act.Should().ThrowAsync<AggregateException>();
        ex.Which.InnerExceptions.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies <c>ContinueAndLog</c> logs failures and does not throw, allowing subsequent handlers to continue.
    /// </summary>
    /// <remarks>
    /// Arrange: Two failing handlers and a <see cref="TestLoggerProvider"/> to capture logs.
    /// Act: Publish a notification with <c>ContinueAndLog</c>.
    /// Assert: Two error log entries are recorded, and no exception bubbles up.
    /// </remarks>
    [Fact]
    public async Task ContinueAndLog_logs_and_swallows()
    {
        TestLoggerProvider provider = new();
        using SimpleLoggerFactory lf = new(provider);
        ILogger<ChannelNotificationPublisher> logger = lf.CreateLogger<ChannelNotificationPublisher>();

        await using ChannelNotificationPublisher publisher = new(
            capacity: 32,
            maxConcurrency: 2,
            fullMode: System.Threading.Channels.BoundedChannelFullMode.Wait,
            failureBehavior: NotificationFailureBehavior.ContinueAndLog,
            logger: logger);

        List<INotificationHandler<TestNote>> handlers =
        [
            new DelegateNotificationHandler<TestNote>((_, _) => throw new InvalidOperationException("bad-1")),
            new DelegateNotificationHandler<TestNote>((_, _) => Task.CompletedTask),
            new DelegateNotificationHandler<TestNote>((_, _) => throw new InvalidOperationException("bad-2")),
        ];

        NotificationPublishContext context = new(NotificationFailureBehavior.ContinueAndLog, maxDegreeOfParallelism: 2);

        await publisher.PublishAsync(new TestNote(10), handlers, context, CancellationToken.None);

        provider.Entries.Count(e => e.Level >= LogLevel.Error && e.Exception is InvalidOperationException).Should().Be(2);
    }
}
