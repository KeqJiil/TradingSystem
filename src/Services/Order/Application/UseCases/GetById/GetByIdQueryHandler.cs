using MediatR;
using Order.Application.Abstractions;

namespace Order.Application.UseCases.GetById;

public class GetByIdQueryHandler(IOrderReader reader) : IRequestHandler<GetByIdQuery, OrderDetailsDto?>
{
    public Task<OrderDetailsDto?> Handle(GetByIdQuery request, CancellationToken cancellationToken)
    {
        return reader.GetOrderByIdAsync(request.OrderId, request.UserId, cancellationToken);
    }
}