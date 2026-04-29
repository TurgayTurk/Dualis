# Dualis.Analyzers Changelog

All notable changes to this package will be documented in this file.

## [0.3.0] - 2025-10-05
### Added
- DULIS003: Warning when multiple IRequestHandler implementations exist for the same request.
- DULIS004: Warning when Send is called with a non-IRequest/IRequest<T> argument.
- DULIS005: Warning when a handler's request type does not implement the required IRequest shape.
- DULIS006: Warning when publishing a notification with no matching INotificationHandler.
- DULIS007: Info suggesting to pass an in-scope CancellationToken to Send/Publish.
- DULIS013: Info to avoid service locator for Dualis services (prefer DI).
- DULIS014: Warning when exception contract request type does not implement the required IRequest shape.
- DULIS015: Warning when multiple IRequestExceptionHandler implementations exist for the same request/response/exception contract.

### Changed
- Internal: analyzer implementations follow netstandard2.0-friendly constructs and repo coding style.
- Version alignment: `Dualis.Analyzers` and `Dualis` are synchronized at `0.3.0`.

## [0.1.0] - 2025-10-04
### Added
- DULIS001: Info when Dualis source generator is not enabled in host project.
- DULIS002: Warning when sending a request without a matching IRequestHandler.
- Analyzer unit tests (xUnit + Microsoft.CodeAnalysis.Testing).
