![](https://raw.githubusercontent.com/TurgayTurk/Dualis/main/logo.png)

# Dualis

Fast, lightweight mediator for .NET with unified requests, pipelines, and notifications. Dualis uses a Roslyn source generator to emit the dispatcher and DI registration code at build time, keeping runtime overhead and allocations low while offering a clean, opinionated API.

- Requests: `IRequest`/`IRequest<T>` with `IRequestHandler<>`/`IRequestHandler<,>`
- Pipelines: request/response and void pipeline behaviors (plus unified behaviour option)
- Notifications: fan-out publish with failure strategies and alternative publishers
- Source-generated `AddDualis` for DI registration and dispatcher implementation

## Install

NuGet:

```
dotnet add package Dualis
```

The package includes the source generator; no extra analyzer package is required.

## Enable code generation (host-only)

Enable the generators in your host/composition root project only (API/Web/Worker) using ONE of the following:

- Recommended: MSBuild property (no extra analyzer config needed when using the NuGet package, thanks to buildTransitive props)

```xml
<PropertyGroup>
  <DualisEnableGenerator>true</DualisEnableGenerator>
</PropertyGroup>
```

- Alternative: via `.editorconfig`/`.globalconfig` (works for both NuGet and ProjectReference)

```
is_global = true
build_property.DualisEnableGenerator = true
```

Notes:
- Only enable the generator in the host. Do not enable it in Domain/Application/Infrastructure projects.
- Add `using Dualis;` where you call `services.AddDualis()` so the generated extension is in scope.
- When Dualis is referenced via NuGet, the package ships a buildTransitive props file that exposes the property to the compiler automatically.
- When Dualis is referenced via ProjectReference (local dev), buildTransitive does not apply. Use the `.editorconfig` option above or make the property compiler-visible in the consuming project:

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="DualisEnableGenerator" />
</ItemGroup>
```

## Quick start

1) Register in DI (the source generator provides `AddDualis`). It auto-registers core services, discovered handlers, pipeline behaviors, and notification handlers.

```csharp
ServiceCollection services = new();
services.AddDualis();
IServiceProvider sp = services.BuildServiceProvider();
IDualizor dualizor = sp.GetRequiredService<IDualizor>();
```

2) Define a request and handler:

```csharp
public sealed record GetUser(Guid Id) : IRequest<UserDto>;

public sealed class GetUserHandler : IRequestHandler<GetUser, UserDto>
{
    public Task<UserDto> Handle(GetUser request, CancellationToken ct)
        => Task.FromResult(new UserDto(request.Id, "Alice"));
}
```

3) Send the request:

```csharp
UserDto user = await dualizor.Send(new GetUser(id));
```

Note: `IDualizor` implements both `ISender` (requests) and `IPublisher` (notifications). You may inject `ISender` or `IPublisher` instead of `IDualizor` if you only need a subset.

## Multi-project (DDD/CA) setup — host-only, cross-assembly auto-registration

- Reference Dualis abstractions in any layer.
- Enable generation in the host only and call `services.AddDualis()` there.
- The generator scans the host compilation and all referenced assemblies to discover public `IRequestHandler<>`/`IRequestHandler<,>` and pipeline behaviors (no reflection).
- Requirements:
  - Handler and behavior types in referenced assemblies must be `public` (or exposed via `InternalsVisibleTo` to the host).
  - Other projects should NOT enable the generator to avoid duplicate `AddDualis`/mediator.

### Registering from multiple layers

- Prefer a single `AddDualis` call in the host (composition root).
- If `AddDualis` is invoked multiple times (e.g., host + library), registration is idempotent:
  - A marker prevents re-wiring the graph more than once.
  - Helpers (`INotificationPublisher`, `NotificationPublishContext`, `SequentialNotificationPublisher`, `ParallelWhenAllNotificationPublisher`) use TryAdd to avoid duplicate descriptors.
  - Options Configure delegates are additive and run in order (last write wins for conflicting values).
- If multiple assemblies generate `ServiceCollectionExtensions.AddDualis`, extension-method ambiguity can occur. Keep generation enabled only in the host.

### Manual registrations in Application

When registering handlers manually from Application, register the interface mapping, not the message:

```csharp
// Correct
services.AddScoped<IRequestHandler<CreateUser>, CreateUserHandler>();
services.AddScoped<IRequestHandler<GetUser, UserDto>, GetUserHandler>();

// Incorrect (service type must be the handler interface)
services.AddScoped<CreateUser, CreateUserHandler>();
```

Manual registrations co-exist with auto-registration. `AddDualis` uses `TryAdd`/`TryAddScoped` so your explicit registrations win when added first.

## Minimal API example

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDualis(opts =>
{
    opts.NotificationFailureBehavior = NotificationFailureBehavior.ContinueAndAggregate;
    opts.MaxPublishDegreeOfParallelism = Environment.ProcessorCount;
});

WebApplication app = builder.Build();

app.MapPost("/create-user", async (CreateUser cmd, IDualizor dualizor, CancellationToken ct) =>
{
    Guid id = await dualizor.Send(cmd, ct);
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
- Auto-registration flags
  - `RegisterDiscoveredBehaviors` (default true)
  - `RegisterDiscoveredCqrsHandlers` (default true)
  - `RegisterDiscoveredNotificationHandlers` (default true)
- Startup validation (opt-in by options)
  - `EnableStartupValidation` (default true)
  - `StartupValidationMode` — `Throw` (default) | `Warn` | `Ignore` (set to `Ignore` to effectively disable)
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
        .Register<LoggingBehavior<CreateUser, Guid>>()
        .Register<ValidationBehavior<CreateUser, Guid>>();

    // Register handler types once; request/response types are discovered automatically
    opts.CQRS.Register<CreateUserHandler>();
    opts.CQRS.Register<GetUserHandler>();
    opts.Notifications.Register<UserCreatedEventHandler>();
});
```

Note: For manual registration from another assembly (e.g., Program.cs in Presentation), handler/behavior classes must be accessible. Make them public, or perform registration within the same assembly (e.g., via a public AddApplication extension) or use `InternalsVisibleTo`.

## Pipelines

Two primary forms are supported:

- Request/response: `IPipelineBehavior<TRequest, TResponse>`
- Void request: `IPipelineBehavior<TRequest>`

There is also a unified form `IPipelineBehaviour<TMessage, TResponse>` that can apply to both requests and notifications (`Unit` for void).

Behaviors are executed in registration order (outer -> inner). You can also annotate behaviors with `PipelineOrderAttribute` to control ordering when auto-registered. Lower values run earlier.

Example request/response behavior:

```csharp
[PipelineOrder(-10)]
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        => await next(ct);
}

[PipelineOrder(5)]
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        => await next(ct);
}
```

Example void behavior:

```csharp
public sealed class AuditBehavior<TRequest> : IPipelineBehavior<TRequest>
{
    public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken ct)
        => await next(ct);
}
```

Notes:
- Auto-registration honors `PipelineOrderAttribute` (ascending order), then sorts by type name to make ordering deterministic.
- When you manually register behaviors (as above), registration order applies regardless of the attribute.

## Notifications

Define a notification and handlers:

```csharp
public sealed record UserCreatedEvent(Guid Id) : INotification;

public sealed class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    public Task Handle(UserCreatedEvent n, CancellationToken ct)
        => Task.CompletedTask;
}
```

Publish from anywhere you have `IPublisher`/`IDualizor`:

```csharp
await dualizor.Publish(new UserCreatedEvent(id));
```

Choose failure behavior:

- `ContinueAndAggregate` — run all handlers, throw `AggregateException` of failures
- `ContinueAndLog` — log and swallow failures
- `StopOnFirstException` — stop immediately on first failure (sequential)

Choose publisher implementation via `NotificationPublisherFactory`.

## Caching

Runtime internals:

- Dualis caches discovered pipeline behaviors as arrays per handler shape to avoid repeated DI enumeration.
- A zero-behavior fast path calls the handler directly to minimize overhead.

Your code:

- Use `IMemoryCache` or `IDistributedCache` in handlers/behaviors like any DI service.

```csharp
public sealed class GetUserHandler(IMemoryCache cache) : IRequestHandler<GetUser, UserDto>
{
    public Task<UserDto> Handle(GetUser request, CancellationToken ct)
    {
        if (!cache.TryGetValue(request.Id, out UserDto value))
        {
            value = new UserDto(request.Id, "Alice");
            cache.Set(request.Id, value, TimeSpan.FromMinutes(5));
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

- At compile time, the generator scans the host compilation and referenced assemblies for implementations of `IRequestHandler<>`, `IRequestHandler<,>`, `INotificationHandler<>`, and pipeline interfaces.
- It generates a sealed `Dualis.Dualizor` class with direct, type-safe dispatch (no reflection) and minimal allocations, plus the `AddDualis` extension that wires everything into DI.
- The generator includes a safety guard: if a `Dualis.Dualizor` already exists in the compilation or references (e.g., a shared kernel), generation is skipped to avoid duplicate-type conflicts.
- The generated `AddDualis` is idempotent by design: duplicate calls won’t duplicate core registrations.

### When it runs (gating)

The generator runs when either of these is true in the project where you call `services.AddDualis(...)`:

- MSBuild property `DualisEnableGenerator` is visible to the compiler and set to `true` (see “Enable code generation”).
- Or a `.editorconfig`/`.globalconfig` sets `build_property.DualisEnableGenerator = true`.

### MSBuild property visibility

- If you set `DualisEnableGenerator` in the project/solution, ensure it’s visible to the compiler:
  - For NuGet consumption, this is automatic via the package’s buildTransitive props.
  - For ProjectReference, add `<CompilerVisibleProperty Include="DualisEnableGenerator" />` to the consuming project, or use the `.editorconfig` method.

### DDD/Clean Architecture guidance

- Reference Dualis abstractions in any layer. Enable generation in the composition root (API/Web/Worker) where you compose DI and call `services.AddDualis(...)`.
- Cross-assembly auto-registration collects public handlers/behaviors from referenced projects; make types public or use `InternalsVisibleTo` to the host.

### Troubleshooting

- "AddDualis not found" or "IServiceCollection does not contain a definition for AddDualis":
  - Ensure generator is enabled (MSBuild property or `.editorconfig`) in the host only.
  - Ensure `using Dualis;` is in scope where you call `services.AddDualis(...)`.
  - Clean + rebuild to clear stale analyzer artifacts.
- Duplicate type `Dualis.Dualizor`:
  - Another assembly defines the type. The generator will skip emitting it. Ensure DI registers the existing mediator to `IDualizor`/`ISender`/`IPublisher`.
- Not all handlers are auto-registered:
  - Ensure the handler/behavior classes are `public` (or visible via `InternalsVisibleTo`).
  - Ensure the host project references the assemblies that contain the handlers.
- Analyzer not loading in IDE:
  - Check Dependencies > Analyzers in your project to see Dualis’s analyzer is present.

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
