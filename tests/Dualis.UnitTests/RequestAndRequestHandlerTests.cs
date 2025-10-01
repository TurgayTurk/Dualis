using Dualis.CQRS;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dualis.UnitTests;

public sealed class RequestAndRequestHandlerTests
{
    // Query via IRequest<T>
    public sealed record GetNumber(int Value) : IQuery<int>;
    public sealed class GetNumberHandler : IQueryHandler<GetNumber, int>
    {
        public Task<int> HandleAsync(GetNumber query, CancellationToken cancellationToken = default) => Task.FromResult(query.Value);
    }

    // Command via IRequest
    public sealed record DoWork() : ICommand;
    public sealed class DoWorkHandler : ICommandHandler<DoWork>
    {
        public Task HandleAsync(DoWork command, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task SendAsync_IRequestOfT_routes_to_query_handler()
    {
        ServiceCollection services = new();
        services.AddDualis();
        services.AddScoped<IQueryHandler<GetNumber, int>, GetNumberHandler>();
        IServiceProvider sp = services.BuildServiceProvider();
        IDualizor mediator = sp.GetRequiredService<IDualizor>();

        IRequest<int> request = new GetNumber(42);
        int result = await mediator.SendAsync(request);

        result.Should().Be(42);
    }

    [Fact]
    public async Task SendAsync_IRequest_routes_to_command_handler()
    {
        ServiceCollection services = new();
        services.AddDualis();
        services.AddScoped<ICommandHandler<DoWork>, DoWorkHandler>();
        IServiceProvider sp = services.BuildServiceProvider();
        IDualizor mediator = sp.GetRequiredService<IDualizor>();

        IRequest request = new DoWork();
        await mediator.SendAsync(request);

        sp.GetRequiredService<ICommandHandler<DoWork>>().Should().NotBeNull();
    }

    [Fact]
    public async Task Send_overloads_forward_to_SendAsync_for_both_query_and_command()
    {
        ServiceCollection services = new();
        services.AddDualis();
        services.AddScoped<IQueryHandler<GetNumber, int>, GetNumberHandler>();
        services.AddScoped<ICommandHandler<DoWork>, DoWorkHandler>();
        IServiceProvider sp = services.BuildServiceProvider();
        ISender sender = sp.GetRequiredService<ISender>();

        int val = await sender.Send<int>(new GetNumber(7));
        val.Should().Be(7);

        await sender.Send(new DoWork());
    }

    // IRequestHandler direct tests
    public sealed record Ping(string Name) : ICommand;
    public sealed class TestState
    {
        public bool Called { get; private set; }
        public void Mark() => Called = true;
    }

    public sealed class PingHandler(TestState state) : IRequestHandler<Ping>
    {
        public Task HandleAsync(Ping request, CancellationToken cancellationToken = default)
        {
            state.Mark();
            return Task.CompletedTask;
        }
    }

    public sealed record Sum(int A, int B) : IQuery<int>;

    public sealed class SumHandler : IRequestHandler<Sum, int>
    {
        public Task<int> HandleAsync(Sum request, CancellationToken cancellationToken = default) => Task.FromResult(request.A + request.B);
    }

    [Fact]
    public async Task IRequestHandler_can_be_resolved_and_invoked_for_void_and_response()
    {
        ServiceCollection services = new();
        services.AddDualis();
        services.AddSingleton<TestState>();
        services.AddScoped<IRequestHandler<Ping>, PingHandler>();
        services.AddScoped<IRequestHandler<Sum, int>, SumHandler>();
        IServiceProvider sp = services.BuildServiceProvider();

        IRequestHandler<Ping> voidHandler = sp.GetRequiredService<IRequestHandler<Ping>>();
        await voidHandler.HandleAsync(new Ping("x"));
        sp.GetRequiredService<TestState>().Called.Should().BeTrue();

        IRequestHandler<Sum, int> respHandler = sp.GetRequiredService<IRequestHandler<Sum, int>>();
        int res = await respHandler.HandleAsync(new Sum(2, 3));
        res.Should().Be(5);
    }
}
