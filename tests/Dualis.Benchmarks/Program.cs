using Dualis.Pipeline;
using Dualis.CQRS;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;

namespace Dualis.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
#if DEBUG
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

    [Benchmark]
    public async Task QueryWithBehaviors() => await mediator.Send(new Ping("hi"));

    [Benchmark]
    public async Task CommandVoidWithBehaviors() => await mediator.Send(new Pong("x"));
}

public sealed record Ping(string Text) : IRequest<string>;

public sealed class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping query, CancellationToken cancellationToken) => Task.FromResult(query.Text);
}

public sealed record Pong(string Text) : IRequest;

public sealed class PongHandler : IRequestHandler<Pong>
{
    public Task Handle(Pong command, CancellationToken cancellationToken) => Task.CompletedTask;
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
