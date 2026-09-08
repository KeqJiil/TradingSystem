using MediatR;
using Polly;

namespace Stock.Infrastructure.MediatrPipelines;

public class ResiliencePipelineBehaviour<TRequest, TResponse>(ResiliencePipeline resiliencePipeline)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        return await resiliencePipeline.ExecuteAsync(static async (state, token) => await state.Next(token),
            (Request: request, Next: next),
            cancellationToken);
    }
}