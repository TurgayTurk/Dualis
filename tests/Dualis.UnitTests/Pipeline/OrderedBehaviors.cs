using Dualis.Pipeline;

namespace Dualis.UnitTests.Pipeline;

/// <summary>
/// Pipeline behavior used in ordering tests. Executes before and after the next delegate with order -10.
/// </summary>
[PipelineOrder(-10)]
public sealed class OrderedBehaviorA<TReq, TRes>(ExecutionLog log) : IPipelineBehavior<TReq, TRes>
{
    /// <summary>
    /// Records "A:before" then invokes the next delegate and finally records "A:after".
    /// </summary>
    public async Task<TRes> Handle(TReq request, RequestHandlerDelegate<TRes> next, CancellationToken cancellationToken)
    {
        log.Add("A:before");
        TRes res = await next(cancellationToken);
        log.Add("A:after");
        return res;
    }
}

/// <summary>
/// Pipeline behavior used in ordering tests. Executes after <see cref="OrderedBehaviorA{TReq, TRes}"/> with order 5.
/// </summary>
[PipelineOrder(5)]
public sealed class OrderedBehaviorB<TReq, TRes>(ExecutionLog log) : IPipelineBehavior<TReq, TRes>
{
    /// <summary>
    /// Records "B:before" then invokes the next delegate and finally records "B:after".
    /// </summary>
    public async Task<TRes> Handle(TReq request, RequestHandlerDelegate<TRes> next, CancellationToken cancellationToken)
    {
        log.Add("B:before");
        TRes res = await next(cancellationToken);
        log.Add("B:after");
        return res;
    }
}
