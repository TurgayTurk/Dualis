using Dualis.CQRS.Commands;
using Dualis.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dualis.UnitTests.Pipeline;

/// <summary>
/// Verifies that pipeline behaviors respect the configured ordering attribute when executing around a handler.
/// </summary>
public sealed class PipelineOrderingTests
{
    internal sealed record Cmd(int Id) : ICommand<string>;

    internal sealed class CmdHandler(ExecutionLog log) : ICommandHandler<Cmd, string>
    {
        /// <summary>
        /// Records a handler marker and returns a formatted result containing the command Id.
        /// </summary>
        public Task<string> HandleAsync(Cmd command, CancellationToken cancellationToken = default)
        {
            log.Add("H");
            return Task.FromResult($"ok-{command.Id}");
        }
    }

    /// <summary>
    /// Ensures request behaviors execute in ascending <see cref="PipelineOrderAttribute"/> order around the handler.
    /// </summary>
    /// <remarks>
    /// Arrange: Register <see cref="OrderedBehaviorA{TReq, TRes}"/> and <see cref="OrderedBehaviorB{TReq, TRes}"/> and a simple handler.
    /// Act: Send the <see cref="Cmd"/> through <see cref="IDualizor"/>.
    /// Assert: The response is correct and the execution log reflects the expected order.
    /// </remarks>
    [Fact]
    public async Task RequestBehaviors_run_in_configured_order()
    {
        ServiceCollection services = new();
        services.AddSingleton<ExecutionLog>();
        services.AddDualis(); // auto-register discovered behaviors
        services.AddScoped<ICommandHandler<Cmd, string>, CmdHandler>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(OrderedBehaviorA<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(OrderedBehaviorB<,>));

        IServiceProvider sp = services.BuildServiceProvider();
        IDualizor mediator = sp.GetRequiredService<IDualizor>();
        ExecutionLog log = sp.GetRequiredService<ExecutionLog>();

        string res = await mediator.SendAsync(new Cmd(1));

        res.Should().Be("ok-1");
        log.Snapshot().Should().ContainInOrder(["A:before", "B:before", "H", "B:after", "A:after"]);
    }
}
