![](https://raw.githubusercontent.com/TurgayTurk/Dualis/main/logo.png)

# Dualis.Analyzers

Roslyn analyzers for Dualis to improve developer experience and correctness. These analyzers run at compile time and do not affect runtime.

## What's new (Unreleased)

Added rules:
- DULIS003 (Warning): Multiple IRequestHandler implementations found for the same request.
- DULIS004 (Warning): Send called with a type that does not implement IRequest/IRequest<T>.
- DULIS005 (Warning): Handler request type does not implement the required IRequest shape.
- DULIS006 (Warning): No INotificationHandler found for published notification type.
- DULIS007 (Info): CancellationToken available in scope but not passed to Send/Publish.
- DULIS013 (Info): Avoid service locator – resolve ISender/IPublisher/IDualizor via constructor injection.

## What's new (0.1.0)

- Initial rule set:
  - DULIS001 (Info): Dualis generator not enabled in host.
  - DULIS002 (Warning): No IRequestHandler found for request type.
- Analyzer tests added (xUnit + Microsoft.CodeAnalysis.Testing).

See full details in [CHANGELOG.md](https://github.com/TurgayTurk/Dualis/blob/main/src/Dualis.Analyzer/CHANGELOG.md).

## Install
- NuGet: dotnet add package Dualis.Analyzers
- Scope: add to your solution or only to the host project. Mark as `PrivateAssets=all` if you don’t want it to flow transitively.

## Rules
- DULIS001 (Info): Dualis generator not enabled in host
  - Suggests enabling the source generator in the composition root for best perf/features.
- DULIS002 (Warning): No IRequestHandler found for request type
  - Triggers when calling ISender/IDualizor.Send with a request that has no matching handler in the compilation.
- DULIS003 (Warning): Multiple IRequestHandler implementations found for the same request
  - Detects ambiguous handler registrations for a request and warns to consolidate.
- DULIS004 (Warning): Send called with non-IRequest argument
  - Ensures the first Send argument implements IRequest/IRequest<T>.
- DULIS005 (Warning): Handler request type not implementing required IRequest shape
  - Validates IRequestHandler<TRequest, TResponse> requires TRequest : IRequest<TResponse> (and similar for void).
- DULIS006 (Warning): No INotificationHandler found for notification type
  - Triggers when publishing a notification with no matching handlers in the compilation.
- DULIS007 (Info): CancellationToken available but not passed
  - Suggests passing the in-scope CancellationToken to Send/Publish.
- DULIS013 (Info): Service locator usage for Dualis services
  - Suggests preferring constructor injection over IServiceProvider.GetRequiredService for Dualis abstractions.

## Enable the Dualis generator (host)
- MSBuild property (recommended):
  <PropertyGroup>
    <DualisEnableGenerator>true</DualisEnableGenerator>
  </PropertyGroup>
  <ItemGroup>
    <CompilerVisibleProperty Include="DualisEnableGenerator" />
  </ItemGroup>
- Assembly attribute:
  using Dualis;
  [assembly: EnableDualisGeneration]
- .editorconfig/.globalconfig:
  is_global = true
  build_property.DualisEnableGenerator = true

## Configure diagnostics via .editorconfig
- Example:
  dotnet_diagnostic.DULIS001.severity = suggestion
  dotnet_diagnostic.DULIS002.severity = none
  dotnet_diagnostic.DULIS007.severity = suggestion

## Compatibility
- Works with Roslyn 4.14+ (SDKs for .NET 7/8/9), ships as netstandard2.0 analyzer.

## Contributing
- Tests live under tests/Dualis.Analyzers.Tests.
- Issues and PRs are welcome.
