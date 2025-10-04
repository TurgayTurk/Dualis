using Dualis.Pipeline;
using Dualis.CQRS;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using Dualis.Notifications;

namespace Dualis.Benchmarks;

/// <summary>
/// Entry point for running all benchmarks in this assembly via BenchmarkSwitcher.
/// In Debug, disables the optimizations validator to allow local iteration.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs all benchmarks in this assembly using BenchmarkDotNet.
    /// </summary>
    public static void Main(string[] args)
    {
#if DEBUG
        IConfig config = DefaultConfig.Instance.WithOptions(ConfigOptions.DisableOptimizationsValidator);
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
#else
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
#endif
    }
}

/// <summary>
/// Benchmarks measuring the cost of request dispatch (Send) with 0..3 pipeline behaviors
/// for both queries (with response) and commands (void).
/// </summary>
[MemoryDiagnoser]
public class DispatcherBenchmarks
{
    private IDualizor mediator = default!;

    /// <summary>
    /// Number of configured pipeline behaviors to include in the chain for the scenario.
    /// </summary>
    [Params(0, 1, 2, 3)]
    public int BehaviorCount { get; set; }

    /// <summary>
    /// Builds the DI container and registers Dualis, handlers, and the requested behaviors.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        ServiceCollection services = new();

        services.AddDualis(opts =>
        {
            if (BehaviorCount >= 1)
            {
                opts.Pipelines.Register<LoggingBehavior<Ping, string>>();
            }
            if (BehaviorCount >= 2)
            {
                opts.Pipelines.Register<ValidationBehavior<Ping, string>>();
            }
            if (BehaviorCount >= 3)
            {
                opts.Pipelines.Register<AuditBehavior<Ping, string>>();
            }

            if (BehaviorCount >= 1)
            {
                opts.Pipelines.Register<VoidBehavior<Pong>>();
            }
            if (BehaviorCount >= 2)
            {
                opts.Pipelines.Register<VoidBehavior2<Pong>>();
            }
            if (BehaviorCount >= 3)
            {
                opts.Pipelines.Register<VoidBehavior3<Pong>>();
            }
        });

        services.AddScoped<IRequestHandler<Ping, string>, PingHandler>();
        services.AddScoped<IRequestHandler<Pong>, PongHandler>();

        IServiceProvider provider = services.BuildServiceProvider();
        mediator = provider.GetRequiredService<IDualizor>();
    }

    /// <summary>
    /// Sends a query with the configured behavior count to measure request/response pipeline overhead.
    /// </summary>
    [Benchmark]
    public async Task QueryWithBehaviors() => await mediator.Send(new Ping("hi"));

    /// <summary>
    /// Sends a void command with the configured behavior count to measure pipeline overhead.
    /// </summary>
    [Benchmark]
    public async Task CommandVoidWithBehaviors() => await mediator.Send(new Pong("x"));
}

/// <summary>
/// Simple query message used by benchmarks.
/// </summary>
public sealed record Ping(string Text) : IRequest<string>;

/// <summary>
/// Query handler returning the request payload to minimize handler cost in measurements.
/// </summary>
public sealed class PingHandler : IRequestHandler<Ping, string>
{
    /// <inheritdoc />
    public Task<string> Handle(Ping query, CancellationToken cancellationToken) => Task.FromResult(query.Text);
}

/// <summary>
/// Simple command message used by benchmarks.
/// </summary>
public sealed record Pong(string Text) : IRequest;

/// <summary>
/// Command handler that completes successfully without additional work.
/// </summary>
public sealed class PongHandler : IRequestHandler<Pong>
{
    /// <inheritdoc />
    public Task Handle(Pong command, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Behavior that just forwards to the next delegate; used to simulate a logging step.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        TResponse res = await next(cancellationToken);
        return res;
    }
}

/// <summary>
/// Behavior that forwards to next; used to simulate validation overhead in the chain.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => await next(cancellationToken);
}

/// <summary>
/// Behavior that forwards to next; used to simulate auditing overhead in the chain.
/// </summary>
public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => await next(cancellationToken);
}

/// <summary>
/// Void behavior that forwards to next; used to simulate logging overhead.
/// </summary>
public sealed class VoidBehavior<TRequest> : IPipelineBehavior<TRequest>
{
    /// <inheritdoc />
    public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken) => await next(cancellationToken);
}

/// <summary>
/// Void behavior that forwards to next; used to simulate validation overhead.
/// </summary>
public sealed class VoidBehavior2<TRequest> : IPipelineBehavior<TRequest>
{
    /// <inheritdoc />
    public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken) => await next(cancellationToken);
}

/// <summary>
/// Void behavior that forwards to next; used to simulate auditing overhead.
/// </summary>
public sealed class VoidBehavior3<TRequest> : IPipelineBehavior<TRequest>
{
    /// <inheritdoc />
    public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken) => await next(cancellationToken);
}

/// <summary>
/// Benchmarks measuring notification publishing cost for sequential and parallel publishers
/// while varying the number of registered handlers.
/// </summary>
[MemoryDiagnoser]
public class NotificationBenchmarks
{
    private IPublisher publisher = default!;

    /// <summary>
    /// Number of notification handlers to register for the benchmark scenario.
    /// </summary>
    [Params(1, 5, 10)]
    public int HandlerCount { get; set; }

    /// <summary>
    /// Selects the parallel publisher when true; otherwise the sequential publisher.
    /// </summary>
    [Params(false, true)]
    public bool UseParallel { get; set; }

    /// <summary>
    /// Builds the DI container and configures the publisher and handler set according to parameters.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        ServiceCollection services = new();
        services.AddDualis(opts =>
        {
            // Select publisher
            opts.NotificationPublisherFactory = sp => UseParallel
                ? sp.GetRequiredService<ParallelWhenAllNotificationPublisher>()
                : sp.GetRequiredService<SequentialNotificationPublisher>();

            // Register N handlers based on HandlerCount
            if (HandlerCount >= 1) { opts.Notifications.Register<BenchGood1>(); }
            if (HandlerCount >= 2) { opts.Notifications.Register<BenchGood2>(); }
            if (HandlerCount >= 3) { opts.Notifications.Register<BenchGood3>(); }
            if (HandlerCount >= 4) { opts.Notifications.Register<BenchGood4>(); }
            if (HandlerCount >= 5) { opts.Notifications.Register<BenchGood5>(); }
            if (HandlerCount >= 6) { opts.Notifications.Register<BenchGood6>(); }
            if (HandlerCount >= 7) { opts.Notifications.Register<BenchGood7>(); }
            if (HandlerCount >= 8) { opts.Notifications.Register<BenchGood8>(); }
            if (HandlerCount >= 9) { opts.Notifications.Register<BenchGood9>(); }
            if (HandlerCount >= 10) { opts.Notifications.Register<BenchGood10>(); }
        });

        IServiceProvider sp = services.BuildServiceProvider();
        publisher = sp.GetRequiredService<IPublisher>();
    }

    /// <summary>
    /// Publishes a notification to the configured number of handlers using the selected publisher strategy.
    /// </summary>
    [Benchmark]
    public Task PublishHandlers() => publisher.Publish(new BenchNote());
}

/// <summary>
/// Notification used by notification benchmarks.
/// </summary>
public sealed record BenchNote() : INotification;

/// <summary>Notification handler that completes immediately (1).</summary>
public sealed class BenchGood1 : INotificationHandler<BenchNote> { public Task Handle(BenchNote _, CancellationToken cancellationToken) => Task.CompletedTask; }
/// <summary>Notification handler that completes immediately (2).</summary>
public sealed class BenchGood2 : INotificationHandler<BenchNote> { public Task Handle(BenchNote _, CancellationToken cancellationToken) => Task.CompletedTask; }
/// <summary>Notification handler that completes immediately (3).</summary>
public sealed class BenchGood3 : INotificationHandler<BenchNote> { public Task Handle(BenchNote _, CancellationToken cancellationToken) => Task.CompletedTask; }
/// <summary>Notification handler that completes immediately (4).</summary>
public sealed class BenchGood4 : INotificationHandler<BenchNote> { public Task Handle(BenchNote _, CancellationToken cancellationToken) => Task.CompletedTask; }
/// <summary>Notification handler that completes immediately (5).</summary>
public sealed class BenchGood5 : INotificationHandler<BenchNote> { public Task Handle(BenchNote _, CancellationToken cancellationToken) => Task.CompletedTask; }
/// <summary>Notification handler that completes immediately (6).</summary>
public sealed class BenchGood6 : INotificationHandler<BenchNote> { public Task Handle(BenchNote _, CancellationToken cancellationToken) => Task.CompletedTask; }
/// <summary>Notification handler that completes immediately (7).</summary>
public sealed class BenchGood7 : INotificationHandler<BenchNote> { public Task Handle(BenchNote _, CancellationToken cancellationToken) => Task.CompletedTask; }
/// <summary>Notification handler that completes immediately (8).</summary>
public sealed class BenchGood8 : INotificationHandler<BenchNote> { public Task Handle(BenchNote _, CancellationToken cancellationToken) => Task.CompletedTask; }
/// <summary>Notification handler that completes immediately (9).</summary>
public sealed class BenchGood9 : INotificationHandler<BenchNote> { public Task Handle(BenchNote _, CancellationToken cancellationToken) => Task.CompletedTask; }
/// <summary>Notification handler that completes immediately (10).</summary>
public sealed class BenchGood10 : INotificationHandler<BenchNote> { public Task Handle(BenchNote _, CancellationToken cancellationToken) => Task.CompletedTask; }
