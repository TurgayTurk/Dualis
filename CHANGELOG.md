# Changelog

All notable changes to this project will be documented in this file.

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
