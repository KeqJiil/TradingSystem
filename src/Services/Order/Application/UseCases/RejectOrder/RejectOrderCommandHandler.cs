using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.RejectOrder;

public class RejectOrderCommandHandler : IRequestHandler<RejectOrderCommand, Result>
{
    public Task<Result> Handle(RejectOrderCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
