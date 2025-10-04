using Dualis.CQRS;
using Dualis.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dualis.UnitTests;

/// <summary>
/// Request used to validate pipeline ordering behavior.
/// </summary>
public sealed record R(int V) : IRequest<int>;

/// <summary>
/// Simple handler that returns the request value unchanged; pipeline behaviors transform the value.
/// </summary>
public sealed class Handler : IRequestHandler<R, int>
{
    /// <inheritdoc />
    public Task<int> Handle(R request, CancellationToken cancellationToken) => Task.FromResult(request.V);
}

/// <summary>
/// Behavior that runs later (higher order) and increments the result.
/// </summary>
[PipelineOrder(10)]
public sealed class B1 : IPipelineBehavior<R, int>
{
    /// <inheritdoc />
    public Task<int> Handle(R request, RequestHandlerDelegate<int> next, CancellationToken cancellationToken) => next(cancellationToken).ContinueWith(t => t.Result + 1, cancellationToken);
}

/// <summary>
/// Behavior that runs earlier (lower order) and doubles the result.
/// </summary>
[PipelineOrder(-5)]
public sealed class B0 : IPipelineBehavior<R, int>
{
    /// <inheritdoc />
    public Task<int> Handle(R request, RequestHandlerDelegate<int> next, CancellationToken cancellationToken) => next(cancellationToken).ContinueWith(t => t.Result * 2, cancellationToken);
}

/// <summary>
/// Tests that behaviors are ordered by <see cref="PipelineOrderAttribute"/> and then by type name for tie-breaking.
/// Disables auto-registration to avoid unrelated open-generic behaviors from other tests being registered.
/// </summary>
public sealed class PipelineOrderingTests
{
    /// <summary>
    /// Verifies that B0 executes before B1 based on order values, resulting in (3 * 2) + 1 = 7.
    /// </summary>
    [Fact]
    public async Task Behaviors_AreOrderedByPipelineOrder_ThenByName()
    {
        ServiceCollection services = new();
        services.AddDualis(opts =>
        {
            opts.RegisterDiscoveredBehaviors = false;
            opts.Pipelines.Register<B1>();
            opts.Pipelines.Register<B0>();
            opts.CQRS.Register<Handler>();
        });

        IServiceProvider sp = services.BuildServiceProvider();
        ISender sender = sp.GetRequiredService<ISender>();

        int res = await sender.Send(new R(3));
        // B0 runs first (-5): (3 * 2) = 6; B1 then (+1) => 7
        res.Should().Be(7);
    }
}
