using Dualis.Pipeline;

namespace Dualis.UnitTests.Pipeline;

[PipelineOrder(-10)]
public sealed class OrderedBehaviorA<TReq, TRes>(ExecutionLog log) : IPipelineBehavior<TReq, TRes>
{
    public async Task<TRes> Handle(TReq request, RequestHandlerDelegate<TRes> next, CancellationToken cancellationToken)
    {
        log.Add("A:before");
        TRes res = await next(cancellationToken);
        log.Add("A:after");
        return res;
    }
}

[PipelineOrder(5)]
public sealed class OrderedBehaviorB<TReq, TRes>(ExecutionLog log) : IPipelineBehavior<TReq, TRes>
{
    public async Task<TRes> Handle(TReq request, RequestHandlerDelegate<TRes> next, CancellationToken cancellationToken)
    {
        log.Add("B:before");
        TRes res = await next(cancellationToken);
        log.Add("B:after");
        return res;
    }
}
