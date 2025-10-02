using Dualis.Notifications;
using Dualis.UnitTests.TestInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dualis.UnitTests.Notifications;

/// <summary>
/// Tests for <see cref="SequentialNotificationPublisher"/> covering failure handling policies.
/// </summary>
public sealed class SequentialNotificationPublisherTests
{
    private sealed record TestNote(int Value) : INotification;

    /// <summary>
    /// Ensures <c>ContinueAndAggregate</c> collects all failures and throws a single <see cref="AggregateException"/>.
    /// </summary>
    /// <remarks>
    /// Arrange: Two failing handlers and one successful handler.
    /// Act: Publish with <c>ContinueAndAggregate</c>.
    /// Assert: Thrown exception contains two inner exceptions.
    /// </remarks>
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

        Func<Task> act = () => publisher.Publish(new TestNote(1), handlers, context, CancellationToken.None);

        FluentAssertions.Specialized.ExceptionAssertions<AggregateException> ex = await act.Should().ThrowAsync<AggregateException>();
        ex.Which.InnerExceptions.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies <c>ContinueAndLog</c> logs failures without throwing.
    /// </summary>
    /// <remarks>
    /// Arrange: One failing handler and a test logger.
    /// Act: Publish with <c>ContinueAndLog</c>.
    /// Assert: Exactly one error log entry is recorded.
    /// </remarks>
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

        await publisher.Publish(new TestNote(1), handlers, context, CancellationToken.None);

        provider.Entries.Should().ContainSingle(e => e.Level >= LogLevel.Error && e.Exception is InvalidOperationException);
    }

    /// <summary>
    /// Ensures <c>StopOnFirstException</c> throws immediately and stops invoking subsequent handlers.
    /// </summary>
    /// <remarks>
    /// Arrange: Three handlers where the second throws.
    /// Act: Publish with <c>StopOnFirstException</c>.
    /// Assert: An exception is thrown and only two handlers were invoked.
    /// </remarks>
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

        Func<Task> act = () => publisher.Publish(new TestNote(0), handlers, context, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        invoked.Should().Be(2);
    }
}
