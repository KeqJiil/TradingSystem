using System.Diagnostics;
using FluentValidation;
using MediatR;
using Stock.Infrastructure.Observability;

namespace Stock.Infrastructure.MediatrPipelines;

public class TracingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly string SpanName = $"mediatr {typeof(TRequest).Name}";

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var activity = StockTelemetry.Source.StartActivity(SpanName);

        try
        {
            return await next(cancellationToken);
        }
        catch (ValidationException)
        {
            activity?.SetTag("validation.failed", true);
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