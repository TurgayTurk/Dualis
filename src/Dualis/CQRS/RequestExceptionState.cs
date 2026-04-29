namespace Dualis.CQRS;

/// <summary>
/// Mutable state passed to request exception handlers.
/// Allows a handler to mark an exception as handled and provide a fallback response.
/// </summary>
/// <typeparam name="TResponse">The request response type.</typeparam>
public sealed class RequestExceptionState<TResponse>
{
    /// <summary>
    /// Gets a value indicating whether the exception was handled.
    /// </summary>
    public bool Handled { get; private set; }

    /// <summary>
    /// Gets the response set by a handler when the exception is handled.
    /// </summary>
    public TResponse? Response { get; private set; }

    /// <summary>
    /// Marks the exception as handled and sets the response that should be returned.
    /// </summary>
    /// <param name="response">The response value to return from the request pipeline.</param>
    public void SetHandled(TResponse response)
    {
        Handled = true;
        Response = response;
    }
}
