using System.Diagnostics;
using MediatR;
using Order.Domain;
using Order.Infrastructure.Observability;

namespace Order.Infrastructure.MediatrPipelines;

public class TracingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly string SpanName = $"mediatr {typeof(TRequest).Name}";

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var activity = OrderTelemetry.Source.StartActivity(SpanName);

        try
        {
            var response = await next(cancellationToken);
            if (response is IResultBase { IsFailure: true } failure)
            {
                activity?.SetTag("result.error.type", failure.Error.Type.ToString());
                activity?.SetStatus(ActivityStatusCode.Error, failure.Error.Message);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetTag("request.cancelled", true);
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
