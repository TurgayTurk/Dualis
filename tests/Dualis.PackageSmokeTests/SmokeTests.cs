using Dualis.CQRS.Queries;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace Dualis.PackageSmokeTests;

/// <summary>
/// Smoke tests that exercise the basic package integration: service registration and a simple query dispatch.
/// </summary>
public sealed class SmokeTests
{
    /// <summary>
    /// Verifies that <c>services.AddDualis()</c> wires up <see cref="IDualizor"/> so a query can be sent
    /// and handled successfully.
    /// </summary>
    /// <remarks>
    /// Arrange: Create a <see cref="ServiceCollection"/>, call <c>AddDualis()</c>, and build the provider. Resolve <see cref="IDualizor"/>.
    /// Act: Send a <see cref="GetUser"/> query with a known Id via <see cref="IDualizor.SendAsync{TResponse}(IQuery{TResponse}, CancellationToken)"/>.
    /// Assert: The returned <see cref="UserDto"/> is not null and has the expected Name.
    /// </remarks>
    [Fact]
    public async Task CanResolveAndSendQuery()
    {
        ServiceCollection services = new();
        services.AddDualis();
        ServiceProvider sp = services.BuildServiceProvider();
        IDualizor dualizor = sp.GetRequiredService<IDualizor>();

        UserDto result = await dualizor.SendAsync(new GetUser(new Guid("00000000-0000-0000-0000-000000000001")));

        result.Should().NotBeNull();
        result.Name.Should().Be("Alice");
    }
}

/// <summary>
/// Sample query used by the smoke test to request a user by identifier.
/// </summary>
public sealed record GetUser(Guid Id) : IQuery<UserDto>;

/// <summary>
/// Minimal DTO returned by the sample handler for validation in the smoke test.
/// </summary>
public sealed record UserDto(Guid Id, string Name);

/// <summary>
/// Sample handler that echoes the provided identifier and returns a fixed name for verification.
/// </summary>
public sealed class GetUserHandler : IQueryHandler<GetUser, UserDto>
{
    /// <inheritdoc />
    public Task<UserDto> HandleAsync(GetUser query, CancellationToken cancellationToken = default)
        => Task.FromResult(new UserDto(query.Id, "Alice"));
}
