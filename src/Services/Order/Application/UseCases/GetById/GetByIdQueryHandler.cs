using MediatR;
using Order.Application.Abstractions;
using Order.Domain;

namespace Order.Application.UseCases.GetById;

public class GetByIdQueryHandler : IRequestHandler<GetByIdQuery, Result<OrderDetailsDto>>
{
    public Task<Result<OrderDetailsDto>> Handle(GetByIdQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
