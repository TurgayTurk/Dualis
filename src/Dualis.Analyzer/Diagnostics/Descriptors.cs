using Microsoft.CodeAnalysis;

namespace Dualis.Analyzer.Diagnostics;

/// <summary>
/// Provides <see cref="DiagnosticDescriptor"/> instances for all Dualis analyzer rules.
/// </summary>
internal static class Descriptors
{
    /// <summary>
    /// Informational diagnostic suggesting to enable the Dualis source generator in the host project.
    /// </summary>
    public static readonly DiagnosticDescriptor DULIS001_GeneratorNotEnabled = new(
        id: "DULIS001",
        title: "Dualis source generator is not enabled in the host project",
        messageFormat: "Enable the generator in the host project via MSBuild property, assembly attribute, or .editorconfig",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "The Dualis generator should be enabled in the composition root (host) for best performance and features.",
        customTags: "CompilationEnd");

    /// <summary>
    /// Warning emitted when a request is sent but no handler is found.
    /// </summary>
    public static readonly DiagnosticDescriptor DULIS002_MissingHandler = new(
        id: "DULIS002",
        title: "No IRequestHandler found for request type",
        messageFormat: "No IRequestHandler was found for request type '{0}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A request is sent but no corresponding IRequestHandler was discovered or registered.");

    /// <summary>
    /// Warning emitted when multiple handlers exist for the same request type.
    /// </summary>
    public static readonly DiagnosticDescriptor DULIS003_DuplicateHandlers = new(
        id: "DULIS003",
        title: "Multiple IRequestHandler implementations found for the same request",
        messageFormat: "Multiple IRequestHandler implementations were found for request type '{0}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "More than one IRequestHandler was discovered for the same request type. Consider consolidating or adjusting DI registrations.",
        customTags: "CompilationEnd");

    /// <summary>
    /// Warning emitted when Send is called with a non-request type.
    /// </summary>
    public static readonly DiagnosticDescriptor DULIS004_InvalidSendArgument = new(
        id: "DULIS004",
        title: "Send called with a type that does not implement IRequest",
        messageFormat: "Argument type '{0}' does not implement the required IRequest/IRequest<TResponse> interface",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "ISender/IDualizor.Send must be called with types that implement IRequest or IRequest<TResponse>.");

    /// <summary>
    /// Warning emitted when a handler's request type does not implement the matching IRequest shape.
    /// </summary>
    public static readonly DiagnosticDescriptor DULIS005_MismatchedHandlerRequest = new(
        id: "DULIS005",
        title: "Handler request type does not implement the required IRequest shape",
        messageFormat: "Request type '{0}' must implement '{1}' to be used with this handler",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "IRequestHandler<TRequest, TResponse> requires TRequest : IRequest<TResponse>, and IRequestHandler<TRequest> requires TRequest : IRequest.",
        customTags: "CompilationEnd");

    /// <summary>
    /// Warning emitted when a published notification has no handlers.
    /// </summary>
    public static readonly DiagnosticDescriptor DULIS006_MissingNotificationHandler = new(
        id: "DULIS006",
        title: "No INotificationHandler found for notification type",
        messageFormat: "No INotificationHandler was found for notification type '{0}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A notification is published but no corresponding INotificationHandler was discovered or registered.");

    /// <summary>
    /// Suggestion when a CancellationToken is available but not passed to Send/Publish.
    /// </summary>
    public static readonly DiagnosticDescriptor DULIS007_TokenNotPassed = new(
        id: "DULIS007",
        title: "CancellationToken available in scope but not passed",
        messageFormat: "Pass the available CancellationToken to '{0}' for cooperative cancellation",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "When a CancellationToken is available in the current scope, pass it to Dualis Send/Publish calls.");

    /// <summary>
    /// Suggestion warning against service locator pattern for resolving Dualizor from IServiceProvider.
    /// </summary>
    public static readonly DiagnosticDescriptor DULIS013_ServiceLocatorUsage = new(
        id: "DULIS013",
        title: "Avoid service locator: prefer DI for ISender/IPublisher/IDualizor",
        messageFormat: "Resolve '{0}' via constructor injection instead of IServiceProvider",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Prefer constructor injection of ISender, IPublisher, or IDualizor rather than resolving them via IServiceProvider.");
}
