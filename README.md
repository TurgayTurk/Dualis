![](https://raw.githubusercontent.com/TurgayTurk/Dualis/main/logo.png)

# Dualis

Fast, lightweight mediator for .NET with CQRS, pipelines, and notifications. Dualis uses a Roslyn source generator to emit the dispatcher and DI registration code at build time, keeping runtime overhead and allocations low while offering a clean, opinionated API.

- CQRS: `ICommand`/`ICommand<T>`, `IQuery<T>` with `ICommandHandler<>`/`IQueryHandler<>`
- Pipelines: request/response, void, and unified pipeline behaviors
- Notifications: fan-out publish with failure strategies and alternative publishers
- Source-generated `AddDualis` for DI registration and dispatcher implementation

## Install

NuGet:

```
dotnet add package Dualis
```

The package includes the source generator; no extra analyzer package is required.

## Enable code generation (required)

Dualis generators are opt-in. Enable them in your host project using ONE of the following:

- Recommended: Assembly attribute in your project (e.g., `GlobalUsings.cs`)

```csharp
using Dualis;

[assembly: EnableDualisGeneration]
```

- Or via MSBuild property (make it compiler-visible)
  - Add to your project or `Directory.Build.props`:

```xml
<PropertyGroup>
  <DualisEnableGenerator>true</DualisEnableGenerator>
</PropertyGroup>
```

  - And make the property visible to the compiler using either option:
    - Add a `.globalconfig` at repo root:

```
is_global = true
build_property.DualisEnableGenerator = true
```

    - Or add to MSBuild (project or `Directory.Build.props`):

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="DualisEnableGenerator" />
</ItemGroup>
```

Notes:
- Add `using Dualis;` in files where you call `services.AddDualis()` so the generated extension is in scope.
- Source generators require SDK-style projects on supported SDKs/IDEs.

## Quick start

1) Register in DI (the source generator provides `AddDualis`). It auto-registers core services, discovered handlers, pipeline behaviors, and notification handlers.

```csharp
var services = new ServiceCollection();
services.AddDualis();
var sp = services.BuildServiceProvider();
var dualizor = sp.GetRequiredService<IDualizor>();
```

2) Define a query and handler:

```csharp
public sealed record GetUserQuery(Guid Id) : IQuery<UserDto>;

internal sealed class GetUserQueryHandler : IQueryHandler<GetUserQuery, UserDto>
{
    public Task<UserDto> HandleAsync(GetUserQuery query, CancellationToken ct = default)
        => Task.FromResult(new UserDto(query.Id, "Alice"));
}
```

3) Send the query:

```csharp
UserDto user = await dualizor.SendAsync(new GetUserQuery(id));
```

Note: `IDualizor` implements both `ISender` (commands/queries) and `IPublisher` (notifications). You may inject `ISender` or `IPublisher` instead of `IDualizor` if you only need a subset.

## Minimal API example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDualis(opts =>
{
    opts.NotificationFailureBehavior = NotificationFailureBehavior.ContinueAndAggregate;
    opts.MaxPublishDegreeOfParallelism = Environment.ProcessorCount;
});

var app = builder.Build();

app.MapPost("/create-user", async (CreateUserCommand cmd, IDualizor dualizor, CancellationToken ct) =>
{
    Guid id = await dualizor.SendAsync(cmd, ct);
    return Results.Ok(new { id });
});

await app.RunAsync();
```

## Dependency injection and options

Use the generated `AddDualis(IServiceCollection, Action<DualizorOptions>?)` to configure behavior.

- `NotificationPublisherFactory` – swap the publisher implementation
  - Default: `SequentialNotificationPublisher`
  - Alternatives: `ParallelWhenAllNotificationPublisher`, `ChannelNotificationPublisher`
- `NotificationFailureBehavior` – how to handle handler failures when publishing
  - `ContinueAndAggregate`, `ContinueAndLog`, `StopOnFirstException`
- `MaxPublishDegreeOfParallelism` – optional max DOP for publishers that support parallelism
- Auto-registration flags
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
            sp.GetRequiredService<ILogger<ParallelWhenAllNotificationPublisher>>(),
            sp.GetRequiredService<ILoggerFactory>());

    // Disable auto-registration and register explicitly
    opts.RegisterDiscoveredBehaviors = false;
    opts.RegisterDiscoveredCqrsHandlers = false;
    opts.RegisterDiscoveredNotificationHandlers = false;

    // If registration disabled, register explicitly
    opts.Pipelines
        .Register<LoggingBehavior<CreateUserCommand, Guid>>()
        .Register<ValidationBehavior<CreateUserCommand, Guid>>();

    // Note: Registering the handler type is sufficient; the associated
    // request (command/query) and response types are discovered automatically.
    opts.CQRS.Register<CreateUserCommandHandler>();
    opts.CQRS.Register<GetUserQueryHandler>();
    opts.Notifications.Register<UserCreatedEventHandler>();
});
```

Note: For manual registration from another assembly (e.g., Program.cs in Presentation), handler/behavior classes must be accessible. Make them public, or perform registration within the same assembly (e.g., via a public AddApplication extension) or use `InternalsVisibleTo`.

## Pipelines

Three forms are supported:

- Request/response: `IPipelineBehavior<TRequest, TResponse>`
- Void request: `IPipelineBehavior<TRequest>`
- Unified: `IPipelineBehaviour<TMessage, TResponse>` (for both requests and notifications; use `Unit` for void)

Behaviors are executed in registration order (outer -> inner). You can also annotate behaviors with `PipelineOrderAttribute` to control ordering when auto-registered. Lower values run earlier.

Example request/response behavior:

```csharp
[PipelineOrder(-10)]
internal sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // before
        var result = await next(ct);
        // after
        return result;
    }
}

[PipelineOrder(5)]
internal sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // validate here
        return await next(ct);
    }
}
```

Example void behavior:

```csharp
internal sealed class AuditBehavior<TRequest> : IPipelineBehavior<TRequest>
{
    public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken ct)
    {
        // before
        await next(ct);
        // after
    }
}
```

Notes:
- Auto-registration honors `PipelineOrderAttribute` (ascending order), then sorts by type name to make ordering deterministic.
- When you manually register behaviors (as above), registration order applies regardless of the attribute.

## Notifications

Define a notification and handlers:

```csharp
public sealed record UserCreatedEvent(Guid Id) : INotification;

internal sealed class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    public Task HandleAsync(UserCreatedEvent n, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

Publish from anywhere you have `IPublisher`/`IDualizor`:

```csharp
await dualizor.PublishAsync(new UserCreatedEvent(id));
```

Choose failure behavior:

- `ContinueAndAggregate` – run all handlers, throw `AggregateException` of failures
- `ContinueAndLog` – log and swallow failures
- `StopOnFirstException` – stop immediately on first failure (sequential)

Choose publisher implementation via `NotificationPublisherFactory`.

## Logging

Dualis uses `Microsoft.Extensions.Logging`.

- When `NotificationFailureBehavior` is set to `ContinueAndLog`, publishers log handler failures via `ILogger` and continue.
- Enable logging via your host builder; no special setup is required.
- To use logger-enabled publishers, resolve loggers from DI in the `NotificationPublisherFactory`:

```csharp
services.AddDualis(opts =>
{
    opts.NotificationFailureBehavior = NotificationFailureBehavior.ContinueAndLog;
    opts.NotificationPublisherFactory = sp =>
        new ParallelWhenAllNotificationPublisher(
            sp.GetRequiredService<ILogger<ParallelWhenAllNotificationPublisher>>(),
            sp.GetRequiredService<ILoggerFactory>());
});
```

## Caching

Runtime internals:

- Dualis caches discovered pipeline behaviors as arrays per handler shape to avoid repeated DI enumeration.
- A zero-behavior fast path calls the handler directly to minimize overhead.

Your code:

- Use `IMemoryCache` or `IDistributedCache` in handlers/behaviors like any DI service.

```csharp
internal sealed class GetUserQueryHandler(IMemoryCache cache) : IQueryHandler<GetUserQuery, UserDto>
{
    public Task<UserDto> HandleAsync(GetUserQuery query, CancellationToken ct = default)
    {
        if (!cache.TryGetValue(query.Id, out UserDto value))
        {
            value = new UserDto(query.Id, "Alice");
            cache.Set(query.Id, value, TimeSpan.FromMinutes(5));
        }
        return Task.FromResult(value);
    }
}
```

## Source generator

Dualis ships a source generator that:

- emits `Dualizor`, the concrete mediator/dispatcher used by `IDualizor`
- emits the `AddDualis` DI extension that auto-registers discovered handlers and behaviors (unless disabled by options)

This keeps the runtime lean and avoids reflection-based dispatch.

### How it works

- At compile time, the generator scans the compilation for implementations of `ICommandHandler<>`, `IQueryHandler<>`, `INotificationHandler<>`, and pipeline interfaces.
- It generates a sealed `Dualis.Dualizor` class with direct, type-safe dispatch (no reflection) and minimal allocations, plus the `AddDualis` extension that wires everything into DI.
- The generator includes a safety guard: if a `Dualis.Dualizor` already exists in the compilation or references (e.g., a shared kernel), generation is skipped to avoid duplicate-type conflicts.

### When it runs (gating)

The generator runs when either of these is true in the project where you call `services.AddDualis(...)`:

- `[assembly: EnableDualisGeneration]` is present, or
- MSBuild property `DualisEnableGenerator` is visible to the compiler and set to `true` (see “Enable code generation”).

### MSBuild property visibility

- If you set `DualisEnableGenerator` in `Directory.Build.props`, ensure it’s visible to the compiler:
  - Add `<CompilerVisibleProperty Include="DualisEnableGenerator" />` to the same `Directory.Build.props` or project file.
  - Or, add a `.globalconfig` at the repo root with `build_property.DualisEnableGenerator = true`.

### DDD/Clean Architecture guidance

- Reference Dualis abstractions in any layer. Enable generation in the composition root (API/Web/Worker) where you compose DI and call `services.AddDualis(...)`.

### Troubleshooting

- "AddDualis not found" or "IServiceCollection does not contain a definition for AddDualis":
  - Ensure generator is enabled (assembly attribute or compiler-visible property).
  - Ensure `using Dualis;` is in scope where you call `services.AddDualis(...)`.
  - Clean + rebuild to clear stale analyzer artifacts.
- Duplicate type `Dualis.Dualizor`:
  - Another assembly defines the type. The generator will skip emitting it. Ensure DI registers the existing mediator to `IDualizor`/`ISender`/`IPublisher`.
- Analyzer not loading in IDE:
  - Check Dependencies > Analyzers in your project to see `Dualis`’s analyzer is present.

## Benchmarks

Basic microbenchmarks live under `tests/Dualis.Benchmarks`. Run in Release:

```
dotnet run -c Release --project tests/Dualis.Benchmarks/Dualis.Benchmarks.csproj
```

## Requirements

- .NET 9 for the runtime library
- The source generator targets .NET Standard 2.0 so it works across SDKs and tooling

## Contributing

Issues and PRs are welcome. Please run unit tests and benchmarks before submitting changes.

## License

MIT
