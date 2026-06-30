# Changelog

All notable changes to this project will be documented in this file.

## [0.4.0.2] - 2026-06-30

Fixed
- **Cross-assembly handler/behavior discovery ignored `InternalsVisibleTo`**: the source generator's cross-assembly scan (`SharedHandlerDiscovery.IsEffectivelyPublic`) only accepted types with `DeclaredAccessibility == Public`, even though the README documents `internal` handlers exposed via `InternalsVisibleTo` as supported. Layered apps that declare CQRS handlers `internal` in an Application project (a common, deliberate encapsulation choice) got a *partially* generated dispatcher: generation appeared to succeed (files were emitted), but most request types had no `case` in the generated switch, causing `InvalidOperationException: Unknown request type: ...` at runtime for any internal handler. Fixed by using Roslyn's own `Compilation.IsSymbolAccessibleWithin`, which correctly honors `InternalsVisibleTo`, instead of a hand-rolled accessibility check.

Added
- Regression test (`InternalsVisibleToDiscoveryTests.InternalHandlerVisibleViaInternalsVisibleToIsDiscoveredAcrossAssemblies`) compiling a library assembly with an `internal` handler plus `[assembly: InternalsVisibleTo]`, and asserting the host's generated dispatcher includes it.

## [0.4.0.1] - 2026-06-30

Fixed
- **Packaging regression**: the `Dualis` 0.4.0 NuGet package shipped without the source generator at all (`analyzers/dotnet/cs/Dualis.SourceGen.dll` was missing from the package). The analyzer asset items in `Dualis.csproj` were declared in a static `ItemGroup`, which MSBuild evaluates before any `Target` runs; on a clean pack the generator DLL didn't exist yet at evaluation time, so the `Exists()` condition silently dropped the item, even though a `BeforeTargets="Pack"` step built the DLL moments later. As a result, **no code was generated for any consumer of 0.4.0**, regardless of project type (Web, WASM, Desktop, etc.) — this was never a WASM-specific limitation. Fixed by moving the item declarations into the build-and-stage `Target` itself so they're added after the generator DLL exists.
- **`AddDualis()` fallback silently dropped manual registrations**: when the source generator hadn't produced output in the host assembly (e.g. due to the packaging regression above, or simply because generation wasn't enabled), the public `AddDualis()` fell back to a minimal internal stub that registered only core infrastructure — it never applied `DualizorOptions.Pipelines`, `CQRS`, or `Notifications` manual registries, and performed no auto-discovery at all. This made `opts.Pipelines.Register<TBehavior>()` (and the CQRS/Notifications equivalents) appear broken, when in fact the registries themselves were fine — they were just never reaching the DI container. Fixed by routing the fallback through the existing, fully-functional `AddDualisRuntime` reflection-based path instead, which already applies all manual registries and performs auto-discovery. The now-redundant minimal stub was removed.

Added
- Regression test (`AddDualisReflectionFallbackTests.AddDualis_WithoutGenerator_PipelinesRegister_BehaviorRunsDuringDispatch`) verifying a behavior registered via `DualizorOptions.Pipelines.Register<T>()` actually executes during dispatch when the generator is not active.

## [0.4.0] - 2026-06-29

Breaking
- Moved `IRequestExceptionHandler<TRequest, TResponse, TException>` and `IRequestExceptionAction<TRequest, TException>` from `Dualis.CQRS` to `Dualis.Pipeline`, mirroring `IPipelineBehavior`'s location and `MediatR.Pipeline`'s layout.
- Renamed `RequestExceptionState<TResponse>` to `RequestExceptionHandlerState<TResponse>` (also moved to `Dualis.Pipeline`) to match MediatR's exact type name. Members (`Handled`, `Response`, `SetHandled`) are unchanged.
- Removed the `where TRequest : IRequest<TResponse>` constraint from `IRequestExceptionHandler<,,>` and the `where TRequest : IRequest` constraint from `IRequestExceptionAction<,>`. Neither constraint exists in MediatR, and they prevented open-generic, independently-constrained "catch-all" exception handlers (e.g. `where TRequest : IQuery<Result<int>>, TResponse : Result<int>, TException : ServerException`) from compiling.

Migration
- Porting from MediatR: replace `using MediatR.Pipeline;` with `using Dualis.Pipeline;`. No other code changes are required, including for open-generic/constrained catch-all exception handlers.
- Existing Dualis consumers: replace `using Dualis.CQRS;` with `using Dualis.Pipeline;` wherever `IRequestExceptionHandler`, `IRequestExceptionAction`, or the renamed state type are referenced, and rename `RequestExceptionState<T>` to `RequestExceptionHandlerState<T>`.

Fixed
- DULIS014 (`MismatchedExceptionContractRequestAnalyzer`) no longer reports a false positive for open-generic exception handlers whose `TRequest` and `TResponse` are independently constrained to the same concrete type (the common MediatR catch-all-handler idiom).

Added
- Sample demonstrating the open-generic catch-all exception handler pattern (`samples/DDD.Application/Common`).

## [0.3.0] - 2025-10-05

Added
- Request exception handling contracts:
  - `IRequestExceptionHandler<TRequest, TResponse, TException>`
  - `IRequestExceptionAction<TRequest, TException>`
  - `RequestExceptionState<TResponse>`
- Exception handling integrated into both generated dispatcher and runtime fallback dispatcher:
  - Response requests can be handled by exception handlers and return fallback responses.
  - Unhandled exceptions trigger exception actions, then are rethrown.
- Registration/discovery support for exception contracts in manual registries, runtime discovery, and source-generated registrations.
- Unit tests for handled/unhandled flows and exception-type specificity behavior.

Changed
- Documentation updated in README for request exception handling semantics and registration examples.
- Version alignment: `Dualis` and `Dualis.Analyzers` are synchronized at `0.3.0`.

## [0.2.5] - 2025-10-04

Added
- `Dualis.Analyzer` project added to the repository. See usage, installation, and rules in [src/Dualis.Analyzer/README.md](src/Dualis.Analyzer/README.md).

Changed
- Tiny performance refactoring across generator/dispatcher hot paths.

Notes
- Documentation: Core README "What's new" updated to link the Analyzer README.

## [0.2.3] - 2025-10-03

Fixed
- Resolved CS0121 ambiguous `AddDualis` calls in multi-project (DDD/CA) solutions.

Changed
- Generator now emits an internal, non-extension method in a non-imported namespace:
  `Dualis.Generated.ServiceCollectionExtensions.AddDualis(IServiceCollection, Action<DualizorOptions>?)`.
  The public runtime entry `Dualis.ServiceCollectionExtensions.AddDualis(this IServiceCollection, Action<DualizorOptions>?)`
  reflectively invokes the generated method when present; otherwise it falls back to the runtime registration path.
- Removed buildTransitive global auto-enable of the generator. Generation must be explicitly enabled in the host project
  using one of: MSBuild property `<DualisEnableGenerator>true</DualisEnableGenerator>` (and making it compiler-visible),
  `[assembly: Dualis.EnableDualisGeneration]`, or `.editorconfig/.globalconfig` with `build_property.DualisEnableGenerator = true`.
- Documentation clarified for host-only codegen, DDD/Clean Architecture setup, and troubleshooting.

Packaging
- Single Dualis package continues to ship analyzer assets under `analyzers/dotnet/cs`. No buildTransitive props/globalconfig are shipped.
- Recommend bumping package version when testing locally to avoid analyzer caching.

Migration
- Consumers: enable the generator only in the composition root (API/Web/Worker) where you call `services.AddDualis()`.
  Do not enable it in Domain/Application/Infrastructure projects.
- Remove any extra Dualis analyzers attached in non-host projects. Clean `bin/obj` and clear NuGet caches if you reused the same version.

## [0.2.2] - 2025-10-03

Added
- `AddDualisRuntime(IServiceCollection, Action<DualizorOptions>?)` for non-generator environments. Applies manual registries and can perform basic runtime discovery of handlers/behaviors when enabled via options flags. Uses generated `Dualis.Dualizor` if present, otherwise falls back to a reflection-based mediator.
- `[assembly: Dualis.EnableDualisGeneration]` attribute to force-enable the source generator regardless of MSBuild gating.
- `ChannelNotificationPublisher` option for bounded concurrency, suitable for high-throughput scenarios.

Changed
- `ParallelWhenAllNotificationPublisher` now falls back to sequential execution when `StopOnFirstException` is selected to guarantee deterministic early stop.
- Generator and runtime internals: improved behavior caching and stability; unknown notifications safely no-op when no handlers are discovered.
- DI registration is more defensive and idempotent (`TryAdd`/`TryAddEnumerable` usage).

Docs
- README updated for 0.2.2 with runtime path and attribute-based opt-in.

Migration
- No breaking changes in this release.

## [0.2.1] - 2025-10-03

Breaking changes
- Removed ICommand/IQuery abstractions and related handler overloads.
- Removed SendAsync/PublishAsync; use ISender.Send(...) and IPublisher.Publish(...) (IDualizor implements both).

Added
- buildTransitive MSBuild props: buildTransitive/Dualis.props exposes DualisEnableGenerator to analyzers for NuGet consumers.
- SourceLink enabled for better debugging experience.

Changed
- Source generator preserves nullable annotations in emitted types to avoid nullability mismatches.
- Suppress CS1998 in generated dispatcher paths to avoid warnings when no await is needed.
- README streamlined; ProjectReference guidance clarified.

Tests / Samples
- Smoke tests updated to resolve ISender and use IRequest/IRequestHandler.
- Cross-assembly tests fixed for INotificationHandler<T>.Handle(...).

Packaging
- Package includes README and icon; ships analyzer under analyzers/dotnet/cs.
- Symbols (snupkg) produced; CHANGELOG included in package root.

Migration
- IQuery<T> ? IRequest<T>
- ICommand ? IRequest
- dualizor.SendAsync(...) ? ISender.Send(...)
- dualizor.PublishAsync(...) ? IPublisher.Publish(...)
- Enable generator in host project only via MSBuild property or .editorconfig. For ProjectReference, add CompilerVisibleProperty or use .editorconfig.
