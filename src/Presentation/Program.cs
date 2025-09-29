using Dualis;
using Dualis.Notifications;
using Presentation.Pipeline;
using Presentation.Testings;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDualis(options =>
{
    // Core notification publishing options
    options.NotificationFailureBehavior = NotificationFailureBehavior.ContinueAndAggregate;
    options.MaxPublishDegreeOfParallelism = Environment.ProcessorCount;
    options.NotificationPublisherFactory = static sp => sp.GetRequiredService<ParallelWhenAllNotificationPublisher>();
    // Alternative: channel-based bounded-concurrency publisher
    //options.NotificationPublisherFactory = sp => new ChannelNotificationPublisher(
    //    capacity: 10_000,
    //    maxConcurrency: Math.Max(2, Environment.ProcessorCount),
    //    fullMode: BoundedChannelFullMode.Wait,
    //    failureBehavior: options.NotificationFailureBehavior,
    //    logger: sp.GetService<ILogger<ChannelNotificationPublisher>>());

    // Pipelines (manual, optional)
    options.RegisterDiscoveredBehaviors = false;
    options.Pipelines.AutoRegisterEnabled = false;
    options.Pipelines
        .Register(typeof(UiPipeline<,>))
        .Register(typeof(UiVoidPipeline<>));

    // CQRS handlers (manual)
    options.RegisterDiscoveredCqrsHandlers = false;
    options.CQRS
        .Register<CreateUserCommandHandler>()   // ICommandHandler<CreateUserCommand, Guid>
        .Register<DeleteUserCommandHandler>()   // ICommandHandler<DeleteUserCommand>
        .Register<GetUserQueryHandler>();       // IQueryHandler<GetUserQuery, UserResponse>

    // Notification handlers (manual)
    options.RegisterDiscoveredNotificationHandlers = false;
    options.Notifications
        .Register<UserCreatedEventHandler>()     // INotificationHandler<USerCreatedEvent>
        .Register<UserCreatedLogEventHandler>(); // INotificationHandler<USerCreatedEvent>
});

WebApplication app = builder.Build();

app.MapPost("/create-user", async (CreateUserCommand command,
                                   IDualizor dualizor,
                                   CancellationToken cancellationToken) =>
{
    Guid userId = await dualizor.CommandAsync(command, cancellationToken);

    return Results.Ok(new { userId });
});

app.MapGet("/users/{id}", async (Guid id,
                                   IDualizor dualizor,
                                   CancellationToken cancellationToken) =>
{
    UserResponse user = await dualizor.QueryAsync(new GetUserQuery(id), cancellationToken);

    return Results.Ok(user);
});

app.MapDelete("/users/{id}", async (Guid id,
                                   IDualizor dualizor,
                                   CancellationToken cancellationToken) =>
{
    await dualizor.SendAsync(new DeleteUserCommand(), cancellationToken);

    return Results.Ok();
});

await app.RunAsync();
