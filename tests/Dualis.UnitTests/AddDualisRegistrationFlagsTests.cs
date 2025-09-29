using Dualis.CQRS.Commands;
using Dualis.CQRS.Queries;
using Dualis.Notifications;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dualis.UnitTests;

public sealed class AddDualisRegistrationFlagsTests
{
    public sealed record Q(int Id) : IQuery<string>;
    public sealed class QHandler : IQueryHandler<Q, string>
    {
        public Task<string> HandleAsync(Q query, CancellationToken cancellationToken = default) => Task.FromResult("ok");
    }

    public sealed record C() : ICommand;
    public sealed class CHandler : ICommandHandler<C>
    {
        public Task HandleAsync(C command, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public sealed record N() : INotification;
    public sealed class NHandler : INotificationHandler<N>
    {
        public Task HandleAsync(N notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void When_auto_registration_disabled_handlers_are_not_added()
    {
        ServiceCollection services = new();
        services.AddDualis(opts =>
        {
            opts.RegisterDiscoveredCqrsHandlers = false;
            opts.RegisterDiscoveredBehaviors = false;
            opts.RegisterDiscoveredNotificationHandlers = false;
        });

        IServiceProvider sp = services.BuildServiceProvider();

        Action resolveQ = () => sp.GetRequiredService<IQueryHandler<Q, string>>();
        resolveQ.Should().Throw<InvalidOperationException>();

        Action resolveC = () => sp.GetRequiredService<ICommandHandler<C>>();
        resolveC.Should().Throw<InvalidOperationException>();

        Action resolveN = () => sp.GetRequiredService<INotificationHandler<N>>();
        resolveN.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Manual_registries_are_applied_before_auto_flags()
    {
        ServiceCollection services = new();
        services.AddDualis(opts =>
        {
            opts.RegisterDiscoveredCqrsHandlers = false;
            opts.RegisterDiscoveredNotificationHandlers = false;
            opts.CQRS.Register<QHandler>()
                     .Register<CHandler>();
            opts.Notifications.Register<NHandler>();
        });

        IServiceProvider sp = services.BuildServiceProvider();

        sp.GetRequiredService<IQueryHandler<Q, string>>().Should().NotBeNull();
        sp.GetRequiredService<ICommandHandler<C>>().Should().NotBeNull();
        sp.GetRequiredService<INotificationHandler<N>>().Should().NotBeNull();
    }
}
