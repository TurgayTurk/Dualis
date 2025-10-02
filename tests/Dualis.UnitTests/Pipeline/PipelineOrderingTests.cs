using Dualis.CQRS;
using Dualis.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dualis.UnitTests.Pipeline;

public sealed class PipelineOrderingTests
{
    internal sealed record Cmd(int Id) : IRequest<string>;

    internal sealed class CmdHandler(ExecutionLog log) : IRequestHandler<Cmd, string>
    {
        public Task<string> Handle(Cmd request, CancellationToken cancellationToken)
        {
            log.Add("H");
            return Task.FromResult($"ok-{request.Id}");
        }
    }

    [Fact]
    public async Task RequestBehaviors_run_in_configured_order()
    {
        ServiceCollection services = new();
        services.AddSingleton<ExecutionLog>();
        services.AddDualis();
        services.AddScoped<IRequestHandler<Cmd, string>, CmdHandler>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(OrderedBehaviorA<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(OrderedBehaviorB<,>));

        IServiceProvider sp = services.BuildServiceProvider();
        IDualizor mediator = sp.GetRequiredService<IDualizor>();
        ExecutionLog log = sp.GetRequiredService<ExecutionLog>();

        string res = await mediator.Send(new Cmd(1));

        res.Should().Be("ok-1");
        log.Snapshot().Should().ContainInOrder(["A:before", "B:before", "H", "B:after", "A:after"]);
    }
}
