using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.RequestCancel;

public class RequestCancelCommandHandler : IRequestHandler<RequestCancelCommand, Result>
{
    public Task<Result> Handle(RequestCancelCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
