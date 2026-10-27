using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.ApproveOrder;

public class ApproveOrderCommandHandler : IRequestHandler<ApproveOrderCommand, Result>
{
    public Task<Result> Handle(ApproveOrderCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
