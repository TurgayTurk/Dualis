using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Microsoft.Extensions.DependencyInjection;
using DN = Dualis.Notifications;
using DP = Dualis.Pipeline;
using DQ = Dualis.CQRS;
using MR = MediatR;

namespace Dualis.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
        IConfig config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .AddJob(Job.Default.WithToolchain(InProcessNoEmitToolchain.Instance));
        switcher.Run(args, config);
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

        services.AddDualisRuntime(opts =>
        {
            if (BehaviorCount >= 1)
            {
                opts.Pipelines.Register<DLoggingBehavior<DqPing, string>>();
            }
            if (BehaviorCount >= 2)
            {
                opts.Pipelines.Register<DValidationBehavior<DqPing, string>>();
            }
            if (BehaviorCount >= 3)
            {
                opts.Pipelines.Register<DAuditBehavior<DqPing, string>>();
            }

            if (BehaviorCount >= 1)
            {
                opts.Pipelines.Register<DVoidBehavior<DcPong>>();
            }
            if (BehaviorCount >= 2)
            {
                opts.Pipelines.Register<DVoidBehavior2<DcPong>>();
            }
            if (BehaviorCount >= 3)
            {
                opts.Pipelines.Register<DVoidBehavior3<DcPong>>();
            }
        });

        services.AddScoped<DQ.IRequestHandler<DqPing, string>, DqPingHandler>();
        services.AddScoped<DQ.IRequestHandler<DcPong>, DcPongHandler>();

        IServiceProvider provider = services.BuildServiceProvider();
        mediator = provider.GetRequiredService<IDualizor>();
    }

    [Benchmark]
    public async Task QueryWithBehaviors() => await mediator.Send(new DqPing("hi"));

    [Benchmark]
    public async Task CommandVoidWithBehaviors() => await mediator.Send(new DcPong("x"));
}

// Dualis-only message types and handlers
public sealed record DqPing(string Text) : DQ.IRequest<string>;

public sealed class DqPingHandler : DQ.IRequestHandler<DqPing, string>
{
    public Task<string> Handle(DqPing query, CancellationToken cancellationToken) => Task.FromResult(query.Text);
}

public sealed record DcPong(string Text) : DQ.IRequest;

public sealed class DcPongHandler : DQ.IRequestHandler<DcPong>
{
    public Task Handle(DcPong command, CancellationToken cancellationToken) => Task.CompletedTask;
}

// Dualis-only pipeline behaviors
public sealed class DLoggingBehavior<TRequest, TResponse> : DP.IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, DP.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        TResponse res = await next(cancellationToken);
        return res;
    }
}

public sealed class DValidationBehavior<TRequest, TResponse> : DP.IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, DP.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => await next(cancellationToken);
}

public sealed class DAuditBehavior<TRequest, TResponse> : DP.IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, DP.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => await next(cancellationToken);
}

public sealed class DVoidBehavior<TRequest> : DP.IPipelineBehavior<TRequest>
{
    public async Task Handle(TRequest request, DP.RequestHandlerDelegate next, CancellationToken cancellationToken) => await next(cancellationToken);
}

public sealed class DVoidBehavior2<TRequest> : DP.IPipelineBehavior<TRequest>
{
    public async Task Handle(TRequest request, DP.RequestHandlerDelegate next, CancellationToken cancellationToken) => await next(cancellationToken);
}

public sealed class DVoidBehavior3<TRequest> : DP.IPipelineBehavior<TRequest>
{
    public async Task Handle(TRequest request, DP.RequestHandlerDelegate next, CancellationToken cancellationToken) => await next(cancellationToken);
}

// Side-by-side Dualis vs MediatR benchmarks under comparable conditions
[MemoryDiagnoser]
public class DualisVsMediatRBenchmarks
{
    private IDualizor dualis = default!;
    private MR.ISender mediatr = default!;

    [Params(0, 1, 2, 3)]
    public int BehaviorCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Dualis container
        ServiceCollection dualisServices = new();
        dualisServices.AddDualisRuntime(opts =>
        {
            if (BehaviorCount >= 1)
            {
                opts.Pipelines.Register<DLoggingBehavior<DqPing, string>>();
            }
            if (BehaviorCount >= 2)
            {
                opts.Pipelines.Register<DValidationBehavior<DqPing, string>>();
            }
            if (BehaviorCount >= 3)
            {
                opts.Pipelines.Register<DAuditBehavior<DqPing, string>>();
            }
        });
        dualisServices.AddScoped<DQ.IRequestHandler<DqPing, string>, DqPingHandler>();
        IServiceProvider dualisProvider = dualisServices.BuildServiceProvider();
        dualis = dualisProvider.GetRequiredService<IDualizor>();

        // MediatR container (minimal comparable setup)
        ServiceCollection mediatrServices = new();
        mediatrServices.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            // Add behaviors matching count
            if (BehaviorCount >= 1)
            {
                cfg.AddOpenBehavior(typeof(MediatRLoggingBehavior<,>));
            }
            if (BehaviorCount >= 2)
            {
                cfg.AddOpenBehavior(typeof(MediatRValidationBehavior<,>));
            }
            if (BehaviorCount >= 3)
            {
                cfg.AddOpenBehavior(typeof(MediatRAuditBehavior<,>));
            }
        });
        mediatrServices.AddScoped<MR.IRequestHandler<MediatRPing, string>, MediatRPingHandler>();
        IServiceProvider mediatrProvider = mediatrServices.BuildServiceProvider();
        mediatr = mediatrProvider.GetRequiredService<MR.ISender>();
    }

    // Query benchmarks
    [Benchmark(Baseline = true)]
    public async Task<string> DualisQuery() => await dualis.Send(new DqPing("hi"));

    [Benchmark]
    public async Task<string> MediatRQuery() => await mediatr.Send(new MediatRPing("hi"));

    // Command (void) benchmarks
    private IDualizor dualisCmd = default!;
    private readonly DQ.IRequest voidCmd = new DcPong("x");
    private readonly MR.IRequest mediatrCmd = new MediatRPong("x");

    [GlobalSetup(Targets = [nameof(DualisCommandVoid), nameof(MediatRCommandVoid)])]
    public void SetupCommands()
    {
        // Dualis void path shares container with query; ensure handler is present
        ServiceCollection services = new();
        services.AddDualisRuntime(opts =>
        {
            if (BehaviorCount >= 1)
            {
                opts.Pipelines.Register<DVoidBehavior<DcPong>>();
            }
            if (BehaviorCount >= 2)
            {
                opts.Pipelines.Register<DVoidBehavior2<DcPong>>();
            }
            if (BehaviorCount >= 3)
            {
                opts.Pipelines.Register<DVoidBehavior3<DcPong>>();
            }
        });
        services.AddScoped<DQ.IRequestHandler<DcPong>, DcPongHandler>();
        IServiceProvider sp = services.BuildServiceProvider();
        dualisCmd = sp.GetRequiredService<IDualizor>();

        // MediatR void path
        ServiceCollection m = new();
        m.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            if (BehaviorCount >= 1)
            {
                cfg.AddOpenBehavior(typeof(MediatRVoidBehavior<,>));
            }
            if (BehaviorCount >= 2)
            {
                cfg.AddOpenBehavior(typeof(MediatRVoidBehavior2<,>));
            }
            if (BehaviorCount >= 3)
            {
                cfg.AddOpenBehavior(typeof(MediatRVoidBehavior3<,>));
            }
        });
        m.AddScoped<MR.IRequestHandler<MediatRPong>, MediatRPongHandler>();
        IServiceProvider sp2 = m.BuildServiceProvider();
        mediatr = sp2.GetRequiredService<MR.ISender>();
    }

    [Benchmark]
    public async Task DualisCommandVoid() => await dualisCmd.Send(voidCmd);

    [Benchmark]
    public async Task MediatRCommandVoid() => await mediatr.Send(mediatrCmd);
}

// MediatR equivalents used for side-by-side comparison
public sealed record MediatRPing(string Text) : MR.IRequest<string>;
public sealed class MediatRPingHandler : MR.IRequestHandler<MediatRPing, string>
{
    public Task<string> Handle(MediatRPing request, CancellationToken cancellationToken) => Task.FromResult(request.Text);
}
public sealed record MediatRPong(string Text) : MR.IRequest;
public sealed class MediatRPongHandler : MR.IRequestHandler<MediatRPong>
{
    public Task Handle(MediatRPong request, CancellationToken cancellationToken) => Task.CompletedTask;
}

// MediatR pipeline behaviors to mirror Dualis behavior counts
public sealed class MediatRLoggingBehavior<TRequest, TResponse> : MR.IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, MR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => await next(cancellationToken);
}
public sealed class MediatRValidationBehavior<TRequest, TResponse> : MR.IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, MR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => await next(cancellationToken);
}
public sealed class MediatRAuditBehavior<TRequest, TResponse> : MR.IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, MR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => await next(cancellationToken);
}

// For MediatR "void" requests, use Unit result behaviors with two generic args
public sealed class MediatRVoidBehavior<TRequest, TResponse> : MR.IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, MR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => await next(cancellationToken);
}
public sealed class MediatRVoidBehavior2<TRequest, TResponse> : MR.IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, MR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => await next(cancellationToken);
}
public sealed class MediatRVoidBehavior3<TRequest, TResponse> : MR.IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, MR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => await next(cancellationToken);
}

// ===================== Notifications benchmarks =====================
[MemoryDiagnoser]
public class DualisVsMediatRNotificationsBenchmarks
{
    [Params(1, 2, 4, 8)]
    public int HandlersCount { get; set; }

    private IPublisher dualis = default!;
    private MR.IPublisher mediatr = default!;

    private readonly DN.INotification dNote = new DNote();
    private readonly MR.INotification mNote = new MediatRNote();

    [GlobalSetup]
    public void Setup()
    {
        // Dualis with default sequential publisher
        ServiceCollection dualisServices = new();
        dualisServices.AddDualisRuntime(opts =>
        {
            // Keep default SequentialNotificationPublisher
        });

        RegisterDualisHandlers(dualisServices, HandlersCount);
        IServiceProvider dualisProvider = dualisServices.BuildServiceProvider();
        dualis = dualisProvider.GetRequiredService<IPublisher>();

        // MediatR
        ServiceCollection mediatrServices = new();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
        RegisterMediatRHandlers(mediatrServices, HandlersCount);
        IServiceProvider mediatrProvider = mediatrServices.BuildServiceProvider();
        mediatr = mediatrProvider.GetRequiredService<MR.IPublisher>();
    }

    [Benchmark(Baseline = true)]
    public async Task DualisPublish() => await dualis.Publish(dNote);

    [Benchmark]
    public async Task MediatRPublish() => await mediatr.Publish(mNote);

    private static void RegisterDualisHandlers(IServiceCollection services, int count)
    {
        // Register up to 8 distinct handler types; choose by count
        if (count >= 1)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH1>();
        }
        if (count >= 2)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH2>();
        }
        if (count >= 3)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH3>();
        }
        if (count >= 4)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH4>();
        }
        if (count >= 5)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH5>();
        }
        if (count >= 6)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH6>();
        }
        if (count >= 7)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH7>();
        }
        if (count >= 8)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH8>();
        }
    }

    private static void RegisterMediatRHandlers(IServiceCollection services, int count)
    {
        if (count >= 1)
        {
            services.AddScoped<MR.INotificationHandler<MediatRNote>, MnH1>();
        }
        if (count >= 2)
        {
            services.AddScoped<MR.INotificationHandler<MediatRNote>, MnH2>();
        }
        if (count >= 3)
        {
            services.AddScoped<MR.INotificationHandler<MediatRNote>, MnH3>();
        }
        if (count >= 4)
        {
            services.AddScoped<MR.INotificationHandler<MediatRNote>, MnH4>();
        }
        if (count >= 5)
        {
            services.AddScoped<MR.INotificationHandler<MediatRNote>, MnH5>();
        }
        if (count >= 6)
        {
            services.AddScoped<MR.INotificationHandler<MediatRNote>, MnH6>();
        }
        if (count >= 7)
        {
            services.AddScoped<MR.INotificationHandler<MediatRNote>, MnH7>();
        }
        if (count >= 8)
        {
            services.AddScoped<MR.INotificationHandler<MediatRNote>, MnH8>();
        }
    }
}

// Dualis-only notifications benchmarks
[MemoryDiagnoser]
public class DualisNotificationsBenchmarks
{
    [Params(1, 2, 4, 8)]
    public int HandlersCount { get; set; }

    private IPublisher dualis = default!;
    private readonly DN.INotification dNote = new DNote();

    [GlobalSetup]
    public void Setup()
    {
        ServiceCollection services = new();
        services.AddDualisRuntime();
        // register N handlers of DNote
        if (HandlersCount >= 1)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH1>();
        }
        if (HandlersCount >= 2)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH2>();
        }
        if (HandlersCount >= 3)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH3>();
        }
        if (HandlersCount >= 4)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH4>();
        }
        if (HandlersCount >= 5)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH5>();
        }
        if (HandlersCount >= 6)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH6>();
        }
        if (HandlersCount >= 7)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH7>();
        }
        if (HandlersCount >= 8)
        {
            services.AddScoped<DN.INotificationHandler<DNote>, DnH8>();
        }

        IServiceProvider provider = services.BuildServiceProvider();
        dualis = provider.GetRequiredService<IPublisher>();
    }

    [Benchmark(Baseline = true)]
    public async Task DualisPublish() => await dualis.Publish(dNote);
}

// Dualis notification and handlers
public sealed record DNote : DN.INotification;
public sealed class DnH1 : DN.INotificationHandler<DNote>
{
    public Task Handle(DNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class DnH2 : DN.INotificationHandler<DNote>
{
    public Task Handle(DNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class DnH3 : DN.INotificationHandler<DNote>
{
    public Task Handle(DNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class DnH4 : DN.INotificationHandler<DNote>
{
    public Task Handle(DNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class DnH5 : DN.INotificationHandler<DNote>
{
    public Task Handle(DNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class DnH6 : DN.INotificationHandler<DNote>
{
    public Task Handle(DNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class DnH7 : DN.INotificationHandler<DNote>
{
    public Task Handle(DNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class DnH8 : DN.INotificationHandler<DNote>
{
    public Task Handle(DNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

// MediatR notification and handlers
public sealed record MediatRNote : MR.INotification;
public sealed class MnH1 : MR.INotificationHandler<MediatRNote>
{
    public Task Handle(MediatRNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class MnH2 : MR.INotificationHandler<MediatRNote>
{
    public Task Handle(MediatRNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class MnH3 : MR.INotificationHandler<MediatRNote>
{
    public Task Handle(MediatRNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class MnH4 : MR.INotificationHandler<MediatRNote>
{
    public Task Handle(MediatRNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class MnH5 : MR.INotificationHandler<MediatRNote>
{
    public Task Handle(MediatRNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class MnH6 : MR.INotificationHandler<MediatRNote>
{
    public Task Handle(MediatRNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class MnH7 : MR.INotificationHandler<MediatRNote>
{
    public Task Handle(MediatRNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
public sealed class MnH8 : MR.INotificationHandler<MediatRNote>
{
    public Task Handle(MediatRNote notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
