# Dualis

Fast, lightweight mediator for .NET with CQRS, pipelines, and notifications. Dualis uses a Roslyn source generator to emit the dispatcher and DI registration code at build time, keeping runtime overhead and allocations low while offering a clean, opinionated API.

- CQRS: `ICommand`/`ICommand<T>`, `IQuery<T>` with `ICommandHandler<>`/`IQueryHandler<>`
- Pipelines: request/response, void, and unified pipeline behaviours
- Notifications: fan-out publish with failure strategies and alternative publishers
- Source-generated `AddDualis` for DI registration and dispatcher implementation

## Install

NuGet (when published):

```
dotnet add package Dualis
```

The package includes the source generator; no extra analyzer package is required.

## Quick start

1) Register in DI (the source generator provides `AddDualis`):

```csharp
var services = new ServiceCollection();
services.AddDualis();
var sp = services.BuildServiceProvider();
var mediator = sp.GetRequiredService<IDualizor>();
```

2) Define a query and handler:

```csharp
public sealed record GetUser(Guid Id) : IQuery<UserDto>;

public sealed class GetUserHandler : IQueryHandler<GetUser, UserDto>
{
    public Task<UserDto> HandleAsync(GetUser query, CancellationToken ct = default)
        => Task.FromResult(new UserDto(query.Id, "Alice"));
}
```

3) Send the query:

```csharp
UserDto user = await mediator.QueryAsync(new GetUser(id));
```

## Minimal API example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDualis(options =>
{
    options.NotificationFailureBehavior = NotificationFailureBehavior.ContinueAndAggregate;
    options.MaxPublishDegreeOfParallelism = Environment.ProcessorCount;
});

var app = builder.Build();

app.MapPost("/create-user", async (CreateUser cmd, IDualizor dualizor, CancellationToken ct) =>
{
    Guid id = await dualizor.CommandAsync(cmd, ct);
    return Results.Ok(new { id });
});

await app.RunAsync();
```

## Dependency injection and options

Use the generated `AddDualis(IServiceCollection, Action<DualizorOptions>?)` to configure behavior.

- `NotificationPublisherFactory` — swap the publisher implementation
  - Default: `SequentialNotificationPublisher`
  - Alternatives: `ParallelWhenAllNotificationPublisher`, `ChannelNotificationPublisher`
- `NotificationFailureBehavior` — how to handle handler failures when publishing
  - `ContinueAndAggregate`, `ContinueAndLog`, `StopOnFirstException`
- `MaxPublishDegreeOfParallelism` — optional max DOP for publishers that support parallelism
- Auto?registration flags
  - `RegisterDiscoveredBehaviors` (default true)
  - `RegisterDiscoveredCqrsHandlers` (default true)
  - `RegisterDiscoveredNotificationHandlers` (default true)
- Manual registries (always available, regardless of auto flags)
  - `Pipelines.Register<TBehavior>()`
  - `CQRS.Register<THandler>()`
  - `Notifications.Register<THandler>()`

Example: override publisher and register pipelines manually

```csharp
services.AddDualis(opts =>
{
    opts.NotificationFailureBehavior = NotificationFailureBehavior.ContinueAndLog;
    opts.MaxPublishDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount);
    opts.NotificationPublisherFactory = sp =>
        new ParallelWhenAllNotificationPublisher(
            sp.GetService<ILogger<ParallelWhenAllNotificationPublisher>>(),
            sp.GetService<ILoggerFactory>());

    // Disable auto-registration and register explicitly
    opts.RegisterDiscoveredBehaviors = false;
    opts.RegisterDiscoveredCqrsHandlers = false;
    opts.RegisterDiscoveredNotificationHandlers = false;

    opts.Pipelines
        .Register<LoggingBehavior<CreateUser, Guid>>()
        .Register<ValidationBehavior<CreateUser, Guid>>();

    opts.CQRS.Register<CreateUserHandler>();
    opts.Notifications.Register<UserCreatedHandler>();
});
```

## Pipelines

Three forms are supported:

- Request/response: `IPipelineBehavior<TRequest,TResponse>`
- Void request: `IPipelineBehavior<TRequest>`
- Unified: `IPipelineBehaviour<TMessage, TResponse>` (for both requests and notifications; use `Unit` for void)

Behaviors are executed in registration order (outer ? inner). You can annotate behaviors with custom ordering attributes and then register in the desired sequence.

Example request/response behavior:

```csharp
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // before
        var result = await next(ct);
        // after
        return result;
    }
}
```

Example void behavior:

```csharp
public sealed class AuditBehavior<TRequest> : IPipelineBehavior<TRequest>
{
    public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken ct)
    {
        // before
        await next(ct);
        // after
    }
}
```

## Notifications

Define a notification and handlers:

```csharp
public sealed record UserCreated(Guid Id) : INotification;

public sealed class UserCreatedHandler : INotificationHandler<UserCreated>
{
    public Task HandleAsync(UserCreated n, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

Publish from anywhere you have `IPublisher`/`IDualizor`:

```csharp
await mediator.PublishAsync(new UserCreated(id));
```

Choose failure behavior:

- `ContinueAndAggregate` — run all handlers, throw `AggregateException` of failures
- `ContinueAndLog` — log and swallow failures
- `StopOnFirstException` — stop immediately on first failure (sequential)

Choose publisher implementation via `NotificationPublisherFactory`.

## Source generator

Dualis ships a source generator that:

- emits `Dualizor`, the concrete mediator/dispatcher used by `IDualizor`
- emits the `AddDualis` DI extension that auto?registers discovered handlers and behaviors (unless disabled by options)

This keeps the runtime lean and avoids reflection-based dispatch.

## Benchmarks

Basic microbenchmarks live under `tests/Dualis.Benchmarks`. Run in Release:

```
dotnet run -c Release --project tests/Dualis.Benchmarks/Dualis.Benchmarks.csproj
```

## Requirements

- .NET 9 for the runtime library
- The source generator targets .NET Standard 2.0 so it works across SDKs tooling

## Contributing

Issues and PRs are welcome. Please run unit tests and benchmarks before submitting changes.

## License

MIT
