using Dualis.Notifications;
using Dualis.UnitTests.TestInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dualis.UnitTests.Notifications;

public sealed class SequentialNotificationPublisherTests
{
    private sealed record TestNote(int Value) : INotification;

    [Fact]
    public async Task ContinueAndAggregate_aggregates_and_throws()
    {
        List<INotificationHandler<TestNote>> handlers =
        [
            new DelegateNotificationHandler<TestNote>((_, _) => throw new InvalidOperationException("A")),
            new DelegateNotificationHandler<TestNote>((_, _) => Task.CompletedTask),
            new DelegateNotificationHandler<TestNote>((_, _) => throw new ApplicationException("B")),
        ];

        SequentialNotificationPublisher publisher = new();
        NotificationPublishContext context = new(NotificationFailureBehavior.ContinueAndAggregate, maxDegreeOfParallelism: null);

        Func<Task> act = () => publisher.PublishAsync(new TestNote(1), handlers, context, CancellationToken.None);

        FluentAssertions.Specialized.ExceptionAssertions<AggregateException> ex = await act.Should().ThrowAsync<AggregateException>();
        ex.Which.InnerExceptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ContinueAndLog_logs_and_swallows()
    {
        TestLoggerProvider provider = new();
        using SimpleLoggerFactory lf = new(provider);
        ILogger<SequentialNotificationPublisher> logger = lf.CreateLogger<SequentialNotificationPublisher>();

        List<INotificationHandler<TestNote>> handlers =
        [
            new DelegateNotificationHandler<TestNote>((_, _) => throw new InvalidOperationException("bad")),
            new DelegateNotificationHandler<TestNote>((_, _) => Task.CompletedTask)
        ];

        SequentialNotificationPublisher publisher = new(logger);
        NotificationPublishContext context = new(NotificationFailureBehavior.ContinueAndLog, maxDegreeOfParallelism: null);

        await publisher.PublishAsync(new TestNote(1), handlers, context, CancellationToken.None);

        provider.Entries.Should().ContainSingle(e => e.Level >= LogLevel.Error && e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task StopOnFirstException_throws_immediately()
    {
        int invoked = 0;
        List<INotificationHandler<TestNote>> handlers =
        [
            new DelegateNotificationHandler<TestNote>((_, _) => { Interlocked.Increment(ref invoked); return Task.CompletedTask; }),
            new DelegateNotificationHandler<TestNote>((_, _) => { Interlocked.Increment(ref invoked); throw new InvalidOperationException("fail"); }),
            new DelegateNotificationHandler<TestNote>((_, _) => { Interlocked.Increment(ref invoked); return Task.CompletedTask; }),
        ];

        SequentialNotificationPublisher publisher = new(NullLogger<SequentialNotificationPublisher>.Instance);
        NotificationPublishContext context = new(NotificationFailureBehavior.StopOnFirstException, null);

        Func<Task> act = () => publisher.PublishAsync(new TestNote(0), handlers, context, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        invoked.Should().Be(2);
    }
}
