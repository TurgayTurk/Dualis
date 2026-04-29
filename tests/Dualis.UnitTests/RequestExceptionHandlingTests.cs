using Dualis.CQRS;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dualis.UnitTests;

public sealed class RequestExceptionHandlingTests
{
    public sealed record CrashRequest() : IRequest<string>;

    public sealed class CrashHandler : IRequestHandler<CrashRequest, string>
    {
        public Task<string> Handle(CrashRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }

    public sealed class CrashExceptionHandler : IRequestExceptionHandler<CrashRequest, string, InvalidOperationException>
    {
        public Task Handle(CrashRequest request, InvalidOperationException exception, RequestExceptionState<string> state, CancellationToken cancellationToken)
        {
            state.SetHandled("handled");
            return Task.CompletedTask;
        }
    }

    public sealed record VoidCrashRequest() : IRequest;

    public sealed class VoidCrashHandler : IRequestHandler<VoidCrashRequest>
    {
        public Task Handle(VoidCrashRequest request, CancellationToken cancellationToken)
            => throw new ArgumentException("bad");
    }

    public sealed class VoidCrashAction : IRequestExceptionAction<VoidCrashRequest, ArgumentException>
    {
        public static int Calls;

        public Task Execute(VoidCrashRequest request, ArgumentException exception, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    public sealed class BaseExceptionHandler : IRequestExceptionHandler<CrashRequest, string, Exception>
    {
        public Task Handle(CrashRequest request, Exception exception, RequestExceptionState<string> state, CancellationToken cancellationToken)
        {
            state.SetHandled("base");
            return Task.CompletedTask;
        }
    }

    public sealed class SpecificExceptionHandler : IRequestExceptionHandler<CrashRequest, string, InvalidOperationException>
    {
        public Task Handle(CrashRequest request, InvalidOperationException exception, RequestExceptionState<string> state, CancellationToken cancellationToken)
        {
            state.SetHandled("specific");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Response_request_exception_handler_can_handle_and_return_response()
    {
        ServiceCollection services = new();
        services.AddDualis(opts =>
        {
            opts.RegisterDiscoveredBehaviors = false;
            opts.RegisterDiscoveredCqrsHandlers = false;
            opts.CQRS.Register<CrashHandler>();
            opts.CQRS.Register<CrashExceptionHandler>();
        });

        ServiceProvider provider = services.BuildServiceProvider();
        ISender sender = provider.GetRequiredService<ISender>();

        string result = await sender.Send(new CrashRequest());

        result.Should().Be("handled");
    }

    [Fact]
    public async Task Void_request_exception_action_runs_and_exception_is_rethrown()
    {
        VoidCrashAction.Calls = 0;
        ServiceCollection services = new();
        services.AddDualis(opts =>
        {
            opts.RegisterDiscoveredBehaviors = false;
            opts.RegisterDiscoveredCqrsHandlers = false;
            opts.CQRS.Register<VoidCrashHandler>();
            opts.CQRS.Register<VoidCrashAction>();
        });

        ServiceProvider provider = services.BuildServiceProvider();
        ISender sender = provider.GetRequiredService<ISender>();

        Func<Task> act = async () => await sender.Send(new VoidCrashRequest());

        await act.Should().ThrowAsync<ArgumentException>();
        VoidCrashAction.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Response_exception_handler_prefers_specific_exception_type_before_base_type()
    {
        ServiceCollection services = new();
        services.AddDualis(opts =>
        {
            opts.RegisterDiscoveredBehaviors = false;
            opts.RegisterDiscoveredCqrsHandlers = false;
            opts.CQRS.Register<CrashHandler>();
            opts.CQRS.Register<BaseExceptionHandler>();
            opts.CQRS.Register<SpecificExceptionHandler>();
        });

        ServiceProvider provider = services.BuildServiceProvider();
        ISender sender = provider.GetRequiredService<ISender>();

        string result = await sender.Send(new CrashRequest());

        result.Should().Be("specific");
    }
}
