using FluentValidation;
using MediatR;

namespace Stock.Infrastructure.MediatrPipelines;

public class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results.SelectMany(result => result.Errors).ToArray();

        if (failures.Length != 0) throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}
