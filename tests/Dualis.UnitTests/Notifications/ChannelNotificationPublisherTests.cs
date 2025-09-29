using Dualis.Notifications;
using Dualis.UnitTests.TestInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dualis.UnitTests.Notifications;

public sealed class ChannelNotificationPublisherTests
{
    private sealed record TestNote(int Value) : INotification;

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
