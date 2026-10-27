using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.ExpireOrder;

public class ExpireOrderCommandHandler : IRequestHandler<ExpireOrderCommand, Result>
{
    public Task<Result> Handle(ExpireOrderCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
