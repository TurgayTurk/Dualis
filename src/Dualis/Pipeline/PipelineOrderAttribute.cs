namespace Dualis.Pipeline;

/// <summary>
/// Specifies the execution order of a pipeline behavior. Lower values run earlier.
/// When multiple behaviors implement the same interface, they are invoked by ascending order.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PipelineOrderAttribute(int order) : Attribute
{
    /// <summary>
    /// Gets the behavior order. Lower values execute earlier.
    /// </summary>
    public int Order { get; } = order;
}
