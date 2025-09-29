using Dualis.CQRS.Queries;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace Dualis.PackageSmokeTests;

public sealed class SmokeTests
{
    [Fact]
    public async Task CanResolveAndSendQuery()
    {
        ServiceCollection services = new();
        services.AddDualis();
        ServiceProvider sp = services.BuildServiceProvider();
        IDualizor dualizor = sp.GetRequiredService<IDualizor>();

        UserDto result = await dualizor.SendAsync(new GetUser(new System.Guid("00000000-0000-0000-0000-000000000001")));

        result.Should().NotBeNull();
        result.Name.Should().Be("Alice");
    }
}

public sealed record GetUser(Guid Id) : IQuery<UserDto>;

public sealed record UserDto(System.Guid Id, string Name);

public sealed class GetUserHandler : IQueryHandler<GetUser, UserDto>
{
    public Task<UserDto> HandleAsync(GetUser query, CancellationToken cancellationToken = default)
        => Task.FromResult(new UserDto(query.Id, "Alice"));
}
