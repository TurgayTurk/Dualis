# Changelog

All notable changes to this project will be documented in this file.

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
