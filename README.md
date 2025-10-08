![](https://raw.githubusercontent.com/TurgayTurk/Dualis/main/logo.png)

# Dualis

Fast, lightweight mediator for .NET with unified requests, pipelines, and notifications. Dualis uses a Roslyn source generator to emit dispatcher and DI registration code at build time, keeping runtime overhead and allocations low while offering a clean, opinionated API.

- Requests: `IRequest`/`IRequest<T>` with `IRequestHandler<>`/`IRequestHandler<,>`
- Pipelines: request/response and void pipeline behaviors (plus unified behaviour option)
- Notifications: fan-out publish with failure strategies and alternative publishers
- Public `AddDualis` entry point for DI; source generator augments it in the host project

## What's new (0.2.5)

- Added: `Dualis.Analyzer` project. See installation and rule details in the analyzer README: [src/Dualis.Analyzer/README.md](https://github.com/TurgayTurk/Dualis/blob/main/src/Dualis.Analyzer/README.md).
- Changed: tiny performance refactoring in the source generator/dispatcher code paths.

See full details in [CHANGELOG.md](https://github.com/TurgayTurk/Dualis/blob/main/CHANGELOG.md).

## Install

NuGet:

```
dotnet add package Dualis
```

The package includes the source generator as analyzer assets. You must opt-in to generation in your host project.

## Enable generation (host-only)

Enable the generator only in the project where you call `services.AddDualis()` (the composition root: API/Web/Worker). Do not enable it in Domain/Application/Infrastructure projects.

Options to enable in the host:

- MSBuild property (recommended):

```
<PropertyGroup>
  <DualisEnableGenerator>true</DualisEnableGenerator>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="DualisEnableGenerator" />
</ItemGroup>
```

- Assembly attribute:

```
using Dualis;

[assembly: EnableDualisGeneration]
```

- .editorconfig/.globalconfig:

```
is_global = true
build_property.DualisEnableGenerator = true
```

Notes:
- Only the host should enable generation.
- The generator output is internal and not an extension method, so it cannot collide with the public entry.
- You can optionally set `EmitCompilerGeneratedFiles` to inspect generated files under `obj/generated`.

## Quick start

1) Register in DI. The public `AddDualis` is always available; when the generator runs in the host, it augments the registration.

```
var builder = WebApplication.CreateBuilder(args);

// Dualis auto-discovers and registers handlers, pipeline behaviors, and notifications
// from the host compilation and referenced assemblies. No manual DI registration required.
builder.Services.AddDualis();

var app = builder.Build();
```

2) Define a query and handler:

```
public sealed record GetUserByNameQuery(string Name) : IRequest<UserDto?>;

public sealed class GetUserByNameQueryHandler : IRequestHandler<GetUserByNameQuery, UserDto?>
{
    public Task<UserDto?> Handle(GetUserByNameQuery request, CancellationToken ct)
        => Task.FromResult<UserDto?>(new UserDto(request.Name));
}
```

3) Use `ISender` in Minimal API to send the request:

```
app.MapGet("/users/{name}", async (string name, ISender sender, CancellationToken ct) =>
{
    UserDto? result = await sender.Send(new GetUserByNameQuery(name), ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

await app.RunAsync();
```

Note: `IDualizor` implements both `ISender` and `IPublisher`. Inject either if you only need a subset.

### Auto-registration, manual registration, and options

By default, `AddDualis` will automatically register all discovered request handlers, pipeline behaviors, and notification handlers. You can disable any auto-registration and manually register specific pieces using `DualizorOptions`.

Disable auto-registration and register manually:

```
builder.Services.AddDualis(opts =>
{
    // Disable auto-registration for any component type as needed
    opts.RegisterDiscoveredBehaviors = false;
    opts.RegisterDiscoveredCqrsHandlers = false;
    opts.RegisterDiscoveredNotificationHandlers = false;

    // Manually register pipeline behaviors (request/response or void)
    opts.Pipelines.Register<LoggingBehavior<GetUserByNameQuery, UserDto?>>();
    opts.Pipelines.Register<VoidBehavior<SomeCommand>>();

    // Manually register notification handlers
    opts.Notifications.Register<UserCreatedEventHandler>();

    // You can also register handlers via DI if preferred
    // services.AddScoped<IRequestHandler<GetUserByNameQuery, UserDto?>, GetUserByNameQueryHandler>();

    // Notifications: choose publisher and failure policy
    opts.NotificationPublisherFactory = sp => sp.GetRequiredService<ParallelWhenAllNotificationPublisher>();
    opts.NotificationFailureBehavior = NotificationFailureBehavior.ContinueAndAggregate;
    opts.MaxPublishDegreeOfParallelism = Environment.ProcessorCount;
});
```

Options overview (non-exhaustive):
- `RegisterDiscoveredBehaviors`/`RegisterDiscoveredCqrsHandlers`/`RegisterDiscoveredNotificationHandlers`: toggles for auto-registration.
- `Pipelines`: registry for manual behavior registration and pipeline settings (also controls behavior auto-registration enablement).
- `Notifications`: registry for manual notification handler registration.
- `NotificationPublisherFactory`: selects the publisher implementation (sequential is default; alternatives include `ParallelWhenAllNotificationPublisher`).
- `NotificationFailureBehavior`: `ContinueAndAggregate`, `ContinueAndLog`, `StopOnFirstException`.
- `MaxPublishDegreeOfParallelism`: degree of parallelism when using parallel publisher.

Behavior ordering: behaviors run outer ? inner in registration order; annotate with `PipelineOrderAttribute` to control execution order when using auto-registration (lower runs earlier).

## Using Dualis without the generator (runtime path)

If you cannot or do not want to enable the generator, use:

```
services.AddDualisRuntime(opts =>
{
    // Configure options, registries, and optional runtime discovery flags here.
});
```

- Uses the generated `Dualis.Dualizor` if present; otherwise falls back to a reflection-based mediator.
- Applies manual registries (`opts.Pipelines`, `opts.CQRS`, `opts.Notifications`) and can perform basic runtime discovery when enabled by options flags.

## Multi-project (DDD/Clean Architecture) setup

- Reference Dualis abstractions wherever needed.
- Enable generation only in the host project and call `services.AddDualis()` there.
- The generator scans the host compilation (and referenced assemblies) to discover public `IRequestHandler<>`/`IRequestHandler<,>` and pipeline behaviors.

Requirements:
- Handler and behavior types in referenced assemblies must be `public` (or visible via `InternalsVisibleTo` to the host).
- Other projects should NOT enable the generator.

## Pipelines

Two primary forms are supported:

- Request/response: `IPipelineBehavior<TRequest, TResponse>`
- Void request: `IPipelineBehavior<TRequest>`

A unified form `IPipelineBehaviour<TMessage, TResponse>` can apply to both requests and notifications (`Unit` for void).

Behaviors are executed in registration order (outer -> inner). You can annotate behaviors with `PipelineOrderAttribute` to control ordering when auto-registered. Lower values run earlier.

## Notifications

Define a notification and handlers:

```
public sealed record UserCreatedEvent(Guid Id) : INotification;

public sealed class UserCreatedEventHandler : INotificationHandler<UserCreatedEvent>
{
    public Task HandleAsync(UserCreatedEvent n, CancellationToken ct)
        => Task.CompletedTask;
}
```

Publish from anywhere you have `IPublisher`/`IDualizor`:

```
await dualizor.Publish(new UserCreatedEvent(id));
```

Choose failure behavior:
- `ContinueAndAggregate` – run all handlers, throw `AggregateException` of failures
- `ContinueAndLog` – log and swallow failures
- `StopOnFirstException` – stop immediately on first failure (sequential)

Choose publisher implementation via `NotificationPublisherFactory` (default: `SequentialNotificationPublisher`; alternatives: `ParallelWhenAllNotificationPublisher`, `ChannelNotificationPublisher`).

## Source generator details

- Emits `Dualis.Dualizor`, the mediator/dispatcher used by `IDualizor`.
- Emits an internal, non-extension DI method in `Dualis.Generated`:
  `ServiceCollectionExtensions.AddDualis(IServiceCollection, Action<DualizorOptions>?)`.
- The public runtime extension `Dualis.ServiceCollectionExtensions.AddDualis(this IServiceCollection, Action<DualizorOptions>?)` invokes the internal method reflectively when the generator runs in the host; otherwise it falls back to a runtime registration path.

### Gating

The generator runs when any of these is true in the host project:

- MSBuild property `DualisEnableGenerator` is visible to the compiler and set to `true`.
- A `.editorconfig`/`.globalconfig` sets `build_property.DualisEnableGenerator = true`.
- An assembly-level attribute `[assembly: Dualis.EnableDualisGeneration]` is present.

## Troubleshooting

- "AddDualis not found" or `IServiceCollection` missing extension:
  - Ensure the host project references `Dualis` and has `using Dualis;` in scope.
  - Ensure generation is enabled in the host (property, attribute, or `.editorconfig`).
  - Clean bin/obj and rebuild to clear stale analyzer artifacts.
- Ambiguous `AddDualis` call (CS0121):
  - Ensure only one Dualis analyzer is active (from the NuGet package you packed). Remove any old/stale analyzers and clean caches.
  - Only the host should enable generation.
- Not all handlers are auto-registered:
  - Ensure handler/behavior classes are `public` or exposed via `InternalsVisibleTo` to the host.
  - Ensure the host references the assemblies containing the handlers.

## Benchmarks

Basic microbenchmarks live under `tests/Dualis.Benchmarks`. Run in Release:

```
dotnet run -c Release --project tests/Dualis.Benchmarks/Dualis.Benchmarks.csproj
```

## Requirements

- Runtime library targets .NET 9
- Source generator targets .NET Standard 2.0 (works across SDKs/tooling)

## Contributing

Issues and PRs are welcome. Please run unit tests and benchmarks before submitting changes.

## License

MIT
