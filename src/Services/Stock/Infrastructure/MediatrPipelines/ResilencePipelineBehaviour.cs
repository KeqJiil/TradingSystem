using MediatR;
using Polly;

namespace Stock.Infrastructure.MediatrPipelines;

public class ResiliencePipelineBehaviour<TRequest, TResponse>(ResiliencePipeline resiliencePipeline)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (ResilienceScope.IsActive) return await next(cancellationToken);

        return await resiliencePipeline.ExecuteAsync(static async (state, token) =>
            {
                ResilienceScope.IsActive = true;
                return await state.Next(token);
            },
            (Request: request, Next: next),
            cancellationToken);
    }
}
