using Dualis;
using Dualis.CQRS.Commands;
using Dualis.CQRS.Queries;
using Dualis.Notifications;

namespace Presentation.Testings;

public sealed record CreateUserCommand(string Name) : ICommand<Guid>;
public sealed record UserResponse(Guid Id, string Name);
internal sealed class CreateUserCommandHandler(IDualizor dualizor, ILogger<CreateUserCommandHandler> logger) : ICommandHandler<CreateUserCommand, Guid>
{
    public async Task<Guid> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Created user {Username}", command.Name);

        await Task.Yield();

        var ucEvent = new USerCreatedEvent(Guid.NewGuid(), command.Name);
        await dualizor.PublishAsync(ucEvent, cancellationToken);

        return Guid.NewGuid();
    }
}

public sealed record GetUserQuery(Guid Id) : IQuery<UserResponse>;
internal sealed class GetUserQueryHandler(ILogger<GetUserQueryHandler> logger) : IQueryHandler<GetUserQuery, UserResponse>
{
    public async Task<UserResponse> HandleAsync(GetUserQuery query, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Returned user {Id}", query.Id);

        await Task.Yield();

        return new UserResponse(query.Id, "Turgay");
    }
}

public sealed record DeleteUserCommand : ICommand;

internal sealed class DeleteUserCommandHandler(ILogger<DeleteUserCommandHandler> logger) : ICommandHandler<DeleteUserCommand>
{
    public async Task HandleAsync(DeleteUserCommand command, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleted user");

        await Task.Yield();
    }
}

public sealed record USerCreatedEvent(Guid Id, string Name) : INotification;
internal sealed class UserCreatedEventHandler(ILogger<UserCreatedEventHandler> logger) : INotificationHandler<USerCreatedEvent>
{
    public async Task HandleAsync(USerCreatedEvent notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("User created event handled for {Id} - {Name}", notification.Id, notification.Name);
        await Task.Yield();
    }
}
internal sealed class UserCreatedLogEventHandler(ILogger<UserCreatedEventHandler> logger) : INotificationHandler<USerCreatedEvent>
{
    public async Task HandleAsync(USerCreatedEvent notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Log event handled for {Id} - {Name}", notification.Id, notification.Name);
        await Task.Yield();
    }
}
