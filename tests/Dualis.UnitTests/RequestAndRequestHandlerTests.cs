using Dualis.CQRS;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dualis.UnitTests;

public sealed class RequestAndRequestHandlerTests
{
    // Request with response via IRequest<T>
    public sealed record GetNumber(int Value) : IRequest<int>;
    public sealed class GetNumberHandler : IRequestHandler<GetNumber, int>
    {
        public Task<int> Handle(GetNumber query, CancellationToken cancellationToken) => Task.FromResult(query.Value);
    }

    // Request without response via IRequest
    public sealed record DoWork() : IRequest;
    public sealed class DoWorkHandler : IRequestHandler<DoWork>
    {
        public Task Handle(DoWork request, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task Send_IRequestOfT_routes_to_handler()
    {
        ServiceCollection services = new();
        services.AddDualis(opts => opts.RegisterDiscoveredBehaviors = false);
        services.AddScoped<IRequestHandler<GetNumber, int>, GetNumberHandler>();
        IServiceProvider sp = services.BuildServiceProvider();
        IDualizor mediator = sp.GetRequiredService<IDualizor>();

        IRequest<int> request = new GetNumber(42);
        int result = await mediator.Send(request);

        result.Should().Be(42);
    }

    [Fact]
    public async Task Send_IRequest_routes_to_handler()
    {
        ServiceCollection services = new();
        services.AddDualis(opts => opts.RegisterDiscoveredBehaviors = false);
        services.AddScoped<IRequestHandler<DoWork>, DoWorkHandler>();
        IServiceProvider sp = services.BuildServiceProvider();
        IDualizor mediator = sp.GetRequiredService<IDualizor>();

        IRequest request = new DoWork();
        await mediator.Send(request);

        sp.GetRequiredService<IRequestHandler<DoWork>>().Should().NotBeNull();
    }

    [Fact]
    public async Task Sender_Send_overloads_work_for_both_request_shapes()
    {
        ServiceCollection services = new();
        services.AddDualis(opts => opts.RegisterDiscoveredBehaviors = false);
        services.AddScoped<IRequestHandler<GetNumber, int>, GetNumberHandler>();
        services.AddScoped<IRequestHandler<DoWork>, DoWorkHandler>();
        IServiceProvider sp = services.BuildServiceProvider();
        ISender sender = sp.GetRequiredService<ISender>();

        int val = await sender.Send(new GetNumber(7));
        val.Should().Be(7);

        await sender.Send(new DoWork());
    }

    // IRequestHandler direct tests
    public sealed record Ping(string Name) : IRequest;
    public sealed class TestState
    {
        public bool Called { get; private set; }
        public void Mark() => Called = true;
    }

    public sealed class PingHandler(TestState state) : IRequestHandler<Ping>
    {
        public Task Handle(Ping request, CancellationToken cancellationToken)
        {
            state.Mark();
            return Task.CompletedTask;
        }
    }

    public sealed record Sum(int A, int B) : IRequest<int>;

    public sealed class SumHandler : IRequestHandler<Sum, int>
    {
        public Task<int> Handle(Sum request, CancellationToken cancellationToken) => Task.FromResult(request.A + request.B);
    }

    [Fact]
    public async Task IRequestHandler_can_be_resolved_and_invoked_for_void_and_response()
    {
        ServiceCollection services = new();
        services.AddDualis(opts => opts.RegisterDiscoveredBehaviors = false);
        services.AddSingleton<TestState>();
        services.AddScoped<IRequestHandler<Ping>, PingHandler>();
        services.AddScoped<IRequestHandler<Sum, int>, SumHandler>();
        IServiceProvider sp = services.BuildServiceProvider();

        IRequestHandler<Ping> voidHandler = sp.GetRequiredService<IRequestHandler<Ping>>();
        await voidHandler.Handle(new Ping("x"), CancellationToken.None);
        sp.GetRequiredService<TestState>().Called.Should().BeTrue();

        IRequestHandler<Sum, int> respHandler = sp.GetRequiredService<IRequestHandler<Sum, int>>();
        int res = await respHandler.Handle(new Sum(2, 3), CancellationToken.None);
        res.Should().Be(5);
    }
}
