using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using Dualis.CQRS.Commands;
using Dualis.CQRS.Queries;
using Dualis.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Dualis.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
#if DEBUG
        // Allow running benchmarks in Debug for local investigation by disabling the optimizations validator.
        IConfig config = DefaultConfig.Instance.WithOptions(ConfigOptions.DisableOptimizationsValidator);
        BenchmarkRunner.Run<DispatcherBenchmarks>(config);
#else
        BenchmarkRunner.Run<DispatcherBenchmarks>();
#endif
    }
}

[MemoryDiagnoser]
public class DispatcherBenchmarks
{
    private IDualizor mediator = default!;

    [Params(0, 1, 2, 3)]
    public int BehaviorCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        ServiceCollection services = new();

        services.AddDualis(opts =>
        {
            // Register response behaviors for queries
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

            // Register void behaviors for commands
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

        // simple handlers
        services.AddScoped<IQueryHandler<Ping, string>, PingHandler>();
        services.AddScoped<ICommandHandler<Pong>, PongHandler>();

        IServiceProvider provider = services.BuildServiceProvider();
        mediator = provider.GetRequiredService<IDualizor>();
    }

    [Benchmark]
    public async Task QueryWithBehaviors() => await mediator.QueryAsync(new Ping("hi"));

    [Benchmark]
    public async Task CommandVoidWithBehaviors() => await mediator.CommandAsync(new Pong("x"));
}

public sealed record Ping(string Text) : IQuery<string>;

public sealed class PingHandler : IQueryHandler<Ping, string>
{
    public Task<string> HandleAsync(Ping query, CancellationToken cancellationToken = default) => Task.FromResult(query.Text);
}

public sealed record Pong(string Text) : ICommand;

public sealed class PongHandler : ICommandHandler<Pong>
{
    public Task HandleAsync(Pong command, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        TResponse res = await next(cancellationToken);
        return res;
    }
}

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => await next(cancellationToken);
}

public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => await next(cancellationToken);
}

public sealed class VoidBehavior<TRequest> : IPipelineBehavior<TRequest>
{
    public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken) => await next(cancellationToken);
}

public sealed class VoidBehavior2<TRequest> : IPipelineBehavior<TRequest>
{
    public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken) => await next(cancellationToken);
}

public sealed class VoidBehavior3<TRequest> : IPipelineBehavior<TRequest>
{
    public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken) => await next(cancellationToken);
}
