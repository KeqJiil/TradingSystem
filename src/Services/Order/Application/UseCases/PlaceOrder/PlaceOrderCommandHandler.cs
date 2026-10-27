using MediatR;
using Order.Domain;

namespace Order.Application.UseCases.PlaceOrder;

public class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Result<Guid>>
{
    public Task<Result<Guid>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
