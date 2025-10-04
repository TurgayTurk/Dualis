using Dualis.CQRS;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dualis.UnitTests;

/// <summary>
/// Verifies that the public AddDualis entry point works without the source generator
/// by falling back to the runtime reflection implementation, and that registration is idempotent.
/// </summary>
public sealed record Ping(string Msg) : IRequest<string>;

/// <summary>
/// Handler used by <see cref="AddDualisReflectionFallbackTests"/> to validate runtime dispatch.
/// </summary>
public sealed class PingHandler : IRequestHandler<Ping, string>
{
    /// <inheritdoc />
    public Task<string> Handle(Ping request, CancellationToken cancellationToken) => Task.FromResult(request.Msg);
}

/// <summary>
/// Tests for the reflection fallback path and idempotency of AddDualis.
/// </summary>
public sealed class AddDualisReflectionFallbackTests
{
    /// <summary>
    /// Ensures AddDualis registers the runtime fallback when no generated dispatcher is present.
    /// Disables auto-registration of discovered pipeline behaviors to avoid cross-test interference.
    /// </summary>
    [Fact]
    public async Task AddDualis_WithoutGenerator_RegistersRuntimeFallback()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddDualis(opts =>
        {
            opts.RegisterDiscoveredBehaviors = false;
            opts.CQRS.Register<PingHandler>();
        });

        // Act
        ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        ISender sender = sp.GetRequiredService<ISender>();
        sender.Should().NotBeNull();

        string res = await sender.Send(new Ping("ok"));
        res.Should().Be("ok");
    }

    /// <summary>
    /// Verifies that calling AddDualis multiple times does not duplicate the core graph.
    /// Subsequent calls may add additional configuration delegates if provided, so the second call is made without a configure action.
    /// </summary>
    [Fact]
    public void AddDualis_CanBeCalledTwice_IsIdempotent()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddDualis(opts => opts.RegisterDiscoveredBehaviors = false);
        int countAfterFirst = services.Count;
        services.AddDualis(); // no configure on second call
        int countAfterSecond = services.Count;

        // Assert: no additional descriptors added by a second call without configure
        countAfterSecond.Should().Be(countAfterFirst);
    }
}
