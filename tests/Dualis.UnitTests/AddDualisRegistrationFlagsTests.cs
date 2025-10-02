using Dualis.CQRS;
using Dualis.Notifications;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dualis.UnitTests;

/// <summary>
/// Tests around configuration flags applied via <c>AddDualis</c> registration options and manual registries.
/// </summary>
public sealed class AddDualisRegistrationFlagsTests
{
    public sealed record Q(int Id) : IRequest<string>;
    public sealed class QHandler : IRequestHandler<Q, string>
    {
        public Task<string> Handle(Q request, CancellationToken cancellationToken) => Task.FromResult("ok");
    }

    public sealed record C() : IRequest;
    public sealed class CHandler : IRequestHandler<C>
    {
        public Task Handle(C request, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public sealed record N() : INotification;
    public sealed class NHandler : INotificationHandler<N>
    {
        public Task Handle(N notification, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that disabling auto-registration prevents discovered handlers and behaviors from being added to DI.
    /// </summary>
    /// <remarks>
    /// Arrange: Configure <c>AddDualis</c> with all auto-registration flags set to false.
    /// Act: Resolve each handler type from the container.
    /// Assert: Each resolution throws <see cref="InvalidOperationException"/>.
    /// </remarks>
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

        Action resolveQ = () => sp.GetRequiredService<IRequestHandler<Q, string>>();
        resolveQ.Should().Throw<InvalidOperationException>();

        Action resolveC = () => sp.GetRequiredService<IRequestHandler<C>>();
        resolveC.Should().Throw<InvalidOperationException>();

        Action resolveN = () => sp.GetRequiredService<INotificationHandler<N>>();
        resolveN.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Ensures manual registrations are honored even when auto-registration flags are disabled.
    /// </summary>
    /// <remarks>
    /// Arrange: Register handlers explicitly via the manual registries and disable discovery flags.
    /// Act: Resolve the handlers from the container.
    /// Assert: Each required service is available.
    /// </remarks>
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

        sp.GetRequiredService<IRequestHandler<Q, string>>().Should().NotBeNull();
        sp.GetRequiredService<IRequestHandler<C>>().Should().NotBeNull();
        sp.GetRequiredService<INotificationHandler<N>>().Should().NotBeNull();
    }
}
